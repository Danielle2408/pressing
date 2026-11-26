using Microsoft.Extensions.Options;
using Pressing.Data;
using Pressing.Models;

namespace Pressing.Services;

public sealed class LoyaltyRewardService(ApplicationDbContext database, IOptions<LoyaltySettings> options)
{
    public async Task AwardIfFullyPaidAsync(LaundryOrder order)
    {
        if (order.LoyaltyRewardGranted || order.AmountPaid < order.TotalAmount) return;
        var currencyPerPoint = options.Value.CurrencyPerPoint;
        if (currencyPerPoint <= 0) throw new InvalidOperationException("Loyalty:CurrencyPerPoint doit être supérieur à zéro.");

        var customer = await database.Customers.FindAsync(order.CustomerId);
        if (customer is null) return;
        var points = (int)Math.Min(int.MaxValue, decimal.Floor(order.TotalAmount / currencyPerPoint));
        order.LoyaltyRewardGranted = true;
        if (points <= 0) return;

        customer.LoyaltyPoints = checked(customer.LoyaltyPoints + points);
        database.LoyaltyLedgerEntries.Add(new LoyaltyLedgerEntry
        {
            CustomerId = customer.Id, LaundryOrderId = order.Id, Points = points,
            CreatedAtUtc = DateTime.UtcNow
        });
        if (!string.IsNullOrWhiteSpace(customer.ApplicationUserId))
        {
            database.UserNotifications.Add(new UserNotification
            {
                UserId = customer.ApplicationUserId, Title = "Points fidélité crédités",
                Message = $"Tu as gagné {points} point(s) après le paiement de {order.Number}.",
                Url = "/EspaceClient/Index", CreatedAtUtc = DateTime.UtcNow
            });
        }
    }
}
