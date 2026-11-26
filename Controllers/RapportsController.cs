using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pressing.Data;
using Pressing.Models;
using Pressing.Security;
using Pressing.Services;
using Pressing.ViewModels;

namespace Pressing.Controllers;

[Authorize(Policy = Roles.Administrator)]
[Route("Administration/Rapports/[action]")]
public sealed class RapportsController(ApplicationDbContext database, UserManager<ApplicationUser> users) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(DateOnly? from, DateOnly? to)
    {
        return View(await BuildReportAsync(from, to));
    }

    [HttpGet]
    public async Task<IActionResult> ExporterCsv(DateOnly? from, DateOnly? to)
    {
        var report = await BuildReportAsync(from, to);
        var rows = new List<string> { "Operateur;Commandes creees;Commandes pretes;Commandes livrees;Delai moyen (heures);Ventes (FCFA)" };
        rows.AddRange(report.Operators.Select(item => string.Join(';', [
            Csv(item.Name), item.OrdersCreated.ToString(CultureInfo.InvariantCulture),
            item.OrdersReady.ToString(CultureInfo.InvariantCulture), item.OrdersDelivered.ToString(CultureInfo.InvariantCulture),
            Csv(item.AverageProcessingHours?.ToString("0.0", CultureInfo.InvariantCulture) ?? ""),
            Csv(item.Revenue.ToString("0.00", CultureInfo.InvariantCulture))
        ])));
        rows.Add($"Clients sans commande depuis 90 jours;{report.InactiveCustomerCount}");
        return File(new UTF8Encoding(true).GetBytes(string.Join("\r\n", rows)), "text/csv; charset=utf-8",
            $"performance-{report.From:yyyyMMdd}-{report.To:yyyyMMdd}.csv");
    }

    [HttpGet]
    public async Task<IActionResult> ExporterPdf(DateOnly? from, DateOnly? to)
    {
        var report = await BuildReportAsync(from, to);
        var lines = report.Operators.Select(item =>
            $"{item.Name} : {item.OrdersCreated} commande(s), {item.OrdersReady} prête(s), {item.OrdersDelivered} livrée(s), " +
            $"délai moyen {(item.AverageProcessingHours.HasValue ? $"{item.AverageProcessingHours.Value:N1} h" : "n/d")}, ventes {item.Revenue:N0} FCFA.").ToList();
        lines.Insert(0, $"Période : {report.From:dd/MM/yyyy} au {report.To:dd/MM/yyyy}");
        lines.Add($"Clients sans commande depuis 90 jours : {report.InactiveCustomerCount}");
        return File(ReportPdfGenerator.Generate("Rapport de performance", lines), "application/pdf",
            $"performance-{report.From:yyyyMMdd}-{report.To:yyyyMMdd}.pdf");
    }

    private async Task<PerformanceReportViewModel> BuildReportAsync(DateOnly? from, DateOnly? to)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var startDate = from ?? new DateOnly(today.Year, today.Month, 1);
        var endDate = to ?? today;
        if (endDate < startDate) (startDate, endDate) = (endDate, startDate);
        if (endDate.DayNumber - startDate.DayNumber > 366) startDate = endDate.AddDays(-366);
        var start = ToUtc(startDate);
        var end = ToUtc(endDate.AddDays(1));
        var operatorIds = (await users.GetUsersInRoleAsync(Roles.Operator)).Select(user => user.Id).ToHashSet();
        var orders = await database.LaundryOrders.AsNoTracking()
            .Where(order => operatorIds.Contains(order.CreatedByUserId) && order.CreatedAtUtc >= start && order.CreatedAtUtc < end)
            .Include(order => order.CreatedByUser).Include(order => order.StatusHistory)
            .Select(order => new
            {
                order.CreatedByUserId, Name = order.CreatedByUser.FullName, order.TotalAmount, order.Status, order.CreatedAtUtc,
                ReadyAt = order.StatusHistory.Where(history => history.NewStatus == OrderStatus.Ready)
                    .OrderBy(history => history.ChangedAtUtc).Select(history => (DateTime?)history.ChangedAtUtc).FirstOrDefault()
            }).ToListAsync();

        var operatorsReport = orders.GroupBy(order => new { order.CreatedByUserId, order.Name })
            .Select(group =>
            {
                var durations = group.Where(item => item.ReadyAt.HasValue)
                    .Select(item => (item.ReadyAt!.Value - item.CreatedAtUtc).TotalHours).ToArray();
                return new OperatorPerformanceViewModel
                {
                    Name = group.Key.Name, OrdersCreated = group.Count(),
                    OrdersReady = group.Count(item => item.ReadyAt.HasValue),
                    OrdersDelivered = group.Count(item => item.Status == OrderStatus.Delivered),
                    Revenue = group.Sum(item => item.TotalAmount),
                    AverageProcessingHours = durations.Length == 0 ? null : durations.Average()
                };
            }).OrderByDescending(item => item.OrdersCreated).ToList();
        var inactiveSince = DateTime.UtcNow.AddDays(-90);
        var inactiveCount = await database.Customers.CountAsync(customer => !customer.Orders.Any(order => order.CreatedAtUtc >= inactiveSince));
        return new PerformanceReportViewModel
        {
            From = startDate, To = endDate, Operators = operatorsReport, InactiveCustomerCount = inactiveCount
        };
    }

    private static DateTime ToUtc(DateOnly date) => TimeZoneInfo.ConvertTimeToUtc(
        DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified), TimeZoneInfo.Local);
    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}
