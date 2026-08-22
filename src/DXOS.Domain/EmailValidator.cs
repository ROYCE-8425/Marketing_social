using System.Text.RegularExpressions;

namespace DXOS.Domain;

public static partial class EmailValidator
{
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailFormatRegex();

    public static string? Normalize(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var trimmed = email.Trim().ToLowerInvariant();
        var atIndex = trimmed.IndexOf('@');
        if (atIndex <= 0 || atIndex == trimmed.Length - 1)
        {
            throw new DomainRuleException("InvalidEmail", "Email không đúng định dạng.");
        }

        var domain = trimmed[(atIndex + 1)..];
        if (!domain.Contains('.') || domain.StartsWith('.') || domain.EndsWith('.') || !EmailFormatRegex().IsMatch(trimmed))
        {
            throw new DomainRuleException("InvalidEmail", "Email domain phải có dấu chấm hợp lệ (ví dụ: user@example.com hoặc cty.vn).");
        }

        return trimmed;
    }
}
