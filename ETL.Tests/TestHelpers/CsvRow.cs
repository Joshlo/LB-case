namespace ETL.Tests.TestHelpers;

/// <summary>
/// A single row in the ETL input file. Every column has a sensible default so a test
/// only has to override the values it actually cares about.
/// </summary>
public record CsvRow
{
    public static readonly DateTimeOffset DefaultTimestamp = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    public string EventId { get; init; } = "evt-1";
    public DateTimeOffset Timestamp { get; init; } = DefaultTimestamp;
    public string ParcelId { get; init; } = "P1";
    public string RecipientId { get; init; } = "R1";
    public string RecipientName { get; init; } = "Jane Doe";
    public string RecipientPhone { get; init; } = "+4512345678";
    public string RecipientEmail { get; init; } = "jane@example.com";
    public string RecipientPostalCode { get; init; } = "8000";
    public string RetailerId { get; init; } = "RET1";
    public string TargetLockerId { get; init; } = "L1";
    public string Size { get; init; } = "M";

    public const string Header =
        "event_id,timestamp,parcel_id,recipient_id,recipient_name,recipient_phone,recipient_email," +
        "recipient_postal_code,retailer_id,target_locker_id,size";

    public string ToCsvLine() => string.Join(",",
        this.EventId,
        this.Timestamp.ToString("O"),
        this.ParcelId,
        this.RecipientId,
        this.RecipientName,
        this.RecipientPhone,
        this.RecipientEmail,
        this.RecipientPostalCode,
        this.RetailerId,
        this.TargetLockerId,
        this.Size);
}
