namespace Pressing.Models;

public sealed class AuditLog
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public string Action { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
