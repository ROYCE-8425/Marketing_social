using System.Text.RegularExpressions;

namespace DXOS.Application;

public static partial class PhoneExtractor
{
    // Regex for Vietnamese mobile phone numbers
    // Supports:
    //  - Local: 03x, 05x, 07x, 08x, 09x (10 digits)
    //  - International: +843x, 843x, (+84) 3x...
    //  - Delimiters: spaces, dashes, dots: 0912 345 678, 0912-345-678, 0912.345.678, (+84) 912 345 678
    private static readonly Regex VnPhoneRegex = new(
        @"(?:(?:\(\s*\+84\s*\)|\+84|84|0)\s*(?:\(\s*0?\s*\))?)\s*[-.\s]*([35789])[-.\s]*(\d)[-.\s]*(\d)[-.\s]*(\d)[-.\s]*(\d)[-.\s]*(\d)[-.\s]*(\d)[-.\s]*(\d)[-.\s]*(\d)\b",
        RegexOptions.Compiled);

    public static string? ExtractFirstPhoneNumber(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var match = VnPhoneRegex.Match(text);
        if (!match.Success) return null;

        // Normalize to standard 10-digit 09xxxxxxxx format
        var digits = string.Concat(
            match.Groups[1].Value,
            match.Groups[2].Value,
            match.Groups[3].Value,
            match.Groups[4].Value,
            match.Groups[5].Value,
            match.Groups[6].Value,
            match.Groups[7].Value,
            match.Groups[8].Value,
            match.Groups[9].Value);

        return $"0{digits}";
    }

    public static IReadOnlyList<string> ExtractAllPhoneNumbers(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        var list = new List<string>();
        var matches = VnPhoneRegex.Matches(text);
        foreach (Match match in matches)
        {
            if (match.Success)
            {
                var digits = string.Concat(
                    match.Groups[1].Value,
                    match.Groups[2].Value,
                    match.Groups[3].Value,
                    match.Groups[4].Value,
                    match.Groups[5].Value,
                    match.Groups[6].Value,
                    match.Groups[7].Value,
                    match.Groups[8].Value,
                    match.Groups[9].Value);

                var normalized = $"0{digits}";
                if (!list.Contains(normalized))
                {
                    list.Add(normalized);
                }
            }
        }
        return list;
    }
}
