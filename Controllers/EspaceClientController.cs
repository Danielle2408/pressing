using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pressing.Data;
using Pressing.Security;
using Pressing.ViewModels;
using Pressing.Models;
using Microsoft.Extensions.Options;

namespace Pressing.Controllers;

[Authorize(Policy = Roles.Customer)]
[Route("EspaceClient/[action]")]
public sealed class EspaceClientController(ApplicationDbContext database, IOptions<OrderSettings> orderOptions) : Controller
{
    private OrderSettings Settings => orderOptions.Value;
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
    public async Task<IActionResult> NouvelleDemande()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!await database.Customers.AnyAsync(item => item.ApplicationUserId == userId)) return NotFound();
        return View(await BuildRequestPageAsync(new CustomerOrderRequestViewModel
        {
            CollectionAtLocal = DateTime.Now.AddDays(1)
        }));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NouvelleDemande(CustomerOrderRequestPageViewModel page)
    {
        var form = page.Form;
        if (form.Lines.Count is < 1 or > 30)
            ModelState.AddModelError("Form.Lines", "Une demande doit contenir de 1 à 30 lignes d’articles.");
        if (form.HomeCollection && (form.CollectionAtLocal is null || form.CollectionAtLocal <= DateTime.Now))
            ModelState.AddModelError("Form.CollectionAtLocal", "Choisis une date de collecte future.");
        if (form.HomeCollection && string.IsNullOrWhiteSpace(form.CollectionAddress))
            ModelState.AddModelError("Form.CollectionAddress", "L’adresse est requise pour une collecte à domicile.");
        if (form.CollectionAddress?.Length > 400)
            ModelState.AddModelError("Form.CollectionAddress", "L’adresse ne peut pas dépasser 400 caractères.");
        if (form.HomeDelivery && string.IsNullOrWhiteSpace(form.DeliveryAddress))
            ModelState.AddModelError("Form.DeliveryAddress", "L’adresse est requise pour une livraison à domicile.");
        if (form.DeliveryAddress?.Length > 400)
            ModelState.AddModelError("Form.DeliveryAddress", "L’adresse ne peut pas dépasser 400 caractères.");
        if (form.Notes?.Length > 1000)
            ModelState.AddModelError("Form.Notes", "La remarque ne peut pas dépasser 1 000 caractères.");

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var customer = await database.Customers.FirstOrDefaultAsync(item => item.ApplicationUserId == userId);
        if (customer is null) return NotFound();
        if (!ModelState.IsValid) return View(await BuildRequestPageAsync(form));

        var now = DateTime.UtcNow;
        var lines = new List<OrderLine>();
        var baseAmount = 0m;
        foreach (var input in form.Lines)
        {
            if (input.Quantity is < 1 or > 100)
            {
                ModelState.AddModelError("Form.Lines", "Chaque quantité doit être comprise entre 1 et 100.");
                return View(await BuildRequestPageAsync(form));
            }
            var tariff = await database.ServiceTariffs.Include(item => item.GarmentCategory)
                .Include(item => item.LaundryService)
                .Where(item => item.GarmentCategoryId == input.CategoryId && item.LaundryServiceId == input.ServiceId &&
                               item.GarmentCategory.IsActive && item.LaundryService.IsActive && item.ValidFromUtc <= now &&
                               (item.ValidUntilUtc == null || item.ValidUntilUtc > now))
                .OrderByDescending(item => item.ValidFromUtc).FirstOrDefaultAsync();
            if (tariff is null)
            {
                ModelState.AddModelError("Form.Lines", "Un article ou service n’a plus de tarif actif. Vérifie le catalogue.");
                return View(await BuildRequestPageAsync(form));
            }
            var lineTotal = decimal.Round(tariff.Price * input.Quantity, 2);
            baseAmount += lineTotal;
            lines.Add(new OrderLine
            {
                ServiceTariffId = tariff.Id, CategoryName = tariff.GarmentCategory.Name,
                ServiceName = tariff.LaundryService.Name, Quantity = input.Quantity, UnitPrice = tariff.Price,
                LineTotal = lineTotal, Color = CleanOptional(input.Color), Brand = CleanOptional(input.Brand),
                Notes = CleanOptional(input.Notes)
            });
        }

        var collectionAtUtc = form.HomeCollection && form.CollectionAtLocal.HasValue
            ? TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(form.CollectionAtLocal.Value, DateTimeKind.Unspecified), TimeZoneInfo.Local)
            : (DateTime?)null;
        var readyFrom = collectionAtUtc ?? now;
        var collectionFee = form.HomeCollection ? Settings.HomeCollectionFee : 0m;
        var deliveryFee = form.HomeDelivery ? Settings.HomeDeliveryFee : 0m;
        var order = new LaundryOrder
        {
            Number = $"TEMP-{Guid.NewGuid():N}", CustomerId = customer.Id, CreatedByUserId = userId!,
            Status = OrderStatus.PendingConfirmation, IsOnlineRequest = true, CreatedAtUtc = now,
            ExpectedReadyAtUtc = readyFrom.AddDays(Math.Max(1, Settings.DefaultProcessingDays)),
            HomeCollection = form.HomeCollection, HomeDelivery = form.HomeDelivery,
            CollectionRequestedAtUtc = collectionAtUtc,
            CollectionAddress = CleanOptional(form.CollectionAddress), Notes = CleanOptional(form.Notes),
            DeliveryAddress = CleanOptional(form.DeliveryAddress),
            CollectionFee = collectionFee, DeliveryFee = deliveryFee,
            TotalAmount = baseAmount + collectionFee + deliveryFee, Lines = lines
        };
        order.StatusHistory.Add(new OrderStatusHistory
        {
            PreviousStatus = null, NewStatus = OrderStatus.PendingConfirmation, UserId = userId!, ChangedAtUtc = now,
            Reason = "Demande créée depuis l’espace client."
        });
        database.LaundryOrders.Add(order);
        await database.SaveChangesAsync();
        order.Number = $"PR-{now.Year}-{order.Id:D6}";
        await database.SaveChangesAsync();
        TempData["RequestSuccess"] = $"Ta demande {order.Number} a été transmise au pressing pour confirmation.";
        return RedirectToAction(nameof(Details), new { id = order.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AnnulerDemande(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var order = await database.LaundryOrders.Include(item => item.Customer)
            .FirstOrDefaultAsync(item => item.Id == id && item.Customer.ApplicationUserId == userId);
        if (order is null) return NotFound();
        if (order.Status != OrderStatus.PendingConfirmation)
        {
            TempData["PaymentError"] = "Cette demande ne peut plus être annulée depuis ton espace client.";
            return RedirectToAction(nameof(Details), new { id });
        }
        var now = DateTime.UtcNow;
        order.Status = OrderStatus.Cancelled;
        order.CancellationReason = "Demande annulée par le client avant confirmation.";
        database.OrderStatusHistories.Add(new OrderStatusHistory
        {
            LaundryOrderId = id, PreviousStatus = OrderStatus.PendingConfirmation, NewStatus = OrderStatus.Cancelled,
            UserId = userId!, ChangedAtUtc = now, Reason = order.CancellationReason
        });
        await database.SaveChangesAsync();
        TempData["RequestSuccess"] = "La demande a été annulée.";
        return RedirectToAction(nameof(Details), new { id });
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PayerEnLigne(int id, [FromServices] IConfiguration configuration,
        [FromServices] IHttpClientFactory clients)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Challenge();
        var order = await database.LaundryOrders.Include(item => item.Customer)
            .Where(item => item.Id == id && item.Customer.ApplicationUserId == userId)
            .FirstOrDefaultAsync();
        if (order is null) return NotFound();
        await using var reservation = await database.Database.BeginTransactionAsync();
        var lockedOrder = await database.LaundryOrders.FromSqlInterpolated(
            $"SELECT * FROM \"LaundryOrders\" WHERE \"Id\" = {id} FOR UPDATE").AsNoTracking().SingleAsync();
        order.TotalAmount = lockedOrder.TotalAmount;
        order.AmountPaid = lockedOrder.AmountPaid;
        order.Status = lockedOrder.Status;
        var remaining = order.TotalAmount - order.AmountPaid;
        if (remaining <= 0 || order.Status is OrderStatus.PendingConfirmation or OrderStatus.Cancelled or OrderStatus.Delivered)
        {
            TempData["PaymentError"] = "Cette commande ne peut pas recevoir de paiement en ligne.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var publicKey = configuration["NotchPay:PublicKey"];
        var callbackBaseUrl = configuration["NotchPay:CallbackBaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(publicKey) || string.IsNullOrWhiteSpace(callbackBaseUrl) ||
            !Uri.TryCreate(callbackBaseUrl, UriKind.Absolute, out var callbackUri) || callbackUri.Scheme != "https")
        {
            TempData["PaymentError"] = "Le paiement en ligne n’est pas encore configuré par le pressing.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var pending = await database.OrderPayments.FirstOrDefaultAsync(payment =>
            payment.LaundryOrderId == id && payment.Method == PaymentMethod.Notchpay &&
            payment.Status == OrderPaymentStatus.Pending);
        if (pending?.CheckoutUrl is not null)
        {
            await reservation.CommitAsync();
            return Redirect(pending.CheckoutUrl);
        }
        if (pending is not null)
        {
            await reservation.CommitAsync();
            TempData["PaymentError"] = "Une demande de paiement est déjà en cours. Termine-la ou contacte le pressing.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var merchantReference = $"pressing-{Guid.NewGuid():N}";
        var payment = new OrderPayment
        {
            ReceiptNumber = null, MerchantReference = merchantReference, LaundryOrderId = id,
            Amount = decimal.Round(remaining, 2), Method = PaymentMethod.Notchpay,
            Status = OrderPaymentStatus.Pending, Currency = "XAF", CreatedByUserId = userId,
            CreatedAtUtc = DateTime.UtcNow
        };
        database.OrderPayments.Add(payment);
        await database.SaveChangesAsync();
        await reservation.CommitAsync();

        try
        {
            var client = clients.CreateClient("NotchPay");
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.notchpay.co/payments");
            request.Headers.TryAddWithoutValidation("Authorization", publicKey);
            request.Content = System.Net.Http.Json.JsonContent.Create(new
            {
                amount = payment.Amount, currency = "XAF", email = User.Identity?.Name,
                phone = order.Customer.PhoneNumber, description = $"Commande {order.Number}",
                reference = merchantReference,
                callback = $"{callbackBaseUrl}/EspaceClient/RetourPaiement"
            });
            using var response = await client.SendAsync(request);
            var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            if (!response.IsSuccessStatusCode || !TryReadCheckout(json, out var checkout, out var providerReference))
                throw new InvalidOperationException("Notch Pay n’a pas fourni de lien de paiement valide.");
            payment.CheckoutUrl = checkout;
            payment.ProviderReference = providerReference;
            await database.SaveChangesAsync();
            return Redirect(checkout);
        }
        catch (Exception)
        {
            payment.Status = OrderPaymentStatus.Failed;
            await database.SaveChangesAsync();
            TempData["PaymentError"] = "Impossible de démarrer le paiement Notch Pay. Réessaie dans quelques instants.";
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    [HttpGet]
    public async Task<IActionResult> RetourPaiement(string? reference, string? trxref,
        [FromServices] IConfiguration configuration, [FromServices] IHttpClientFactory clients)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var payment = await database.OrderPayments.Include(item => item.LaundryOrder)
            .Where(item => item.Method == PaymentMethod.Notchpay && item.MerchantReference == trxref &&
                           item.CreatedByUserId == userId)
            .FirstOrDefaultAsync();
        if (payment is null) return NotFound();
        var key = configuration["NotchPay:PublicKey"];
        if (string.IsNullOrWhiteSpace(reference) || string.IsNullOrWhiteSpace(key))
            return RedirectToAction(nameof(Details), new { id = payment.LaundryOrderId });
        var client = clients.CreateClient("NotchPay");
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.notchpay.co/payments/{Uri.EscapeDataString(reference)}");
        request.Headers.TryAddWithoutValidation("Authorization", key);
        using var response = await client.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            await ConfirmPaymentAsync(payment, json, reference);
        }
        return RedirectToAction(nameof(Details), new { id = payment.LaundryOrderId });
    }

    private static bool TryReadCheckout(System.Text.Json.JsonElement json, out string checkout, out string? providerRef)
    {
        checkout = string.Empty; providerRef = null;
        if (!json.TryGetProperty("authorization_url", out var url) || !Uri.TryCreate(url.GetString(), UriKind.Absolute, out var uri) ||
            uri.Scheme != "https" || uri.Host != "pay.notchpay.co") return false;
        checkout = uri.ToString();
        if (json.TryGetProperty("transaction", out var transaction))
            providerRef = transaction.ValueKind == System.Text.Json.JsonValueKind.String ? transaction.GetString() :
                transaction.ValueKind == System.Text.Json.JsonValueKind.Object && transaction.TryGetProperty("reference", out var r) ? r.GetString() : null;
        return true;
    }

    private async Task ConfirmPaymentAsync(OrderPayment payment, System.Text.Json.JsonElement json, string reference)
    {
        var data = json.TryGetProperty("data", out var d) ? d : json;
        var trx = data.TryGetProperty("transaction", out var t) ? t : data;
        if (!trx.TryGetProperty("status", out var status) || status.GetString() != "complete" ||
            !trx.TryGetProperty("amount", out var amount) || amount.GetDecimal() != payment.Amount ||
            !trx.TryGetProperty("currency", out var currency) || currency.GetString() != "XAF") return;

        await using var transaction = await database.Database.BeginTransactionAsync();
        var order = await database.LaundryOrders.FromSqlInterpolated(
            $"SELECT * FROM \"LaundryOrders\" WHERE \"Id\" = {payment.LaundryOrderId} FOR UPDATE").SingleAsync();
        var current = await database.OrderPayments.FirstAsync(item => item.Id == payment.Id);
        if (current.Status == OrderPaymentStatus.Confirmed) return;
        if (current.Status != OrderPaymentStatus.Pending || order.Status == OrderStatus.Cancelled ||
            order.TotalAmount - order.AmountPaid < current.Amount) return;
        current.ProviderReference = reference;
        current.Status = OrderPaymentStatus.Confirmed;
        current.ReceiptNumber = $"REC-{DateTime.UtcNow.Year}-{current.Id:D6}";
        order.AmountPaid += current.Amount;
        await database.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private async Task<CustomerOrderRequestPageViewModel> BuildRequestPageAsync(CustomerOrderRequestViewModel form)
    {
        var now = DateTime.UtcNow;
        var prices = await database.ServiceTariffs.AsNoTracking()
            .Where(item => item.GarmentCategory.IsActive && item.LaundryService.IsActive && item.ValidFromUtc <= now &&
                           (item.ValidUntilUtc == null || item.ValidUntilUtc > now))
            .Select(item => new OrderPriceOptionViewModel
            {
                CategoryId = item.GarmentCategoryId, ServiceId = item.LaundryServiceId,
                ServiceName = item.LaundryService.Name, Price = item.Price
            }).ToListAsync();
        return new CustomerOrderRequestPageViewModel
        {
            Form = form,
            Categories = await database.GarmentCategories.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.Name).ToListAsync(),
            Services = await database.LaundryServices.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.Name).ToListAsync(),
            Prices = prices, Settings = Settings
        };
    }

    private static string? CleanOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
