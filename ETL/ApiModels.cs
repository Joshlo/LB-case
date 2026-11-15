using System.Net;

namespace ETL;

public record ApiResult<T>(HttpStatusCode StatusCode, T? Data = default(T), ErrorResponse? Error = null)
{
    public bool IsSuccess => this.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created;
}

public record AllocationRequest(string ParcelId, Sizes SizeRequired, string RecipientId, string PinCode);

public record AllocationResponse(string AllocationId, string LockerId, string CompartmentId, string PinCode, DateTimeOffset AllocatedAt, DateTimeOffset ExpiresAt);

public record CompartmentModel(
    string CompartmentId,
    Sizes Size,
    OccupancyStatuses OccupancyStatus,
    DoorStates DoorState,
    SensorStatuses SensorStatus,
    string? CurrentParcelId,
    DateTimeOffset? AllocatedAt);

// ApiModels.cs
public enum OccupancyStatuses { Available, Reserved, Occupied, OutOfService }
public enum DoorStates { Closed, Open, DoorNotClosed }
public enum SensorStatuses { Empty, ParcelPresent, ParcelPickedUp }

public record LockerModel(
    string LockerId,
    string Name,
    string Address,
    int TotalCompartments
);

public record NotificationRequest(string RecipientId, string ParcelId, string LockerId, string PinCode, string Channel, string TextBody);
public record NotificationResponse(string NotificationId, string RecipientId, string ParcelId, string DeliveredChannel, DateTimeOffset DispatchedAt);

public record ReleaseRequest(string ParcelId, string Reason);

public record ErrorResponse(string Code, string Message);