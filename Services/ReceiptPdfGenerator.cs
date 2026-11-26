using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Pressing.Models;

namespace Pressing.Services;

public static partial class ReceiptPdfGenerator
{
    public static byte[] Generate(OrderPayment payment)
    {
        var order = payment.LaundryOrder;
        var lines = new List<string>
        {
            "PRESSING",
            "RECU DE PAIEMENT",
            $"Recu : {payment.ReceiptNumber}",
            $"Commande : {order.Number}",
            $"Date : {payment.CreatedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm}",
            $"Client : {order.Customer.FullName}",
            $"Telephone : {order.Customer.PhoneNumber}",
            "-----------------------------------------------",
            "Articles et services :"
        };
        lines.AddRange(order.Lines.Select(item =>
            $"{item.Quantity} x {item.CategoryName} - {item.ServiceName} : {item.LineTotal.ToString("N0", CultureInfo.InvariantCulture)} FCFA"));
        lines.Add("-----------------------------------------------");
        lines.Add($"Total commande : {order.TotalAmount.ToString("N0", CultureInfo.InvariantCulture)} FCFA");
        lines.Add($"Total paye : {order.AmountPaid.ToString("N0", CultureInfo.InvariantCulture)} FCFA");
        lines.Add($"Solde restant : {(order.TotalAmount - order.AmountPaid).ToString("N0", CultureInfo.InvariantCulture)} FCFA");
        lines.Add($"Paiement recu : {payment.Amount.ToString("N0", CultureInfo.InvariantCulture)} FCFA");
        lines.Add($"Moyen : {payment.Method.ToFrench()}");
        if (!string.IsNullOrWhiteSpace(payment.TransactionReference)) lines.Add($"Reference : {payment.TransactionReference}");
        lines.Add("Merci de votre confiance.");

        var content = new StringBuilder("BT\n/F1 14 Tf\n48 790 Td\n");
        for (var index = 0; index < lines.Count; index++)
        {
            if (index == 1) content.Append("/F2 14 Tf\n");
            else if (index == 2) content.Append("/F1 10 Tf\n");
            content.Append('(').Append(EscapePdf(Ascii(lines[index]))).Append(") Tj\n0 -16 Td\n");
        }
        content.Append("ET\n");
        var stream = Encoding.ASCII.GetBytes(content.ToString());
        var objects = new[]
        {
            Encoding.ASCII.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"),
            Encoding.ASCII.GetBytes("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
            Encoding.ASCII.GetBytes("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R /F2 5 0 R >> >> /Contents 6 0 R >>"),
            Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"),
            Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>"),
            BuildStreamObject(stream)
        };
        using var output = new MemoryStream();
        Write(output, Encoding.ASCII.GetBytes("%PDF-1.4\n%Pressing\n"));
        var offsets = new List<long> { 0 };
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(output.Position);
            Write(output, Encoding.ASCII.GetBytes($"{index + 1} 0 obj\n"));
            Write(output, objects[index]);
            Write(output, Encoding.ASCII.GetBytes("\nendobj\n"));
        }
        var xrefOffset = output.Position;
        Write(output, Encoding.ASCII.GetBytes($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n"));
        for (var index = 1; index < offsets.Count; index++)
            Write(output, Encoding.ASCII.GetBytes($"{offsets[index]:D10} 00000 n \n"));
        Write(output, Encoding.ASCII.GetBytes($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF"));
        return output.ToArray();
    }

    private static byte[] BuildStreamObject(byte[] stream)
    {
        using var objectBytes = new MemoryStream();
        Write(objectBytes, Encoding.ASCII.GetBytes($"<< /Length {stream.Length} >>\nstream\n"));
        Write(objectBytes, stream);
        Write(objectBytes, Encoding.ASCII.GetBytes("endstream"));
        return objectBytes.ToArray();
    }

    private static void Write(Stream stream, byte[] bytes) => stream.Write(bytes, 0, bytes.Length);

    private static string Ascii(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var output = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            output.Append(character switch
            {
                '’' or '‘' => '\'', '–' or '—' => '-', '…' => '.', 'œ' => 'o', 'Œ' => 'O',
                _ when char.IsControl(character) => ' ',
                _ when character <= (char)127 => character,
                _ => '?'
            });
        }
        return output.ToString();
    }

    private static string EscapePdf(string value) => value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

    [GeneratedRegex("[^A-Za-z0-9._-]")]
    public static partial Regex InvalidFileNameCharacters();
}
