using CsvHelper;

namespace ETL;

[CsvRecord]
internal partial class EtlRecord
{
    [CsvColumn("event_id")]
    public string EventId { get; set; }

    [CsvColumn("timestamp")]
    public DateTimeOffset TimeStamp { get; set; }

    [CsvColumn("parcel_id")]
    public string ParcelId { get; set; }
    
    [CsvColumn("recipient_id")]
    public string RecipientId { get; set; }
    
    [CsvColumn("recipient_name")]
    public string RecipientName { get; set; }
    
    [CsvColumn("recipient_phone")]
    public string RecipientPhone { get; set; }
    
    [CsvColumn("recipient_email")]
    public string RecipientEmail { get; set; }
    
    [CsvColumn("recipient_postal_code")]
    public string RecipientPostalCode { get; set; }

    [CsvColumn("retailer_id")]
    public string RetailerId { get; set; }

    [CsvColumn("target_locker_id")]
    public string? TargetLockerId { get; set; }

    [CsvColumn("size")]
    public Sizes? Size { get; set; }

    public bool IsValid() =>
        !string.IsNullOrWhiteSpace(this.ParcelId) &&
        (!string.IsNullOrWhiteSpace(this.RecipientPhone) || !string.IsNullOrWhiteSpace(this.RecipientEmail)) &&
        !string.IsNullOrWhiteSpace(this.TargetLockerId) &&
        this.Size != null;

    public bool CanReceiveSms => !string.IsNullOrWhiteSpace(this.RecipientPhone);
    public bool CanReceiveEmail => !string.IsNullOrWhiteSpace(this.RecipientEmail);
}

public enum Sizes
{
    S,
    M,
    L,
}