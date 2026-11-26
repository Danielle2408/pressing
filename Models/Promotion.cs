namespace Pressing.Models;

public enum PromotionKind
{
    Percentage,
    FixedAmount
}

public sealed class Promotion
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NormalizedCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PromotionKind Kind { get; set; }
    public decimal Value { get; set; }
    public int? GarmentCategoryId { get; set; }
    public GarmentCategory? GarmentCategory { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreatedByUserId { get; set; } = string.Empty;
    public ApplicationUser CreatedByUser { get; set; } = null!;
}
