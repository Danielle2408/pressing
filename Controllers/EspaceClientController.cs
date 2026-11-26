using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pressing.Data;
using Pressing.Security;
using Pressing.ViewModels;

namespace Pressing.Controllers;

[Authorize(Policy = Roles.Customer)]
[Route("EspaceClient/[action]")]
public sealed class EspaceClientController(ApplicationDbContext database) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var customer = await database.Customers.AsNoTracking()
            .FirstOrDefaultAsync(item => item.ApplicationUserId == userId);
        if (customer is null)
            return NotFound();

        var orders = await database.LaundryOrders.AsNoTracking()
            .Where(order => order.CustomerId == customer.Id)
            .OrderByDescending(order => order.CreatedAtUtc)
            .Take(100)
            .ToListAsync();

        return View(new CustomerPortalViewModel { Customer = customer, Orders = orders });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var order = await database.LaundryOrders.AsNoTracking()
            .Include(item => item.Customer)
            .Include(item => item.Lines)
            .Include(item => item.StatusHistory)
            .Include(item => item.Payments)
            .Where(item => item.Id == id && item.Customer.ApplicationUserId == userId)
            .FirstOrDefaultAsync();

        return order is null ? NotFound() : View(new CustomerOrderDetailsViewModel { Order = order });
    }
}
