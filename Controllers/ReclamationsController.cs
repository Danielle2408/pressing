using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pressing.Data;
using Pressing.Models;
using Pressing.Security;
using Pressing.ViewModels;

namespace Pressing.Controllers;

[Authorize(Roles = Roles.Administrator + "," + Roles.Operator)]
[Route("Comptoir/Reclamations/[action]")]
public sealed class ReclamationsController(ApplicationDbContext database) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(ClaimStatus? status)
    {
        var claims = database.CustomerClaims.AsNoTracking().Include(item => item.Customer)
            .Include(item => item.LaundryOrder).Include(item => item.CreatedByUser)
            .Include(item => item.ResolvedByUser).AsQueryable();
        if (status.HasValue) claims = claims.Where(item => item.Status == status.Value);
        return View(new CustomerClaimsListViewModel
        {
            Claims = await claims.OrderBy(item => item.Status == ClaimStatus.Open || item.Status == ClaimStatus.UnderReview ? 0 : 1)
                .ThenByDescending(item => item.CreatedAtUtc).Take(200).ToListAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Traiter(int id, ClaimStatus status, string? decision)
    {
        if (!Enum.IsDefined(status)) return BadRequest();
        if (!User.IsInRole(Roles.Administrator) && status != ClaimStatus.UnderReview) return Forbid();
        var claim = await database.CustomerClaims.FirstOrDefaultAsync(item => item.Id == id);
        if (claim is null) return NotFound();
        if (claim.Status is ClaimStatus.Resolved or ClaimStatus.Rejected)
        {
            TempData["ClaimError"] = "Cette réclamation est déjà clôturée.";
            return RedirectToAction(nameof(Index));
        }
        var finalDecision = status is ClaimStatus.Resolved or ClaimStatus.Rejected;
        var cleanDecision = string.IsNullOrWhiteSpace(decision) ? null : decision.Trim();
        if (cleanDecision?.Length > 2000 || (finalDecision && string.IsNullOrWhiteSpace(cleanDecision)))
        {
            TempData["ClaimError"] = "Une décision de moins de 2 000 caractères est requise pour clôturer la réclamation.";
            return RedirectToAction(nameof(Index));
        }
        claim.Status = status;
        claim.Decision = cleanDecision;
        if (finalDecision)
        {
            claim.ResolvedAtUtc = DateTime.UtcNow;
            claim.ResolvedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        }
        await database.SaveChangesAsync();
        TempData["ClaimSuccess"] = finalDecision ? "La décision a été enregistrée." : "La réclamation est maintenant en cours d’examen.";
        return RedirectToAction(nameof(Index));
    }
}
