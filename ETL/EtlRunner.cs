using System.Net.Http.Headers;
using System.Text.Json;
using CsvHelper;

namespace ETL;

public class EtlRunner(IApiClient apiClient, ICodeGenerator codeGenerator, IStateStore stateStore)
{
    public async Task Run(string fileName)
    {
        ApiResult<List<LockerModel>?> lockerResponse = await apiClient.GetLockers();

        if (!lockerResponse.IsSuccess)
        {
            Console.WriteLine("Failed to retrieve locker data.");
            return;
        }

        List<LockerModel> lockers = lockerResponse.Data ?? [];

        await using var cvsReader = CsvReader.FromFile(fileName);

        IEnumerable<EtlRecord> records = cvsReader.GetRecords<EtlRecord>();

        // lpcs stands for latest parcel records
        var lprs = records.GroupBy(r => r.ParcelId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(r => r.TimeStamp).ThenByDescending(r => r.EventId, StringComparer.OrdinalIgnoreCase).First())
            .ToList();

        foreach (EtlRecord record in lprs)
        {
            ParcelState? state = stateStore.GetState(record.ParcelId);

            if (state is not null && record.TimeStamp <= state.Timestamp)
            {
                // This is an older record, we can skip it
                continue;
            }
            
            switch (state?.Status)
            {
                case null or StateStatuses.Unknown or StateStatuses.Released:
                    await this.NewAllocation(record);
                    continue;
                case StateStatuses.Completed:
                    continue;
            }

            if (!record.IsValid())
            {
                Console.WriteLine($"Record with event ID {record.EventId} is invalid. Skipping.");
                continue;
            }

            if (state is { Status: StateStatuses.Allocated } && !HasChanged(record, state))
            {
                // This is a duplicate record, we can skip it
                stateStore.SetState(state with
                {
                    Timestamp = record.TimeStamp
                });
                continue;
            }

            switch (await ShouldReallocate(record, state))
            {
                case ReallocationStates.Reallocate:
                    await Reallocate(record, state);
                    break;
                case ReallocationStates.Completed:
                    stateStore.SetState(state with { Status = StateStatuses.Completed, Timestamp = record.TimeStamp });
                    break;
                case ReallocationStates.Conflict:
                case ReallocationStates.Unknown:
                    Console.WriteLine($"Record with event ID {record.EventId} is conflicting. Skipping for now - handle it manually.");
                    break;
                case ReallocationStates.NoReallocate:
                    stateStore.SetState(state with { Timestamp = record.TimeStamp });
                    break;
            }
        }
    }

    private async Task Reallocate(EtlRecord record, ParcelState state)
    {
        if (await this.HasAvailableCompartments(record.TargetLockerId!, record.Size!.Value))
        {
            var pinCode = codeGenerator.Generate();

            ApiResult<AllocationResponse> allocationResult = await apiClient.Allocate(record.TargetLockerId!, new AllocationRequest(record.ParcelId, record.Size!.Value, record.RecipientId, pinCode));
            if (allocationResult.IsSuccess)
            {
                Console.WriteLine($"Reallocated {record.ParcelId} to {allocationResult.Data!.LockerId}/{allocationResult.Data.CompartmentId}.");

                ApiResult<CompartmentModel> releaseResult = await apiClient.Release(state.LockerId!, state.CompartmentId!, new ReleaseRequest(record.ParcelId, "Reallocation due to size or locker change"));

                if (releaseResult.IsSuccess)
                {
                    Console.WriteLine($"Released {record.ParcelId} from {state.LockerId}/{state.CompartmentId}.");
                }
                else
                {
                    Console.WriteLine($"Release failed for {record.ParcelId} in {state.LockerId}/{state.CompartmentId}. Manual cleanup needed.");
                }
                
                // The reason we do this twice is that a store like this would have logging
                stateStore.SetState(state with
                {
                    Timestamp = record.TimeStamp,
                    Status = StateStatuses.Released,
                });
                
                stateStore.SetState(state with
                {
                    Timestamp = record.TimeStamp,
                    Status = StateStatuses.Allocated,
                    LockerId = allocationResult.Data!.LockerId,
                    CompartmentId = allocationResult.Data.CompartmentId,
                    CompartmentSize = record.Size!.Value,
                });

                await this.SendNotifications(
                    record,
                    allocationResult.Data!.LockerId,
                    allocationResult.Data!.PinCode,
                    Messages.Allocated(
                        record.ParcelId,
                        allocationResult.Data!.LockerId,
                        allocationResult.Data!.PinCode));
            }
        }
    }
    
