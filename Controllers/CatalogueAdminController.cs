using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Pressing.Data;
using Pressing.Models;
using Pressing.Security;
using Pressing.ViewModels;

namespace Pressing.Controllers;

[Authorize(Policy = Roles.Administrator)]
[Route("Administration/Catalogue/[action]")]
public sealed class CatalogueAdminController(ApplicationDbContext database) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        await LoadOptionsAsync();
        var model = new AdminCatalogueViewModel
        {
            Categories = await database.GarmentCategories.AsNoTracking().OrderBy(item => item.Name).ToListAsync(),
            Services = await database.LaundryServices.AsNoTracking().OrderBy(item => item.Name).ToListAsync(),
            Tariffs = await database.ServiceTariffs.AsNoTracking()
                .Include(item => item.GarmentCategory)
                .Include(item => item.LaundryService)
                .OrderByDescending(item => item.ValidFromUtc)
                .Take(100)
                .ToListAsync()
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AjouterCategorie(string name)
    {
        var label = NormalizeLabel(name);
        if (label is null)
            TempData["CatalogueError"] = "Saisis un nom de catégorie de 2 à 100 caractères.";
        else if (await database.GarmentCategories.AnyAsync(item => item.NormalizedName == NormalizeKey(label)))
            TempData["CatalogueError"] = "Cette catégorie existe déjà.";
        else
        {
            database.GarmentCategories.Add(new GarmentCategory { Name = label, NormalizedName = NormalizeKey(label) });
            await database.SaveChangesAsync();
            TempData["CatalogueSuccess"] = "La catégorie a été ajoutée.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AjouterService(string name)
    {
        var label = NormalizeLabel(name);
        if (label is null)
            TempData["CatalogueError"] = "Saisis un nom de service de 2 à 100 caractères.";
        else if (await database.LaundryServices.AnyAsync(item => item.NormalizedName == NormalizeKey(label)))
            TempData["CatalogueError"] = "Ce service existe déjà.";
        else
        {
            database.LaundryServices.Add(new LaundryService { Name = label, NormalizedName = NormalizeKey(label) });
            await database.SaveChangesAsync();
            TempData["CatalogueSuccess"] = "Le service a été ajouté.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ModifierTarif(int categoryId, int serviceId, decimal price)
    {
        if (price <= 0 || price > 100_000_000)
        {
            TempData["CatalogueError"] = "Le tarif doit être supérieur à 0 FCFA.";
            return RedirectToAction(nameof(Index));
        }

        var category = await database.GarmentCategories.FindAsync(categoryId);
        var service = await database.LaundryServices.FindAsync(serviceId);
        if (category is null || !category.IsActive || service is null || !service.IsActive)
        {
            TempData["CatalogueError"] = "Choisis une catégorie et un service actifs.";
            return RedirectToAction(nameof(Index));
        }

        await using var transaction = await database.Database.BeginTransactionAsync();
        var effectiveAt = DateTime.UtcNow;
        var current = await database.ServiceTariffs
            .Where(item => item.GarmentCategoryId == categoryId && item.LaundryServiceId == serviceId &&
                           item.ValidFromUtc <= effectiveAt &&
                           (item.ValidUntilUtc == null || item.ValidUntilUtc > effectiveAt))
            .OrderByDescending(item => item.ValidFromUtc)
            .FirstOrDefaultAsync();

        if (current is not null && current.Price == price)
        {
            await transaction.RollbackAsync();
            TempData["CatalogueError"] = "Ce tarif est déjà en vigueur.";
            return RedirectToAction(nameof(Index));
        }

        if (current is not null)
        {
            effectiveAt = effectiveAt <= current.ValidFromUtc
                ? current.ValidFromUtc.AddTicks(1)
                : effectiveAt;
            current.ValidUntilUtc = effectiveAt;
        }

        database.ServiceTariffs.Add(new ServiceTariff
        {
            GarmentCategoryId = categoryId,
            LaundryServiceId = serviceId,
            Price = decimal.Round(price, 2),
            ValidFromUtc = effectiveAt
        });
        await database.SaveChangesAsync();
        await transaction.CommitAsync();

        TempData["CatalogueSuccess"] = "Le nouveau tarif est enregistré. L’ancien tarif reste dans l’historique.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BasculerCategorie(int id)
    {
        var category = await database.GarmentCategories.FindAsync(id);
        if (category is null)
            return NotFound();

        category.IsActive = !category.IsActive;
        await database.SaveChangesAsync();
        TempData["CatalogueSuccess"] = category.IsActive ? "Catégorie réactivée." : "Catégorie désactivée.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BasculerService(int id)
    {
        var service = await database.LaundryServices.FindAsync(id);
        if (service is null)
            return NotFound();

        service.IsActive = !service.IsActive;
        await database.SaveChangesAsync();
        TempData["CatalogueSuccess"] = service.IsActive ? "Service réactivé." : "Service désactivé.";
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadOptionsAsync()
    {
        ViewBag.CategoryOptions = new SelectList(
            await database.GarmentCategories.AsNoTracking().Where(item => item.IsActive)
                .OrderBy(item => item.Name).ToListAsync(), "Id", "Name");
        ViewBag.ServiceOptions = new SelectList(
            await database.LaundryServices.AsNoTracking().Where(item => item.IsActive)
                .OrderBy(item => item.Name).ToListAsync(), "Id", "Name");
    }

    private static string? NormalizeLabel(string? value)
    {
        var label = value?.Trim();
        return label is { Length: >= 2 and <= 100 } ? label : null;
    }

    private static string NormalizeKey(string value) => value.Trim().Normalize().ToUpperInvariant();
}
