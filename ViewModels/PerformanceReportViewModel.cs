namespace Pressing.ViewModels;

public sealed class PerformanceReportViewModel
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public int InactiveCustomerCount { get; init; }
    public IReadOnlyList<OperatorPerformanceViewModel> Operators { get; init; } = [];
}

public sealed class OperatorPerformanceViewModel
{
    public string Name { get; init; } = string.Empty;
    public int OrdersCreated { get; init; }
    public int OrdersReady { get; init; }
    public int OrdersDelivered { get; init; }
    public decimal Revenue { get; init; }
    public double? AverageProcessingHours { get; init; }
}
