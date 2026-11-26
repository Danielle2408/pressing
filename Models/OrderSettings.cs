namespace Pressing.Models;

public sealed class OrderSettings
{
    public decimal ExpressSurchargePercent { get; set; } = 25;
    public decimal HomeCollectionFee { get; set; }
    public decimal HomeDeliveryFee { get; set; }
    public int DefaultProcessingDays { get; set; } = 3;
}
