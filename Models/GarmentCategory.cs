namespace Pressing.Models;

public sealed class GarmentCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<ServiceTariff> Tariffs { get; set; } = new List<ServiceTariff>();
}
