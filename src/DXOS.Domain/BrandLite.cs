namespace DXOS.Domain;

public static class BrandLite
{
    public static readonly IReadOnlyList<string> ProhibitedTerms =
    [
        "lừa đảo",
        "cam kết 100%",
        "số 1 việt nam",
        "đối thủ giả",
        "hàng lậu",
        "hoàn tiền vô điều kiện",
        "chữa bách bệnh",
        "tuyệt đối"
    ];

    public static void Validate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        foreach (var term in ProhibitedTerms)
        {
            if (text.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                throw new DomainRuleException("BrandBlocked", $"Nội dung vi phạm từ cấm thương hiệu: '{term}'.");
            }
        }
    }
}
