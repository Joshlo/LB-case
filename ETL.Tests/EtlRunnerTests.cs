using System.Net;
using ETL.Tests.TestHelpers;
using FakeItEasy;

namespace ETL.Tests;

public class EtlRunnerTests : IDisposable
{
    private const string PinCode = "12345678";

    private static readonly DateTimeOffset Timestamp = CsvRow.DefaultTimestamp;

    private readonly IApiClient _apiClient = A.Fake<IApiClient>();
    private readonly IStateStore _stateStore = A.Fake<IStateStore>();
    private readonly List<ParcelState> _savedStates = [];
    private readonly EtlRunner _sut;

    private TempCsvFile? _csvFile;

    public EtlRunnerTests()
    {
        // Happy-path defaults: one locker with a free M compartment, and every API call succeeds.
        var freeCompartment = new CompartmentModel(
            "C1", Sizes.M, OccupancyStatuses.Available, DoorStates.Closed, SensorStatuses.Empty, null, null);
        var occupiedCompartment = freeCompartment with { OccupancyStatus = OccupancyStatuses.Occupied };

        A.CallTo(() => this._apiClient.GetLockers())
            .Returns(new ApiResult<List<LockerModel>?>(HttpStatusCode.OK, [new LockerModel("L1", "Locker 1", "Street 1", 10)]));

        A.CallTo(() => this._apiClient.GetCompartments(A<string>._))
            .Returns(new ApiResult<List<CompartmentModel>?>(HttpStatusCode.OK, [freeCompartment]));

        A.CallTo(() => this._apiClient.GetCompartment(A<string>._, A<string>._))
            .Returns(new ApiResult<CompartmentModel?>(HttpStatusCode.OK, occupiedCompartment));

        A.CallTo(() => this._apiClient.Allocate(A<string>._, A<AllocationRequest>._))
            .ReturnsLazily((string lockerId, AllocationRequest request) => new ApiResult<AllocationResponse>(
                HttpStatusCode.Created,
                new AllocationResponse("A1", lockerId, "C1", request.PinCode, Timestamp, Timestamp.AddDays(3))));

        A.CallTo(() => this._apiClient.Release(A<string>._, A<string>._, A<ReleaseRequest>._))
            .Returns(new ApiResult<CompartmentModel>(HttpStatusCode.OK, freeCompartment));

        A.CallTo(() => this._apiClient.SendNotification(A<NotificationRequest>._))
            .ReturnsLazily((NotificationRequest request) => new ApiResult<NotificationResponse>(
                HttpStatusCode.Created,
                new NotificationResponse("N1", request.RecipientId, request.ParcelId, request.Channel, Timestamp)));

        A.CallTo(() => this._stateStore.GetState(A<string>._)).Returns(null);
        A.CallTo(() => this._stateStore.SetState(A<ParcelState>._))
            .Invokes((ParcelState state) => this._savedStates.Add(state));

        var codeGenerator = A.Fake<ICodeGenerator>();
        A.CallTo(() => codeGenerator.Generate()).Returns(PinCode);

        this._sut = new EtlRunner(this._apiClient, codeGenerator, this._stateStore);
    }

    public void Dispose() => this._csvFile?.Dispose();

    private Task RunWith(params CsvRow[] rows)
    {
        this._csvFile = TempCsvFile.Create(rows);
        return this._sut.Run(this._csvFile.Path);
    }

    [Fact]
    public async Task Run_NewParcel_AllocatesCompartmentAndNotifiesRecipient()
    {
        await this.RunWith(new CsvRow());

        A.CallTo(() => this._apiClient.Allocate("L1", new AllocationRequest("P1", Sizes.M, "R1", PinCode)))
            .MustHaveHappenedOnceExactly();

        ParcelState saved = Assert.Single(this._savedStates);
        Assert.Equal(new ParcelState("P1", "L1", "C1", Sizes.M, Timestamp, StateStatuses.Allocated), saved);

        // Recipient has both phone and email, so both channels are used.
        A.CallTo(() => this._apiClient.SendNotification(
                A<NotificationRequest>.That.Matches(r => r.TextBody == Messages.Allocated("P1", "L1", PinCode))))
            .MustHaveHappenedTwiceExactly();
    }

    [Fact]
    public async Task Run_InvalidRecord_StoresUnknownStateWithoutAllocating()
    {
        await this.RunWith(new CsvRow { Size = "" });

        ParcelState saved = Assert.Single(this._savedStates);
        Assert.Equal(StateStatuses.Unknown, saved.Status);
        A.CallTo(() => this._apiClient.Allocate(A<string>._, A<AllocationRequest>._)).MustNotHaveHappened();
        A.CallTo(() => this._apiClient.SendNotification(A<NotificationRequest>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Run_MultipleRowsForSameParcel_OnlyProcessesLatest()
    {
        await this.RunWith(
            new CsvRow { EventId = "evt-2", Timestamp = Timestamp.AddHours(1), TargetLockerId = "L2" },
            new CsvRow { EventId = "evt-1", Timestamp = Timestamp, TargetLockerId = "L1" });

        A.CallTo(() => this._apiClient.Allocate("L2", A<AllocationRequest>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => this._apiClient.Allocate("L1", A<AllocationRequest>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Run_AllocatedParcelUnchanged_OnlyUpdatesTimestamp()
    {
        var existing = new ParcelState("P1", "L1", "C1", Sizes.M, Timestamp.AddDays(-1), StateStatuses.Allocated);
        A.CallTo(() => this._stateStore.GetState("P1")).Returns(existing);

        await this.RunWith(new CsvRow());

        ParcelState saved = Assert.Single(this._savedStates);
        Assert.Equal(existing with { Timestamp = Timestamp }, saved);
        A.CallTo(() => this._apiClient.Allocate(A<string>._, A<AllocationRequest>._)).MustNotHaveHappened();
        A.CallTo(() => this._apiClient.SendNotification(A<NotificationRequest>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Run_TargetLockerChanged_ReallocatesToNewLockerAndReleasesOld()
    {
        var existing = new ParcelState("P1", "L1", "C1", Sizes.M, Timestamp.AddDays(-1), StateStatuses.Allocated);
        A.CallTo(() => this._stateStore.GetState("P1")).Returns(existing);

        await this.RunWith(new CsvRow { TargetLockerId = "L2" });

        // The new compartment is secured before the old one is released.
        A.CallTo(() => this._apiClient.Allocate("L2", A<AllocationRequest>._))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => this._apiClient.Release("L1", "C1", A<ReleaseRequest>._))
                .MustHaveHappenedOnceExactly());

        Assert.Equal(
            new ParcelState("P1", "L2", "C1", Sizes.M, Timestamp, StateStatuses.Allocated),
            this._savedStates.Last());

        // Only the new allocation is sent (SMS + Email) - no "please wait" message.
        A.CallTo(() => this._apiClient.SendNotification(
                A<NotificationRequest>.That.Matches(r => r.TextBody == Messages.Allocated("P1", "L2", PinCode))))
            .MustHaveHappenedTwiceExactly();
        A.CallTo(() => this._apiClient.SendNotification(A<NotificationRequest>._))
            .MustHaveHappenedTwiceExactly();
    }
}
