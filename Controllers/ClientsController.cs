using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pressing.Data;
using Pressing.Models;
using Pressing.Security;
using Pressing.ViewModels;

namespace Pressing.Controllers;

[Authorize(Roles = Roles.Administrator + "," + Roles.Operator)]
[Route("Comptoir/Clients/[action]")]
public sealed class ClientsController(ApplicationDbContext database) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? query)
    {
        var customers = database.Customers.AsNoTracking();
        var trimmed = query?.Trim();
        if (!string.IsNullOrEmpty(trimmed))
        {
            var normalizedPhone = NormalizePhone(trimmed);
            var namePart = trimmed.ToLowerInvariant();
            customers = string.IsNullOrEmpty(normalizedPhone)
                ? customers.Where(customer => customer.FullName.ToLower().Contains(namePart))
                : customers.Where(customer => customer.NormalizedPhone.Contains(normalizedPhone) ||
                                               customer.FullName.ToLower().Contains(namePart));
        }

        var result = await customers
            .OrderByDescending(customer => customer.CreatedAtUtc)
            .Take(100)
            .ToListAsync();

        return View(new CustomerSearchViewModel { Query = trimmed, Customers = result });
    }

    [HttpGet]
    public IActionResult Creer() => View(new CreateCustomerViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Creer(CreateCustomerViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var normalizedPhone = NormalizePhone(model.PhoneNumber);
        if (await database.Customers.AnyAsync(customer => customer.NormalizedPhone == normalizedPhone))
        {
            ModelState.AddModelError(nameof(model.PhoneNumber), "Un client utilise déjà ce numéro de téléphone.");
            return View(model);
        }

        var customer = new Customer
        {
            FullName = model.FullName.Trim(),
            PhoneNumber = model.PhoneNumber.Trim(),
            NormalizedPhone = normalizedPhone,
            Address = string.IsNullOrWhiteSpace(model.Address) ? null : model.Address.Trim()
        };

        database.Customers.Add(customer);
        await database.SaveChangesAsync();
        TempData["CustomerSuccess"] = "La fiche client a été créée.";
        return RedirectToAction(nameof(Details), new { id = customer.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var customer = await database.Customers.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);
        return customer is null ? NotFound() : View(new CustomerDetailsViewModel { Customer = customer });
    }

    private static string NormalizePhone(string value) => Regex.Replace(value, @"[^0-9+]", string.Empty);
}
