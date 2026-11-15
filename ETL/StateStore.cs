namespace ETL;

public interface IStateStore
{
    ParcelState? GetState(string parcelId);
    void SetState(ParcelState state);
    void RemoveState(string parcelId);
}

public class StateStore : IStateStore
{
    private readonly Dictionary<string, ParcelState> _states = new Dictionary<string, ParcelState>();

    public ParcelState? GetState(string parcelId) => this._states.GetValueOrDefault(parcelId);
    public void SetState(ParcelState state) => this._states[state.ParcelId] = state;
    public void RemoveState(string parcelId) => this._states.Remove(parcelId);
    
}

public record ParcelState(
    string ParcelId, 
    string? LockerId, 
    string? CompartmentId, 
    Sizes? CompartmentSize, 
    DateTimeOffset Timestamp, 
    StateStatuses Status = StateStatuses.Unknown);


public enum StateStatuses
{
    Unknown,
    Allocated,
    Released,
    Completed,
}