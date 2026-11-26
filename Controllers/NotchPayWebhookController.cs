using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pressing.Data;
using Pressing.Models;

namespace Pressing.Controllers;

[ApiController]
[AllowAnonymous]
[Route("webhooks/notchpay")]
public sealed class NotchPayWebhookController(ApplicationDbContext database, IConfiguration configuration) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(65536)]
    public async Task<IActionResult> Receive()
    {
        var hashKey = configuration["NotchPay:WebhookHashKey"];
        var signature = Request.Headers["x-notch-signature"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(hashKey) || string.IsNullOrWhiteSpace(signature)) return Unauthorized();

        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        var expected = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(hashKey), Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
        var supplied = signature.Trim().ToLowerInvariant();
        if (expected.Length != supplied.Length || !CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(supplied))) return Unauthorized();

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var data = root.TryGetProperty("data", out var payload) ? payload : root;
        var reference = ReadString(data, "reference");
        var status = ReadString(data, "status");
        if (string.IsNullOrWhiteSpace(reference) || status != "complete") return Ok();

        var payment = await database.OrderPayments.Include(item => item.LaundryOrder)
            .FirstOrDefaultAsync(item => item.Method == PaymentMethod.Notchpay && item.ProviderReference == reference);
        if (payment is null || payment.Status == OrderPaymentStatus.Confirmed) return Ok();
        if (!TryReadDecimal(data, "amount", out var amount) || amount != payment.Amount ||
            ReadString(data, "currency") != "XAF") return Ok();

        await using var transaction = await database.Database.BeginTransactionAsync();
        var order = await database.LaundryOrders.FromSqlInterpolated(
            $"SELECT * FROM \"LaundryOrders\" WHERE \"Id\" = {payment.LaundryOrderId} FOR UPDATE").SingleAsync();
        var current = await database.OrderPayments.FirstAsync(item => item.Id == payment.Id);
        if (current.Status == OrderPaymentStatus.Pending && order.Status != OrderStatus.Cancelled &&
            order.TotalAmount - order.AmountPaid >= current.Amount)
        {
            current.Status = OrderPaymentStatus.Confirmed;
            current.ReceiptNumber = $"REC-{DateTime.UtcNow.Year}-{current.Id:D6}";
            order.AmountPaid += current.Amount;
            await database.SaveChangesAsync();
        }
        await transaction.CommitAsync();
        return Ok();
    }

    private static string? ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool TryReadDecimal(JsonElement element, string property, out decimal value)
    {
        value = 0;
        return element.TryGetProperty(property, out var amount) &&
               (amount.ValueKind == JsonValueKind.Number ? amount.TryGetDecimal(out value) :
                   amount.ValueKind == JsonValueKind.String && decimal.TryParse(amount.GetString(), out value));
    }
}
