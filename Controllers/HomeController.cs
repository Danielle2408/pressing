using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pressing.Data;
using Pressing.Models;
using Pressing.Security;
using Pressing.ViewModels;

namespace Pressing.Controllers;

public class HomeController(ApplicationDbContext database) : Controller
{
    public async Task<IActionResult> Index()
    {
        var isAdmin = User.IsInRole(Roles.Administrator);
        var isOperator = User.IsInRole(Roles.Operator);
        if (!isAdmin && !isOperator) return View(new DashboardViewModel());

        var today = DateOnly.FromDateTime(DateTime.Now);
        var startToday = LocalDayUtc(today);
        var startTomorrow = LocalDayUtc(today.AddDays(1));
        var startYesterday = LocalDayUtc(today.AddDays(-1));
        var startWeek = LocalDayUtc(today.AddDays(-6));
        var startPreviousWeek = LocalDayUtc(today.AddDays(-13));
        var startMonth = LocalDayUtc(new DateOnly(today.Year, today.Month, 1));
        var previousMonthDate = new DateOnly(today.Year, today.Month, 1).AddMonths(-1);
        var startPreviousMonth = LocalDayUtc(previousMonthDate);
        var now = DateTime.UtcNow;

        var orders = database.LaundryOrders.AsNoTracking();
        var paid = database.OrderPayments.AsNoTracking().Where(payment => payment.Status == OrderPaymentStatus.Confirmed);
        var viewModel = new DashboardViewModel
        {
            IsStaffDashboard = true,
            IsAdministrator = isAdmin,
            ReceivedToday = await orders.CountAsync(order => order.CreatedAtUtc >= startToday && order.CreatedAtUtc < startTomorrow),
            ReadyToday = await orders.CountAsync(order => order.Status == OrderStatus.Ready && order.ExpectedReadyAtUtc >= startToday && order.ExpectedReadyAtUtc < startTomorrow),
            DeliveredToday = await orders.CountAsync(order => order.Status == OrderStatus.Delivered && order.StatusHistory.Any(history => history.NewStatus == OrderStatus.Delivered && history.ChangedAtUtc >= startToday && history.ChangedAtUtc < startTomorrow)),
            OverdueCount = await orders.CountAsync(order => order.ExpectedReadyAtUtc < now && order.Status != OrderStatus.Delivered && order.Status != OrderStatus.Cancelled),
            LowStockCount = await database.InventoryItems.CountAsync(item => item.IsActive && item.QuantityOnHand <= item.AlertThreshold),
            OldUnpaidCount = await orders.CountAsync(order => order.CreatedAtUtc < now.AddDays(-7) && order.AmountPaid < order.TotalAmount && order.Status != OrderStatus.Cancelled),
            RevenueToday = await paid.Where(payment => payment.CreatedAtUtc >= startToday && payment.CreatedAtUtc < startTomorrow).SumAsync(payment => (decimal?)payment.Amount) ?? 0m,
            RevenueYesterday = await paid.Where(payment => payment.CreatedAtUtc >= startYesterday && payment.CreatedAtUtc < startToday).SumAsync(payment => (decimal?)payment.Amount) ?? 0m,
            RevenueThisWeek = await paid.Where(payment => payment.CreatedAtUtc >= startWeek && payment.CreatedAtUtc < startTomorrow).SumAsync(payment => (decimal?)payment.Amount) ?? 0m,
            RevenuePreviousWeek = await paid.Where(payment => payment.CreatedAtUtc >= startPreviousWeek && payment.CreatedAtUtc < startWeek).SumAsync(payment => (decimal?)payment.Amount) ?? 0m,
            RevenueThisMonth = await paid.Where(payment => payment.CreatedAtUtc >= startMonth && payment.CreatedAtUtc < startTomorrow).SumAsync(payment => (decimal?)payment.Amount) ?? 0m,
            RevenuePreviousMonth = await paid.Where(payment => payment.CreatedAtUtc >= startPreviousMonth && payment.CreatedAtUtc < startMonth).SumAsync(payment => (decimal?)payment.Amount) ?? 0m,
            Workload = await orders.Where(order => order.Status != OrderStatus.Delivered && order.Status != OrderStatus.Cancelled)
                .GroupBy(order => order.Status).Select(group => new OrderStatusCountViewModel { Status = group.Key, Count = group.Count() }).ToListAsync(),
            UrgentOrders = await orders.Include(order => order.Customer)
                .Where(order => order.Status != OrderStatus.Delivered && order.Status != OrderStatus.Cancelled && order.ExpectedReadyAtUtc < startTomorrow)
                .OrderBy(order => order.ExpectedReadyAtUtc).Take(20).ToListAsync()
        };

        if (isAdmin)
        {
            viewModel = new DashboardViewModel
            {
                IsStaffDashboard = true, IsAdministrator = true,
                ReceivedToday = viewModel.ReceivedToday, ReadyToday = viewModel.ReadyToday,
                DeliveredToday = viewModel.DeliveredToday, OverdueCount = viewModel.OverdueCount,
                LowStockCount = viewModel.LowStockCount, OldUnpaidCount = viewModel.OldUnpaidCount,
                RevenueToday = viewModel.RevenueToday, RevenueYesterday = viewModel.RevenueYesterday,
                RevenueThisWeek = viewModel.RevenueThisWeek, RevenuePreviousWeek = viewModel.RevenuePreviousWeek,
                RevenueThisMonth = viewModel.RevenueThisMonth, RevenuePreviousMonth = viewModel.RevenuePreviousMonth,
                Workload = viewModel.Workload, UrgentOrders = viewModel.UrgentOrders,
                ServiceRevenue = await database.OrderLines.AsNoTracking()
                    .Where(line => line.LaundryOrder.CreatedAtUtc >= startMonth && line.LaundryOrder.CreatedAtUtc < startTomorrow)
                    .GroupBy(line => line.ServiceName).Select(group => new DashboardAmountViewModel
                    { Label = group.Key, Amount = group.Sum(line => line.LineTotal) }).OrderByDescending(item => item.Amount).Take(6).ToListAsync(),
                TopCustomers = await orders.Where(order => order.CreatedAtUtc >= startMonth && order.CreatedAtUtc < startTomorrow)
                    .GroupBy(order => order.Customer.FullName).Select(group => new DashboardAmountViewModel
                    { Label = group.Key, Amount = group.Sum(order => order.TotalAmount) }).OrderByDescending(item => item.Amount).Take(6).ToListAsync()
            };
        }
        return View(viewModel);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    private static DateTime LocalDayUtc(DateOnly date) => TimeZoneInfo.ConvertTimeToUtc(
        DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified), TimeZoneInfo.Local);
}
