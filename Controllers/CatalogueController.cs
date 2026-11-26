using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pressing.Data;
using Pressing.ViewModels;

namespace Pressing.Controllers;

public sealed class CatalogueController(ApplicationDbContext database) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var now = DateTime.UtcNow;
        var rates = await database.ServiceTariffs
            .AsNoTracking()
            .Where(tariff =>
                tariff.GarmentCategory.IsActive &&
                tariff.LaundryService.IsActive &&
                tariff.ValidFromUtc <= now &&
                (tariff.ValidUntilUtc == null || tariff.ValidUntilUtc > now))
            .OrderBy(tariff => tariff.GarmentCategory.Name)
            .ThenBy(tariff => tariff.LaundryService.Name)
            .Select(tariff => new CatalogueRateViewModel
            {
                Category = tariff.GarmentCategory.Name,
                Service = tariff.LaundryService.Name,
                Price = tariff.Price
            })
            .ToListAsync();

        return View(new CatalogueViewModel { Rates = rates });
    }
}
