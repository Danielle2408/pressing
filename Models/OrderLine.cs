namespace Pressing.Models;

public sealed class OrderLine
{
    public int Id { get; set; }
    public int LaundryOrderId { get; set; }
    public LaundryOrder LaundryOrder { get; set; } = null!;
    public int? ServiceTariffId { get; set; }
    public ServiceTariff? ServiceTariff { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public string? Color { get; set; }
    public string? Brand { get; set; }
    public string? ReceptionCondition { get; set; }
    public string? Notes { get; set; }
}
