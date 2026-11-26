using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Encodings.Web;
using Pressing.Data;
using Pressing.Models;
using Pressing.Security;
using Pressing.Services;
using Pressing.ViewModels;

namespace Pressing.Controllers;

public sealed class CompteController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    RoleManager<IdentityRole> roleManager,
    ApplicationDbContext database,
    SmtpEmailSender emailSender,
    ILogger<CompteController> logger) : Controller
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
    [HttpGet]
    public IActionResult RenvoyerConfirmation() => View(new ForgotPasswordViewModel());

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
            : result.IsNotAllowed
                ? "Confirme ton adresse e-mail avant de te connecter. Tu peux demander un nouvel e-mail de confirmation."
            : "Identifiants incorrects.");
        return View(model);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult MotDePasseOublie() => View(new ForgotPasswordViewModel());

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MotDePasseOublie(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await userManager.FindByEmailAsync(model.Email.Trim());
        if (user is not null && user.IsActive && !string.IsNullOrWhiteSpace(user.Email))
        {
            try
            {
                var token = await userManager.GeneratePasswordResetTokenAsync(user);
                var link = Url.Action(nameof(ReinitialiserMotDePasse), "Compte",
                    new { userId = user.Id, token }, Request.Scheme);
                if (!string.IsNullOrWhiteSpace(link))
                {
                    var safeName = HtmlEncoder.Default.Encode(user.FullName);
                    var safeLink = HtmlEncoder.Default.Encode(link);
                    await emailSender.SendAsync(user.Email,
                        "Réinitialisation du mot de passe",
                        $"<p>Bonjour {safeName},</p><p>Pour réinitialiser ton mot de passe, ouvre ce lien :</p><p><a href=\"{safeLink}\">Réinitialiser mon mot de passe</a></p><p>Si tu n’as pas demandé cette réinitialisation, ignore cet e-mail.</p>");
                }
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Impossible d’envoyer un lien de réinitialisation à l’utilisateur {UserId}.", user.Id);
            }
        }
        return View("MotDePasseOublieConfirme");
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> ReinitialiserMotDePasse(string? userId, string? token)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token) ||
            await userManager.FindByIdAsync(userId) is null) return View("LienReinitialisationInvalide");
        return View(new ResetPasswordViewModel { UserId = userId, Token = token });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReinitialiserMotDePasse(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await userManager.FindByIdAsync(model.UserId);
        if (user is null || !user.IsActive) return View("LienReinitialisationInvalide");
        var result = await userManager.ResetPasswordAsync(user, model.Token, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }
        return View("MotDePasseReinitialise");
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
        await SendConfirmationEmailAsync(user);
        return View("ConfirmationEmailEnvoyee");
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RenvoyerConfirmation(ForgotPasswordViewModel model)
    {
        if (ModelState.IsValid)
        {
            var user = await userManager.FindByEmailAsync(model.Email.Trim());
            if (user is not null && user.IsActive && !user.EmailConfirmed) await SendConfirmationEmailAsync(user);
        }
        return View("ConfirmationEmailEnvoyee");
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> ConfirmerAdresse(string? userId, string? token)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token)) return View("LienConfirmationInvalide");
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return View("LienConfirmationInvalide");
        var result = await userManager.ConfirmEmailAsync(user, token);
        return result.Succeeded ? View("AdresseConfirmee") : View("LienConfirmationInvalide");
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

    private async Task SendConfirmationEmailAsync(ApplicationUser user)
    {
        try
        {
            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            var link = Url.Action(nameof(ConfirmerAdresse), "Compte", new { userId = user.Id, token }, Request.Scheme);
            if (string.IsNullOrWhiteSpace(link) || string.IsNullOrWhiteSpace(user.Email)) return;
            var safeName = HtmlEncoder.Default.Encode(user.FullName);
            var safeLink = HtmlEncoder.Default.Encode(link);
            await emailSender.SendAsync(user.Email, "Confirme ton adresse e-mail",
                $"<p>Bonjour {safeName},</p><p>Confirme ton adresse e-mail pour activer ton compte client :</p><p><a href=\"{safeLink}\">Confirmer mon adresse</a></p>");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Impossible d’envoyer l’e-mail de confirmation au compte {UserId}.", user.Id);
        }
    }
}
