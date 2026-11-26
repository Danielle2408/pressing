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
[Route("Comptoir/Stocks/[action]")]
public sealed class StocksController(ApplicationDbContext database, AuditLogService auditLog) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var model = new StockViewModel
        {
            Items = await database.InventoryItems.AsNoTracking().Include(item => item.SupplierRecord).OrderBy(item => item.Name).ToListAsync(),
            Suppliers = await database.Suppliers.AsNoTracking().OrderBy(item => item.Name).ToListAsync(),
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
        var supplier = model.SupplierId.HasValue
            ? await database.Suppliers.FirstOrDefaultAsync(item => item.Id == model.SupplierId && item.IsActive)
            : null;
        if (model.SupplierId.HasValue && supplier is null)
            ModelState.AddModelError(nameof(model.SupplierId), "Choisis un fournisseur actif.");
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
            AlertThreshold = model.AlertThreshold, SupplierId = supplier?.Id, Supplier = supplier?.Name, UpdatedAtUtc = DateTime.UtcNow
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
        auditLog.Record(userId, "Création", "Stock", item.Id, $"Produit {item.Name}, quantité initiale {item.QuantityOnHand:N3} {item.Unit}.");
        await database.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["StockSuccess"] = "Le produit a été ajouté à l’inventaire.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AjouterFournisseur(CreateSupplierViewModel model)
    {
        if (!User.IsInRole(Roles.Administrator)) return Forbid();
        var name = model.Name.Trim();
        if (await database.Suppliers.AnyAsync(item => item.Name.ToLower() == name.ToLower()))
            ModelState.AddModelError(nameof(model.Name), "Un fournisseur portant ce nom existe déjà.");
        if (!ModelState.IsValid)
        {
            TempData["StockError"] = "Vérifie les informations du fournisseur et réessaie.";
            return RedirectToAction(nameof(Index));
        }
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();
        var supplier = new Supplier
        {
            Name = name, ContactName = CleanOptional(model.ContactName), PhoneNumber = CleanOptional(model.PhoneNumber),
            Email = CleanOptional(model.Email), Address = CleanOptional(model.Address), CreatedAtUtc = DateTime.UtcNow
        };
        database.Suppliers.Add(supplier);
        await database.SaveChangesAsync();
        auditLog.Record(userId, "Création", "Fournisseur", supplier.Id, supplier.Name);
        await database.SaveChangesAsync();
        TempData["StockSuccess"] = "Le fournisseur a été ajouté.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BasculerFournisseur(int id)
    {
        if (!User.IsInRole(Roles.Administrator)) return Forbid();
        var supplier = await database.Suppliers.FindAsync(id);
        if (supplier is null) return NotFound();
        supplier.IsActive = !supplier.IsActive;
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();
        auditLog.Record(userId, supplier.IsActive ? "Réactivation" : "Désactivation", "Fournisseur", supplier.Id, supplier.Name);
        await database.SaveChangesAsync();
        TempData["StockSuccess"] = supplier.IsActive ? "Fournisseur réactivé." : "Fournisseur désactivé.";
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
        auditLog.Record(userId, model.Direction == StockMovementDirection.Incoming ? "Entrée de stock" : "Sortie de stock",
            "Stock", item.Id, $"{model.Quantity:N3} {item.Unit} pour {item.Name}.");
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
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();
        auditLog.Record(userId, item.IsActive ? "Désactivation" : "Réactivation", "Stock", item.Id, item.Name);
        await database.SaveChangesAsync();
        TempData["StockSuccess"] = item.IsActive ? "Produit réactivé." : "Produit désactivé.";
        return RedirectToAction(nameof(Index));
    }

    private static string? CleanOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
