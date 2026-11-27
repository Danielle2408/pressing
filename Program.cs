using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pressing.Data;
using Pressing.Models;
using Pressing.Security;

var builder = WebApplication.CreateBuilder(args);

// In local development, read ignored .env values for the app as well as Docker Compose.
// Explicit process environment variables retain precedence over this local file.
if (builder.Environment.IsDevelopment())
{
    var localEnvPath = Path.Combine(builder.Environment.ContentRootPath, ".env");
    if (File.Exists(localEnvPath))
    {
        var localValues = new Dictionary<string, string?>();
        foreach (var line in File.ReadLines(localEnvPath))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;
            var separator = trimmed.IndexOf('=');
            if (separator <= 0) continue;
            var key = trimmed[..separator].Trim().Replace("__", ":", StringComparison.Ordinal);
            var value = trimmed[(separator + 1)..].Trim();
            if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
                value = value[1..^1];
            if (string.IsNullOrWhiteSpace(key) || !string.IsNullOrEmpty(builder.Configuration[key])) continue;
            localValues[key] = value;
        }
        builder.Configuration.AddInMemoryCollection(localValues);
    }
}

builder.Services.AddControllersWithViews();
builder.Services.Configure<OrderSettings>(builder.Configuration.GetSection("Ordering"));
builder.Services.Configure<LoyaltySettings>(builder.Configuration.GetSection("Loyalty"));
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
builder.Services.AddScoped<Pressing.Services.LoyaltyRewardService>();
builder.Services.AddScoped<Pressing.Services.PromotionPricingService>();
builder.Services.AddScoped<Pressing.Services.SmtpEmailSender>();
builder.Services.AddScoped<Pressing.Services.CustomerStatusEmailService>();
builder.Services.AddScoped<Pressing.Services.AuditLogService>();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Pressing")));

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = false;
        options.SignIn.RequireConfirmedEmail = false;
        options.Password.RequiredLength = 10;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager();

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Compte/Connexion";
    options.AccessDeniedPath = "/Compte/AccesRefuse";
});
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.Zero);

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Roles.Administrator, policy => policy.RequireRole(Roles.Administrator));
    options.AddPolicy(Roles.Operator, policy => policy.RequireRole(Roles.Operator));
    options.AddPolicy(Roles.Customer, policy => policy.RequireRole(Roles.Customer));
});

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    await DatabaseInitializer.InitializeAsync(scope.ServiceProvider, app.Configuration);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
