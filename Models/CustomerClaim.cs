namespace Pressing.Models;

public enum ClaimStatus
{
    Open,
    UnderReview,
    Resolved,
    Rejected
}

public static class ClaimStatusLabels
{
    public static string ToFrench(this ClaimStatus status) => status switch
    {
        ClaimStatus.Open => "Ouverte",
        ClaimStatus.UnderReview => "En cours d’examen",
        ClaimStatus.Resolved => "Résolue",
        ClaimStatus.Rejected => "Rejetée",
        _ => status.ToString()
    };
}

public sealed class CustomerClaim
{
    public int Id { get; set; }
    public int LaundryOrderId { get; set; }
    public LaundryOrder LaundryOrder { get; set; } = null!;
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public ClaimStatus Status { get; set; } = ClaimStatus.Open;
    public string? Decision { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public ApplicationUser CreatedByUser { get; set; } = null!;
    public string? ResolvedByUserId { get; set; }
    public ApplicationUser? ResolvedByUser { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAtUtc { get; set; }
}
