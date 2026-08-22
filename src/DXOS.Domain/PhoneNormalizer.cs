using System.Text.RegularExpressions;

namespace DXOS.Domain;

public static partial class PhoneNormalizer
{
    [GeneratedRegex(@"[^\d+]")]
    private static partial Regex NonDigitsOrPlusRegex();

    [GeneratedRegex(@"^0[0-9]{9}$")]
    private static partial Regex VnPhoneRegex();

    public static string? Normalize(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var cleaned = NonDigitsOrPlusRegex().Replace(phone.Trim(), string.Empty);
        if (cleaned.StartsWith("+84", StringComparison.Ordinal))
        {
            cleaned = "0" + cleaned[3..];
        }
        else if (cleaned.StartsWith("84", StringComparison.Ordinal) && cleaned.Length == 11)
        {
            cleaned = "0" + cleaned[2..];
        }

        if (!VnPhoneRegex().IsMatch(cleaned))
        {
            throw new DomainRuleException("InvalidPhone", "Số điện thoại VN không hợp lệ. Phải gồm 10 chữ số (ví dụ: 0xxxxxxxxx hoặc +84xxxxxxxxx).");
        }

        return cleaned;
    }
}
