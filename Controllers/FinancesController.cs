using System.Security.Claims;
using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pressing.Data;
using Pressing.Models;
using Pressing.Security;
using Pressing.ViewModels;
using Pressing.Services;

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

    [HttpGet]
    public async Task<IActionResult> ExporterExcel(DateOnly? from, DateOnly? to)
    {
        var (startDate, endDate) = NormalizePeriod(from, to);
        var start = ToUtc(startDate);
        var end = ToUtc(endDate.AddDays(1));
        var payments = await database.OrderPayments.AsNoTracking()
            .Include(item => item.LaundryOrder).ThenInclude(order => order.Customer)
            .Where(item => item.Status == OrderPaymentStatus.Confirmed && item.CreatedAtUtc >= start && item.CreatedAtUtc < end)
            .OrderBy(item => item.CreatedAtUtc).ToListAsync();
        var expenses = await database.Expenses.AsNoTracking()
            .Where(item => item.SpentAtUtc >= start && item.SpentAtUtc < end)
            .OrderBy(item => item.SpentAtUtc).ToListAsync();
        var rows = new List<string>
        {
            "Type;Date;Reference;Commande;Client ou libelle;Categorie ou moyen;Montant FCFA"
        };
        rows.AddRange(payments.Select(payment => string.Join(';', [
            Csv("Recette"), Csv(payment.CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)),
            Csv(payment.ReceiptNumber ?? payment.TransactionReference ?? string.Empty), Csv(payment.LaundryOrder.Number),
            Csv(payment.LaundryOrder.Customer.FullName), Csv(payment.Method.ToFrench()), Csv(payment.Amount.ToString("0.00", CultureInfo.GetCultureInfo("fr-FR")))
        ])));
        rows.AddRange(expenses.Select(expense => string.Join(';', [
            Csv("Depense"), Csv(expense.SpentAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)),
            Csv(string.Empty), Csv(string.Empty), Csv(expense.Label), Csv(expense.Category), Csv(expense.Amount.ToString("0.00", CultureInfo.GetCultureInfo("fr-FR")))
        ])));
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        var bytes = encoding.GetBytes(string.Join("\r\n", rows));
        return File(bytes, "text/csv; charset=utf-8", $"rapport-financier-{startDate:yyyyMMdd}-{endDate:yyyyMMdd}.csv");
    }

    [HttpGet]
    public async Task<IActionResult> ExporterPdf(DateOnly? from, DateOnly? to)
    {
        var (startDate, endDate) = NormalizePeriod(from, to);
        var start = ToUtc(startDate);
        var end = ToUtc(endDate.AddDays(1));
        var payments = await database.OrderPayments.AsNoTracking().Where(item =>
            item.Status == OrderPaymentStatus.Confirmed && item.CreatedAtUtc >= start && item.CreatedAtUtc < end).ToListAsync();
        var expenses = await database.Expenses.AsNoTracking().Where(item => item.SpentAtUtc >= start && item.SpentAtUtc < end).ToListAsync();
        var lines = new List<string>
        {
            $"Periode : {startDate:dd/MM/yyyy} au {endDate:dd/MM/yyyy}",
            $"Recettes confirmees : {payments.Sum(item => item.Amount):N0} FCFA",
            $"Depenses enregistrees : {expenses.Sum(item => item.Amount):N0} FCFA",
            $"Recettes moins depenses : {payments.Sum(item => item.Amount) - expenses.Sum(item => item.Amount):N0} FCFA",
            "", "RECETTES PAR MOYEN DE PAIEMENT"
        };
        lines.AddRange(payments.GroupBy(item => item.Method)
            .OrderBy(group => group.Key)
            .Select(group => $"{group.Key.ToFrench()} : {group.Sum(item => item.Amount):N0} FCFA ({group.Count()} paiement(s))"));
        lines.AddRange(["", "DEPENSES PAR CATEGORIE"]);
        lines.AddRange(expenses.GroupBy(item => item.Category).OrderBy(group => group.Key)
            .Select(group => $"{group.Key} : {group.Sum(item => item.Amount):N0} FCFA ({group.Count()} depense(s))"));
        var pdf = ReportPdfGenerator.Generate("Rapport financier du pressing", lines);
        return File(pdf, "application/pdf", $"rapport-financier-{startDate:yyyyMMdd}-{endDate:yyyyMMdd}.pdf");
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

    private static (DateOnly Start, DateOnly End) NormalizePeriod(DateOnly? from, DateOnly? to)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var start = from ?? new DateOnly(today.Year, today.Month, 1);
        var end = to ?? today;
        if (end < start) (start, end) = (end, start);
        if (end.DayNumber - start.DayNumber > 366) start = end.AddDays(-366);
        return (start, end);
    }

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}
