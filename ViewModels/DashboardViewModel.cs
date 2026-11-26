using Pressing.Models;

namespace Pressing.ViewModels;

public sealed class DashboardViewModel
{
    public bool IsStaffDashboard { get; init; }
    public bool IsAdministrator { get; init; }
    public int ReceivedToday { get; init; }
    public int ReadyToday { get; init; }
    public int DeliveredToday { get; init; }
    public int OverdueCount { get; init; }
    public int LowStockCount { get; init; }
    public int OldUnpaidCount { get; init; }
    public decimal RevenueToday { get; init; }
    public decimal RevenueYesterday { get; init; }
    public decimal RevenueThisWeek { get; init; }
    public decimal RevenuePreviousWeek { get; init; }
    public decimal RevenueThisMonth { get; init; }
    public decimal RevenuePreviousMonth { get; init; }
    public IReadOnlyList<OrderStatusCountViewModel> Workload { get; init; } = [];
    public IReadOnlyList<LaundryOrder> UrgentOrders { get; init; } = [];
    public IReadOnlyList<DashboardAmountViewModel> ServiceRevenue { get; init; } = [];
    public IReadOnlyList<DashboardAmountViewModel> TopCustomers { get; init; } = [];
}

public sealed class OrderStatusCountViewModel
{
    public OrderStatus Status { get; init; }
    public int Count { get; init; }
}

public sealed class DashboardAmountViewModel
{
    public string Label { get; init; } = string.Empty;
    public decimal Amount { get; init; }
}
