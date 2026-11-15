namespace ETL;

public static class Messages
{
    public static string Allocated(string parcelId, string lockerId, string pinCode) =>
        $"Your parcel {parcelId} has been allocated to locker {lockerId}. Please use pin code {pinCode} to retrieve it.";
    
    public static string Reallocating(string parcelId) =>
        $"Your parcel {parcelId} will be reallocated, please wait for new instructions.";
}
