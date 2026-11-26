using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pressing.Data;
using Pressing.Models;
using Pressing.Security;
using Pressing.ViewModels;

namespace Pressing.Controllers;

[Authorize(Policy = Roles.Administrator)]
[Route("Administration/Finances/[action]")]
public sealed class FinancesController(ApplicationDbContext database) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(DateOnly? from, DateOnly? to)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var startDate = from ?? new DateOnly(today.Year, today.Month, 1);
        var endDate = to ?? today;
        if (endDate < startDate) (startDate, endDate) = (endDate, startDate);
        if (endDate.DayNumber - startDate.DayNumber > 366) startDate = endDate.AddDays(-366);
        var start = ToUtc(startDate);
        var end = ToUtc(endDate.AddDays(1));
        var paymentQuery = database.OrderPayments.AsNoTracking().Where(item => item.Status == OrderPaymentStatus.Confirmed &&
            item.CreatedAtUtc >= start && item.CreatedAtUtc < end);
        var methodTotals = await paymentQuery.GroupBy(item => item.Method)
            .Select(group => new PaymentMethodTotalViewModel
            {
                Method = group.Key, Amount = group.Sum(item => item.Amount), Count = group.Count()
            }).OrderBy(item => item.Method).ToListAsync();
        var expenseQuery = database.Expenses.AsNoTracking()
            .Where(item => item.SpentAtUtc >= start && item.SpentAtUtc < end);
        var expenseTotal = await expenseQuery.SumAsync(item => (decimal?)item.Amount) ?? 0m;
        var expenses = await expenseQuery.Include(item => item.CreatedByUser)
            .OrderByDescending(item => item.SpentAtUtc).Take(200).ToListAsync();
        return View(new FinanceReportViewModel
        {
            From = startDate, To = endDate,
            Revenue = methodTotals.Sum(item => item.Amount), Expenses = expenseTotal,
            Methods = methodTotals, ExpenseItems = expenses,
            NewExpense = new CreateExpenseViewModel { SpentOn = today }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AjouterDepense(CreateExpenseViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["FinanceError"] = "Vérifie le libellé, la catégorie, le montant et la date de la dépense.";
            return RedirectToAction(nameof(Index));
        }
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Challenge();
        var localDate = DateTime.SpecifyKind(model.SpentOn.ToDateTime(TimeOnly.FromDateTime(DateTime.Now)), DateTimeKind.Unspecified);
        database.Expenses.Add(new Expense
        {
            Label = model.Label.Trim(), Category = model.Category.Trim(), Amount = decimal.Round(model.Amount, 2),
            SpentAtUtc = TimeZoneInfo.ConvertTimeToUtc(localDate, TimeZoneInfo.Local), CreatedByUserId = userId
        });
        await database.SaveChangesAsync();
        TempData["FinanceSuccess"] = "La dépense a été enregistrée.";
        return RedirectToAction(nameof(Index), new { from = model.SpentOn, to = model.SpentOn });
    }

    private static DateTime ToUtc(DateOnly date) => TimeZoneInfo.ConvertTimeToUtc(
        DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified), TimeZoneInfo.Local);
}
