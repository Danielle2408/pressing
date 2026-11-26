using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pressing.Data;
using Pressing.Models;
using Pressing.Security;
using Pressing.ViewModels;
using Pressing.Services;

namespace Pressing.Controllers;

[Authorize(Policy = Roles.Administrator)]
[Route("Administration/Utilisateurs/[action]")]
public sealed class AdministrationUtilisateursController(
    UserManager<ApplicationUser> users,
    ApplicationDbContext database,
    AuditLogService auditLog) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? role)
    {
        var validRole = Roles.All.Contains(role ?? string.Empty) ? role : null;
        var all = await users.Users.AsNoTracking().OrderBy(item => item.FullName).ToListAsync();
        var rows = new List<AdminUserRowViewModel>();
        foreach (var user in all)
        {
            var roleName = (await users.GetRolesAsync(user)).FirstOrDefault();
            if (roleName is null || (validRole is not null && roleName != validRole)) continue;
            rows.Add(new AdminUserRowViewModel { User = user, Role = roleName });
        }
        return View(new AdminUserListViewModel { Role = validRole, Users = rows });
    }

    [HttpGet]
    public IActionResult Creer() => View(new CreateStaffUserViewModel());

    [HttpGet]
    public async Task<IActionResult> Modifier(string id)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();
        return View(new EditAdminUserViewModel
        {
            UserId = user.Id, FullName = user.FullName, Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber ?? string.Empty
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Modifier(EditAdminUserViewModel model)
    {
        var user = await users.FindByIdAsync(model.UserId);
        if (user is null) return NotFound();
        var email = model.Email.Trim();
        var phone = NormalizePhone(model.PhoneNumber);
        if (await users.Users.AnyAsync(item => item.Id != user.Id && item.Email == email))
            ModelState.AddModelError(nameof(model.Email), "Cette adresse e-mail est déjà utilisée.");
        if (await users.Users.AnyAsync(item => item.Id != user.Id && item.PhoneNumber == phone))
            ModelState.AddModelError(nameof(model.PhoneNumber), "Ce numéro de téléphone est déjà utilisé.");
        if (!ModelState.IsValid) return View(model);

        user.FullName = model.FullName.Trim();
        var emailResult = await users.SetEmailAsync(user, email);
        var nameResult = await users.SetUserNameAsync(user, email);
        var phoneResult = await users.SetPhoneNumberAsync(user, phone);
        if (!emailResult.Succeeded || !nameResult.Succeeded || !phoneResult.Succeeded)
        {
            foreach (var error in emailResult.Errors.Concat(nameResult.Errors).Concat(phoneResult.Errors))
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }
        var customer = await database.Customers.FirstOrDefaultAsync(item => item.ApplicationUserId == user.Id);
        if (customer is not null)
        {
            customer.FullName = user.FullName;
            customer.PhoneNumber = phone;
            customer.NormalizedPhone = phone;
        }
        await users.UpdateSecurityStampAsync(user);
        await database.SaveChangesAsync();
        auditLog.Record(User.FindFirstValue(ClaimTypes.NameIdentifier)!, "Modification", "Utilisateur", user.Id,
            "Nom, e-mail ou téléphone modifié.");
        await database.SaveChangesAsync();
        TempData["UsersSuccess"] = $"Le compte de {user.FullName} a été modifié.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Creer(CreateStaffUserViewModel model)
    {
        if (model.Role is not (Roles.Administrator or Roles.Operator))
            ModelState.AddModelError(nameof(model.Role), "Choisis le rôle Administrateur ou Opérateur.");
        var email = model.Email.Trim();
        var phone = NormalizePhone(model.PhoneNumber);
        if (await users.FindByEmailAsync(email) is not null)
            ModelState.AddModelError(nameof(model.Email), "Cette adresse e-mail est déjà utilisée.");
        if (await users.Users.AnyAsync(item => item.PhoneNumber == phone))
            ModelState.AddModelError(nameof(model.PhoneNumber), "Ce numéro de téléphone est déjà utilisé.");
        if (!ModelState.IsValid) return View(model);

        var user = new ApplicationUser
        {
            UserName = email, Email = email, EmailConfirmed = true,
            PhoneNumber = phone, PhoneNumberConfirmed = true, FullName = model.FullName.Trim()
        };
        await using var transaction = await database.Database.BeginTransactionAsync();
        var created = await users.CreateAsync(user, model.TemporaryPassword);
        if (!created.Succeeded)
        {
            foreach (var error in created.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }
        var assigned = await users.AddToRoleAsync(user, model.Role);
        if (!assigned.Succeeded)
        {
            await transaction.RollbackAsync();
            foreach (var error in assigned.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }
        auditLog.Record(User.FindFirstValue(ClaimTypes.NameIdentifier)!, "Création", "Utilisateur", user.Id,
            $"Compte créé avec le rôle {model.Role}.");
        await database.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["UsersSuccess"] = $"Le compte de {user.FullName} a été créé. Transmets-lui son mot de passe temporaire de manière sécurisée.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BasculerActivation(string id)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();
        if (User.FindFirstValue(ClaimTypes.NameIdentifier) == id)
        {
            TempData["UsersError"] = "Tu ne peux pas désactiver ton propre compte.";
            return RedirectToAction(nameof(Index));
        }
        var roles = await users.GetRolesAsync(user);
        if (user.IsActive && roles.Contains(Roles.Administrator))
        {
            var activeAdministrators = await users.GetUsersInRoleAsync(Roles.Administrator);
            if (activeAdministrators.Count(item => item.IsActive) <= 1)
            {
                TempData["UsersError"] = "Le dernier compte administrateur actif ne peut pas être désactivé.";
                return RedirectToAction(nameof(Index));
            }
        }
        user.IsActive = !user.IsActive;
        var result = await users.UpdateAsync(user);
        if (result.Succeeded)
        {
            await users.UpdateSecurityStampAsync(user);
            auditLog.Record(User.FindFirstValue(ClaimTypes.NameIdentifier)!, user.IsActive ? "Réactivation" : "Désactivation", "Utilisateur", user.Id);
            await database.SaveChangesAsync();
        }
        if (!result.Succeeded)
            TempData["UsersError"] = "Le changement d’état du compte n’a pas pu être enregistré.";
        else
            TempData["UsersSuccess"] = user.IsActive ? "Compte réactivé." : "Compte désactivé.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> ReinitialiserMotDePasse(string id)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();
        if (!await users.IsInRoleAsync(user, Roles.Administrator) && !await users.IsInRoleAsync(user, Roles.Operator))
            return NotFound();
        return View(new ResetStaffPasswordViewModel { UserId = user.Id, FullName = user.FullName });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReinitialiserMotDePasse(ResetStaffPasswordViewModel model)
    {
        var user = await users.FindByIdAsync(model.UserId);
        if (user is null) return NotFound();
        if (!ModelState.IsValid) { model.FullName = user.FullName; return View(model); }
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var result = await users.ResetPasswordAsync(user, token, model.TemporaryPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            model.FullName = user.FullName;
            return View(model);
        }
        auditLog.Record(User.FindFirstValue(ClaimTypes.NameIdentifier)!, "Réinitialisation du mot de passe", "Utilisateur", user.Id);
        await database.SaveChangesAsync();
        TempData["UsersSuccess"] = $"Le mot de passe de {user.FullName} a été réinitialisé. Transmets-le-lui de manière sécurisée.";
        return RedirectToAction(nameof(Index));
    }

    private static string NormalizePhone(string value) => Regex.Replace(value, @"[^0-9+]", string.Empty);
}
