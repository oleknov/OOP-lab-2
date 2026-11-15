using System.Text;
using System.Text.RegularExpressions;

namespace StudentPerformanceXml.Services;

public static class XmlFileDecoder
{
    private static readonly Regex EncodingPattern =
        new(@"^\s*<\?xml[^>]*encoding\s*=\s*[""']([A-Za-z0-9._\-]+)[""']", RegexOptions.Compiled);

    static XmlFileDecoder()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static string Decode(byte[] bytes)
    {
        Encoding encoding = DetectDeclaredEncoding(bytes) ?? Encoding.UTF8;
        using var reader = new StreamReader(new MemoryStream(bytes), encoding, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static Encoding? DetectDeclaredEncoding(byte[] bytes)
    {
        string head = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 256));
        Match match = EncodingPattern.Match(head.TrimStart('﻿', '?'));
        if (!match.Success) return null;

        try
        {
            return Encoding.GetEncoding(match.Groups[1].Value);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
