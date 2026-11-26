using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pressing.Data;
using Pressing.Models;
using Pressing.Security;
using Pressing.ViewModels;
using Pressing.Services;

namespace Pressing.Controllers;

[Authorize(Roles = Roles.Administrator + "," + Roles.Operator)]
[Route("Comptoir/Caisse/[action]")]
public sealed class CaisseController(ApplicationDbContext database, AuditLogService auditLog) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Challenge();
        var date = DateOnly.FromDateTime(DateTime.Now);
        var (start, end) = DayBoundsUtc(date);
        var confirmed = database.OrderPayments.AsNoTracking().Where(item => item.CreatedByUserId == userId &&
            item.Status == OrderPaymentStatus.Confirmed && item.CreatedAtUtc >= start && item.CreatedAtUtc < end);
        var expectedCash = await confirmed.Where(item => item.Method == PaymentMethod.Cash).SumAsync(item => (decimal?)item.Amount) ?? 0m;
        var otherPayments = await confirmed.Where(item => item.Method != PaymentMethod.Cash).SumAsync(item => (decimal?)item.Amount) ?? 0m;
        var count = await confirmed.CountAsync();
        var closure = await database.CashClosures.AsNoTracking()
            .FirstOrDefaultAsync(item => item.OperatorId == userId && item.BusinessDate == date);
        return View(new CashClosureViewModel
        {
            BusinessDate = date, ExpectedCash = expectedCash, OtherPayments = otherPayments,
            PaymentCount = count, Closure = closure, CountedCash = closure?.CountedCash ?? expectedCash,
            Note = closure?.Note
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cloturer(CashClosureViewModel model)
    {
        if (model.CountedCash < 0)
        {
            TempData["CashError"] = "Le montant compté ne peut pas être négatif.";
            return RedirectToAction(nameof(Index));
        }
        if (model.Note?.Length > 500)
        {
            TempData["CashError"] = "La note ne peut pas dépasser 500 caractères.";
            return RedirectToAction(nameof(Index));
        }
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Challenge();
        var date = DateOnly.FromDateTime(DateTime.Now);
        if (await database.CashClosures.AnyAsync(item => item.OperatorId == userId && item.BusinessDate == date))
        {
            TempData["CashError"] = "La caisse est déjà clôturée pour aujourd’hui.";
            return RedirectToAction(nameof(Index));
        }
        var (start, end) = DayBoundsUtc(date);
        var confirmed = database.OrderPayments.AsNoTracking().Where(item => item.CreatedByUserId == userId &&
            item.Status == OrderPaymentStatus.Confirmed && item.CreatedAtUtc >= start && item.CreatedAtUtc < end);
        var expectedCash = await confirmed.Where(item => item.Method == PaymentMethod.Cash).SumAsync(item => (decimal?)item.Amount) ?? 0m;
        var otherPayments = await confirmed.Where(item => item.Method != PaymentMethod.Cash).SumAsync(item => (decimal?)item.Amount) ?? 0m;
        var closure = new CashClosure
        {
            OperatorId = userId, BusinessDate = date, ExpectedCash = expectedCash,
            CountedCash = decimal.Round(model.CountedCash, 2), OtherPayments = otherPayments,
            PaymentCount = await confirmed.CountAsync(),
            Note = string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim(), ClosedAtUtc = DateTime.UtcNow
        };
        database.CashClosures.Add(closure);
        try
        {
            await database.SaveChangesAsync();
            auditLog.Record(userId, "Clôture", "Caisse", closure.Id,
                $"Espèces attendues {closure.ExpectedCash:N0} FCFA, comptées {closure.CountedCash:N0} FCFA, écart {closure.Variance:N0} FCFA.");
            await database.SaveChangesAsync();
            TempData["CashSuccess"] = $"Caisse clôturée. Écart constaté : {closure.Variance:N0} FCFA.";
        }
        catch (DbUpdateException)
        {
            TempData["CashError"] = "Une clôture existe déjà pour aujourd’hui.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Historique()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var query = database.CashClosures.AsNoTracking().Include(item => item.Operator).AsQueryable();
        if (!User.IsInRole(Roles.Administrator)) query = query.Where(item => item.OperatorId == userId);
        var closures = await query.OrderByDescending(item => item.BusinessDate).ThenBy(item => item.Operator.FullName)
            .Take(120).ToListAsync();
        return View(new CashClosureHistoryViewModel { Closures = closures });
    }

    private static (DateTime Start, DateTime End) DayBoundsUtc(DateOnly date)
    {
        var startLocal = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        var endLocal = DateTime.SpecifyKind(date.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        return (TimeZoneInfo.ConvertTimeToUtc(startLocal, TimeZoneInfo.Local), TimeZoneInfo.ConvertTimeToUtc(endLocal, TimeZoneInfo.Local));
    }
}
