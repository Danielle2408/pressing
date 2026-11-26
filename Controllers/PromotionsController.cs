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

[Authorize(Policy = Roles.Administrator)]
[Route("Administration/Promotions/[action]")]
public sealed class PromotionsController(ApplicationDbContext database, AuditLogService auditLog) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index() => View(new PromotionListViewModel
    {
        Promotions = await database.Promotions.AsNoTracking().Include(item => item.GarmentCategory)
            .OrderByDescending(item => item.StartsAtUtc).Take(200).ToListAsync(),
        Categories = await database.GarmentCategories.AsNoTracking().OrderBy(item => item.Name).ToListAsync()
    });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ajouter(CreatePromotionViewModel model)
    {
        var normalized = model.Code?.Trim().ToUpperInvariant() ?? string.Empty;
        if (model.Kind == PromotionKind.Percentage && model.Value > 100)
            ModelState.AddModelError(nameof(model.Value), "Un pourcentage doit être compris entre 0 et 100.");
        if (model.EndsAtLocal <= model.StartsAtLocal)
            ModelState.AddModelError(nameof(model.EndsAtLocal), "La fin doit être après le début.");
        if (await database.Promotions.AnyAsync(item => item.NormalizedCode == normalized))
            ModelState.AddModelError(nameof(model.Code), "Ce code promotionnel existe déjà.");
        if (model.GarmentCategoryId.HasValue && !await database.GarmentCategories
                .AnyAsync(item => item.Id == model.GarmentCategoryId && item.IsActive))
            ModelState.AddModelError(nameof(model.GarmentCategoryId), "Choisis une catégorie active.");
        if (!ModelState.IsValid)
        {
            TempData["PromotionError"] = "Vérifie les champs de la promotion.";
            return RedirectToAction(nameof(Index));
        }
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();
        var promotion = new Promotion
        {
            Code = normalized, NormalizedCode = normalized,
            Description = model.Description.Trim(), Kind = model.Kind, Value = decimal.Round(model.Value, 2),
            GarmentCategoryId = model.GarmentCategoryId,
            StartsAtUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(model.StartsAtLocal, DateTimeKind.Unspecified), TimeZoneInfo.Local),
            EndsAtUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(model.EndsAtLocal, DateTimeKind.Unspecified), TimeZoneInfo.Local),
            CreatedByUserId = userId
        };
        database.Promotions.Add(promotion);
        await database.SaveChangesAsync();
        auditLog.Record(userId, "Création", "Promotion", promotion.Id, $"Code {promotion.Code} créé.");
        await database.SaveChangesAsync();
        TempData["PromotionSuccess"] = "La promotion a été créée.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BasculerActivation(int id)
    {
        var promotion = await database.Promotions.FindAsync(id);
        if (promotion is null) return NotFound();
        promotion.IsActive = !promotion.IsActive;
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();
        auditLog.Record(userId, promotion.IsActive ? "Réactivation" : "Désactivation", "Promotion", promotion.Id, promotion.Code);
        await database.SaveChangesAsync();
        TempData["PromotionSuccess"] = promotion.IsActive ? "Code réactivé." : "Code désactivé.";
        return RedirectToAction(nameof(Index));
    }
}
