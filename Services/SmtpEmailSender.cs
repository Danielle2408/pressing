using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using Pressing.Models;

namespace Pressing.Services;

public sealed class SmtpEmailSender(IOptions<EmailSettings> options)
{
    public async Task SendAsync(string recipient, string subject, string htmlBody)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(settings.FromAddress))
            throw new InvalidOperationException("Les paramètres SMTP ne sont pas configurés.");
        using var message = new MailMessage
        {
            From = new MailAddress(settings.FromAddress, settings.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(new MailAddress(recipient));
        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.EnableSsl,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(settings.UserName, settings.Password)
        };
        await client.SendMailAsync(message);
    }
}