    private async Task NewAllocation(EtlRecord record)
    {
        if (!record.IsValid())
        {
            stateStore.SetState(new ParcelState(
                record.ParcelId,
                record.TargetLockerId,
                null,
                record.Size,
                record.TimeStamp
            ));
            return;
        }

        if (await this.HasAvailableCompartments(record.TargetLockerId!, record.Size!.Value))
        {
            var pinCode = codeGenerator.Generate();
            ApiResult<AllocationResponse> allocationResult = await apiClient.Allocate(record.TargetLockerId!, new AllocationRequest(record.ParcelId, record.Size!.Value, record.RecipientId, pinCode));
            if (allocationResult.IsSuccess)
            {
                Console.WriteLine($"Allocated {record.ParcelId} to {allocationResult.Data!.LockerId}/{allocationResult.Data.CompartmentId}.");

                stateStore.SetState(new ParcelState(
                    record.ParcelId,
                    allocationResult.Data!.LockerId,
                    allocationResult.Data!.CompartmentId,
                    record.Size,
                    record.TimeStamp,
                    StateStatuses.Allocated
                ));

                await this.SendNotifications(
                    record,
                    allocationResult.Data!.LockerId,
                    allocationResult.Data!.PinCode,
                    Messages.Allocated(
                        record.ParcelId,
                        allocationResult.Data!.LockerId,
                        allocationResult.Data!.PinCode));
            }
        }
    }

    private async Task<ReallocationStates> ShouldReallocate(EtlRecord record, ParcelState state)
    {
        if(state.LockerId is null || state.CompartmentId is null)
        {
            return ReallocationStates.Unknown;
        }
        
        ApiResult<CompartmentModel?> response = await apiClient.GetCompartment(state.LockerId!, state.CompartmentId!);

        if (response is { IsSuccess: true })
        {
            CompartmentModel compartment = response.Data!;
            
            if(compartment.OccupancyStatus == OccupancyStatuses.Available)
            {
                return ReallocationStates.Completed;
            }

            if (compartment.SensorStatus != SensorStatuses.Empty)
            {
                return ReallocationStates.Conflict;
            }
            
            return record.Size > state.CompartmentSize ||
                   record.TargetLockerId != state.LockerId
                ? ReallocationStates.Reallocate
                : ReallocationStates.NoReallocate;
        }

        return ReallocationStates.Unknown;
    }

    private enum ReallocationStates
    {
        Unknown,
        Reallocate,
        NoReallocate,
        Completed,
        Conflict,
    }

    private Task<ApiResult<NotificationResponse>[]> SendNotifications(EtlRecord record, string lockerId, string pinCode, string message)
    {
        List<string> channels = [];
        if(record.CanReceiveSms)
        {
            channels.Add("SMS");
        }
        if(record.CanReceiveEmail)
        {
            channels.Add("Email");
        }
        
        return Task.WhenAll(channels.Select(channel =>
            apiClient.SendNotification(new NotificationRequest(
                record.RecipientId, record.ParcelId, lockerId, pinCode, channel, message))));
    }

    private static bool HasChanged(EtlRecord record, ParcelState state)
    {
        return record.TargetLockerId != state.LockerId || record.Size != state.CompartmentSize;
    }

    private async Task<bool> HasAvailableCompartments(string lockerId, Sizes size)
    {
        ApiResult<List<CompartmentModel>?> response = await apiClient.GetCompartments(lockerId);
        return response.IsSuccess && FindAvailableCompartment(response.Data!, size) is not null;
    }
    
    private static CompartmentModel? FindAvailableCompartment(List<CompartmentModel> compartments, Sizes size)
    {
        return compartments.FirstOrDefault(c => c.OccupancyStatus == OccupancyStatuses.Available && c.Size >= size);
    }
}
