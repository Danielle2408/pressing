using System.Globalization;
using System.Text;
using Pressing.Models;

namespace Pressing.Services;

public static class ReportPdfGenerator
{
    public static byte[] Generate(string title, IReadOnlyList<string> lines)
    {
        var content = new StringBuilder("BT\n/F2 16 Tf\n48 790 Td\n(").Append(Escape(Ascii(title))).Append(") Tj\n/F1 10 Tf\n");
        foreach (var line in lines.Take(43)) content.Append("0 -16 Td\n(").Append(Escape(Ascii(Truncate(line, 92)))).Append(") Tj\n");
        content.Append("ET\n");
        var stream = Encoding.ASCII.GetBytes(content.ToString());
        var objects = new[]
        {
            Encoding.ASCII.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"),
            Encoding.ASCII.GetBytes("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
            Encoding.ASCII.GetBytes("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R /F2 5 0 R >> >> /Contents 6 0 R >>"),
            Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"),
            Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>"),
            StreamObject(stream)
        };
        using var output = new MemoryStream();
        Write(output, Encoding.ASCII.GetBytes("%PDF-1.4\n%Pressing report\n"));
        var offsets = new List<long> { 0 };
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(output.Position);
            Write(output, Encoding.ASCII.GetBytes($"{i + 1} 0 obj\n")); Write(output, objects[i]); Write(output, Encoding.ASCII.GetBytes("\nendobj\n"));
        }
        var xref = output.Position;
        Write(output, Encoding.ASCII.GetBytes($"xref\n0 {offsets.Count}\n0000000000 65535 f \n"));
        for (var i = 1; i < offsets.Count; i++) Write(output, Encoding.ASCII.GetBytes($"{offsets[i]:D10} 00000 n \n"));
        Write(output, Encoding.ASCII.GetBytes($"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF"));
        return output.ToArray();
    }

    private static byte[] StreamObject(byte[] content)
    {
        using var result = new MemoryStream();
        Write(result, Encoding.ASCII.GetBytes($"<< /Length {content.Length} >>\nstream\n")); Write(result, content); Write(result, Encoding.ASCII.GetBytes("endstream"));
        return result.ToArray();
    }

    private static string Ascii(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var output = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            output.Append(char.IsControl(character) ? ' ' : character <= (char)127 ? character : '?');
        }
        return output.ToString();
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..(max - 3)] + "...";
    private static void Write(Stream stream, byte[] bytes) => stream.Write(bytes, 0, bytes.Length);
}
