using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pressing.Data;
using Pressing.Security;
using Pressing.ViewModels;
using Pressing.Models;

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
        if (remaining <= 0 || order.Status is OrderStatus.Cancelled or OrderStatus.Delivered)
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
}
