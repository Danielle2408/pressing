using System.Text.Encodings.Web;
using Microsoft.EntityFrameworkCore;
using Pressing.Data;
using Pressing.Models;

namespace Pressing.Services;

public sealed class CustomerStatusEmailService(
    ApplicationDbContext database,
    SmtpEmailSender emailSender,
    ILogger<CustomerStatusEmailService> logger)
{
    public async Task SendAsync(LaundryOrder order, string customerName, OrderStatus status)
    {
        var recipient = await database.Customers.AsNoTracking()
            .Where(customer => customer.Id == order.CustomerId && customer.ReceiveStatusEmails && customer.ApplicationUser != null &&
                               customer.ApplicationUser.EmailConfirmed)
            .Select(customer => customer.ApplicationUser!.Email)
            .FirstOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(recipient)) return;

        var safeName = HtmlEncoder.Default.Encode(customerName);
        var safeNumber = HtmlEncoder.Default.Encode(order.Number);
        var statusLabel = HtmlEncoder.Default.Encode(status.ToFrench());
        var message = status switch
        {
            OrderStatus.Ready => $"Ta commande <strong>{safeNumber}</strong> est prête. Tu peux venir la retirer au pressing.",
            OrderStatus.Delivered => $"La commande <strong>{safeNumber}</strong> a été remise.",
            OrderStatus.Cancelled => $"La commande <strong>{safeNumber}</strong> a été annulée.",
            _ => $"Le statut de ta commande <strong>{safeNumber}</strong> est maintenant : {statusLabel}."
        };
        try
        {
            await emailSender.SendAsync(recipient, $"Mise à jour de la commande {order.Number}",
                $"<p>Bonjour {safeName},</p><p>{message}</p><p>Connecte-toi à ton espace client pour consulter le détail.</p>");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Impossible d'envoyer l'e-mail de statut pour la commande {OrderNumber}.", order.Number);
        }
    }
}
