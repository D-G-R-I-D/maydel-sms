namespace Maydel.Domain.Audit;

/// <summary>
/// Append-only record of every data change, written automatically by the persistence layer.
/// Replaces the old manually-written "activity log".
/// </summary>
public class AuditLog
{
    public long Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public required string Action { get; set; }
    public required string EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Changes { get; set; }
    public string? IpAddress { get; set; }
}
