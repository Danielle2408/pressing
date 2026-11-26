using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pressing.Data;
using Pressing.Models;
using Pressing.Security;

namespace Pressing.Controllers;

[Authorize(Policy = Roles.Administrator)]
[Route("Administration/Journal/[action]")]
public sealed class AuditController(ApplicationDbContext database) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(DateOnly? date, string? entity)
    {
        var query = database.AuditLogs.AsNoTracking().Include(item => item.User).AsQueryable();
        if (date.HasValue)
        {
            var start = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(date.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified), TimeZoneInfo.Local);
            var end = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(date.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified), TimeZoneInfo.Local);
            query = query.Where(item => item.CreatedAtUtc >= start && item.CreatedAtUtc < end);
        }
        var trimmed = entity?.Trim();
        if (!string.IsNullOrEmpty(trimmed)) query = query.Where(item => item.Entity.Contains(trimmed));
        ViewBag.FilterDate = date?.ToString("yyyy-MM-dd");
        ViewBag.FilterEntity = trimmed;
        return View(await query.OrderByDescending(item => item.CreatedAtUtc).Take(300).ToListAsync());
    }
}
