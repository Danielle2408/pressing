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
[Route("Comptoir/Stocks/[action]")]
public sealed class StocksController(ApplicationDbContext database) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var model = new StockViewModel
        {
            Items = await database.InventoryItems.AsNoTracking().OrderBy(item => item.Name).ToListAsync(),
            Movements = await database.StockMovements.AsNoTracking().Include(item => item.InventoryItem)
                .Include(item => item.User).OrderByDescending(item => item.CreatedAtUtc).Take(100).ToListAsync()
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ajouter(CreateInventoryItemViewModel model)
    {
        if (!User.IsInRole(Roles.Administrator)) return Forbid();
        var name = model.Name?.Trim() ?? string.Empty;
        if (model.InitialQuantity != decimal.Round(model.InitialQuantity, 3) ||
            model.AlertThreshold != decimal.Round(model.AlertThreshold, 3))
            ModelState.AddModelError(string.Empty, "Les quantités ne peuvent pas dépasser trois décimales.");
        if (await database.InventoryItems.AnyAsync(item => item.Name.ToLower() == name.ToLower()))
            ModelState.AddModelError(nameof(model.Name), "Un produit portant ce nom existe déjà.");
        if (!ModelState.IsValid)
        {
            TempData["StockError"] = "Vérifie les informations du produit et réessaie.";
            return RedirectToAction(nameof(Index));
        }
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Challenge();
        await using var transaction = await database.Database.BeginTransactionAsync();
        var item = new InventoryItem
        {
            Name = name, Unit = model.Unit.Trim(), QuantityOnHand = model.InitialQuantity,
            AlertThreshold = model.AlertThreshold, Supplier = CleanOptional(model.Supplier), UpdatedAtUtc = DateTime.UtcNow
        };
        database.InventoryItems.Add(item);
        await database.SaveChangesAsync();
        if (model.InitialQuantity > 0)
        {
            database.StockMovements.Add(new StockMovement
            {
                InventoryItemId = item.Id, Direction = StockMovementDirection.Incoming,
                Quantity = model.InitialQuantity, Note = "Stock initial", UserId = userId
            });
            await database.SaveChangesAsync();
        }
        await transaction.CommitAsync();
        TempData["StockSuccess"] = "Le produit a été ajouté à l’inventaire.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Mouvement(RecordStockMovementViewModel model)
    {
        if (!Enum.IsDefined(model.Direction) || !ModelState.IsValid)
        {
            TempData["StockError"] = "Vérifie le type de mouvement et la quantité.";
            return RedirectToAction(nameof(Index));
        }
        if (model.Quantity != decimal.Round(model.Quantity, 3))
        {
            TempData["StockError"] = "La quantité ne peut pas avoir plus de trois décimales.";
            return RedirectToAction(nameof(Index));
        }
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Challenge();
        await using var transaction = await database.Database.BeginTransactionAsync();
        var item = await database.InventoryItems.FromSqlInterpolated(
            $"SELECT * FROM \"InventoryItems\" WHERE \"Id\" = {model.InventoryItemId} FOR UPDATE").SingleOrDefaultAsync();
        if (item is null || !item.IsActive)
        {
            TempData["StockError"] = "Choisis un produit actif.";
            return RedirectToAction(nameof(Index));
        }
        if (model.Direction == StockMovementDirection.Outgoing && model.Quantity > item.QuantityOnHand)
        {
            TempData["StockError"] = $"Stock insuffisant : {item.QuantityOnHand:N3} {item.Unit} disponible(s).";
            return RedirectToAction(nameof(Index));
        }
        item.QuantityOnHand += model.Direction == StockMovementDirection.Incoming ? model.Quantity : -model.Quantity;
        item.UpdatedAtUtc = DateTime.UtcNow;
        database.StockMovements.Add(new StockMovement
        {
            InventoryItemId = item.Id, Direction = model.Direction,
            Quantity = decimal.Round(model.Quantity, 3), Note = CleanOptional(model.Note), UserId = userId
        });
        await database.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["StockSuccess"] = "Le mouvement de stock a été enregistré.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BasculerActivation(int id)
    {
        if (!User.IsInRole(Roles.Administrator)) return Forbid();
        var item = await database.InventoryItems.FindAsync(id);
        if (item is null) return NotFound();
        item.IsActive = !item.IsActive;
        item.UpdatedAtUtc = DateTime.UtcNow;
        await database.SaveChangesAsync();
        TempData["StockSuccess"] = item.IsActive ? "Produit réactivé." : "Produit désactivé.";
        return RedirectToAction(nameof(Index));
    }

    private static string? CleanOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
