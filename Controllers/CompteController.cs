using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pressing.Data;
using Pressing.Models;
using Pressing.Security;
using Pressing.ViewModels;

namespace Pressing.Controllers;

public sealed class CompteController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    RoleManager<IdentityRole> roleManager,
    ApplicationDbContext database) : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Connexion(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Connexion(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var identifier = model.Identifier.Trim();
        var normalizedPhone = NormalizePhone(identifier);
        var user = await userManager.FindByEmailAsync(identifier)
            ?? await userManager.Users.FirstOrDefaultAsync(candidate =>
                candidate.PhoneNumber == normalizedPhone);

        if (user is null || !user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Identifiants incorrects ou compte désactivé.");
            return View(model);
        }

        var result = await signInManager.PasswordSignInAsync(
            user, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
            return LocalRedirect(model.ReturnUrl ??
                (await userManager.IsInRoleAsync(user, Roles.Customer)
                    ? Url.Action("Index", "EspaceClient")!
                    : Url.Action("Index", "Home")!));

        ModelState.AddModelError(string.Empty, result.IsLockedOut
            ? "Trop de tentatives. Réessaie dans 15 minutes."
            : "Identifiants incorrects.");
        return View(model);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Inscription()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        return View(new RegisterViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Inscription(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var email = model.Email.Trim();
        var phone = NormalizePhone(model.PhoneNumber);
        if (await userManager.Users.AnyAsync(candidate => candidate.PhoneNumber == phone))
        {
            ModelState.AddModelError(nameof(model.PhoneNumber), "Ce numéro de téléphone est déjà utilisé.");
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            PhoneNumber = phone,
            FullName = model.FullName.Trim()
        };

        await using var transaction = await database.Database.BeginTransactionAsync();
        var result = await userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        if (!await roleManager.RoleExistsAsync(Roles.Customer))
        {
            var roleCreation = await roleManager.CreateAsync(new IdentityRole(Roles.Customer));
            if (!roleCreation.Succeeded)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError(string.Empty, "La création du compte a échoué. Réessaie plus tard.");
                return View(model);
            }
        }

        var roleResult = await userManager.AddToRoleAsync(user, Roles.Customer);
        if (!roleResult.Succeeded)
        {
            await transaction.RollbackAsync();
            ModelState.AddModelError(string.Empty, "La création du compte a échoué. Réessaie plus tard.");
            return View(model);
        }

        database.Customers.Add(new Customer
        {
            ApplicationUserId = user.Id,
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber!,
            NormalizedPhone = phone
        });
        await database.SaveChangesAsync();
        await transaction.CommitAsync();

        await signInManager.SignInAsync(user, isPersistent: false);
        return RedirectToAction("Index", "EspaceClient");
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deconnexion()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction(nameof(Connexion));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccesRefuse() => View();

    private static string NormalizePhone(string value) =>
        Regex.Replace(value, @"[^0-9+]", string.Empty);
}
