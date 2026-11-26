using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pressing.Models;
using Pressing.Security;

namespace Pressing.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration)
    {
        var database = services.GetRequiredService<ApplicationDbContext>();
        await database.Database.MigrateAsync();

        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var roleName in Roles.All)
        {
            if (!await roles.RoleExistsAsync(roleName))
            {
                var result = await roles.CreateAsync(new IdentityRole(roleName));
                EnsureSucceeded(result, $"Création du rôle {roleName}");
            }
        }

        await SeedInitialAdministratorAsync(services, configuration);
    }

    private static async Task SeedInitialAdministratorAsync(IServiceProvider services, IConfiguration configuration)
    {
        var email = configuration["InitialAdmin:Email"]?.Trim();
        var password = configuration["InitialAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password))
            return;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                "InitialAdmin:Email et InitialAdmin:Password doivent être configurés ensemble.");

        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var administrator = await users.FindByEmailAsync(email);
        if (administrator is null)
        {
            administrator = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = "Administrateur principal"
            };

            var createResult = await users.CreateAsync(administrator, password);
            EnsureSucceeded(createResult, "Création du compte administrateur initial");
        }

        if (!await users.IsInRoleAsync(administrator, Roles.Administrator))
        {
            var roleResult = await users.AddToRoleAsync(administrator, Roles.Administrator);
            EnsureSucceeded(roleResult, "Affectation du rôle administrateur initial");
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"{operation} a échoué : {errors}");
        }
    }
}
