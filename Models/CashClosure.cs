namespace Pressing.Models;

public sealed class CashClosure
{
    public int Id { get; set; }
    public string OperatorId { get; set; } = string.Empty;
    public ApplicationUser Operator { get; set; } = null!;
    public DateOnly BusinessDate { get; set; }
    public decimal ExpectedCash { get; set; }
    public decimal CountedCash { get; set; }
    public decimal Variance => CountedCash - ExpectedCash;
    public decimal OtherPayments { get; set; }
    public int PaymentCount { get; set; }
    public string? Note { get; set; }
    public DateTime ClosedAtUtc { get; set; } = DateTime.UtcNow;
}
