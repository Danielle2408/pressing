namespace Pressing.Models;

public sealed class ServiceTariff
{
    public int Id { get; set; }
    public int GarmentCategoryId { get; set; }
    public GarmentCategory GarmentCategory { get; set; } = null!;
    public int LaundryServiceId { get; set; }
    public LaundryService LaundryService { get; set; } = null!;
    public decimal Price { get; set; }
    public DateTime ValidFromUtc { get; set; }
    public DateTime? ValidUntilUtc { get; set; }
}
