namespace DXOS.Domain;

public static class LeadScoring
{
    public const int PhoneAndEmailScore = 80;
    public const int PhoneOrEmailScore = 50;
    public const int FallbackScore = 20;

    private static readonly string[] HighIntentKeywords =
    [
        "mua", "tư vấn", "tu van", "báo giá", "bao gia", "đăng ký", "dang ky",
        "khoá học", "khoa hoc", "học", "hoc", "demo", "cần", "can", "giá", "gia",
        "quan tâm", "quan tam", "ngay", "hotline", "hỗ trợ", "ho tro"
    ];

    public static int Score(string? phone, string? email)
    {
        var hasPhone = !string.IsNullOrWhiteSpace(phone);
        var hasEmail = !string.IsNullOrWhiteSpace(email);
        if (hasPhone && hasEmail)
        {
            return PhoneAndEmailScore;
        }

        if (hasPhone || hasEmail)
        {
            return PhoneOrEmailScore;
        }

        return FallbackScore;
    }

    public static (int Score, LeadLabel Label, ScoreBreakdown Breakdown, IReadOnlyList<string> Reasons) Calculate(
        string? name,
        string? phone,
        string? email,
        LeadSource source,
        Guid? campaignId,
        DateTimeOffset nowUtc)
    {
        var reasons = new List<string>();

        // 1. Hành vi (Behavior) - Max 40
        var hasPhone = !string.IsNullOrWhiteSpace(phone);
        var hasEmail = !string.IsNullOrWhiteSpace(email);
        int behavior;
        if (source == LeadSource.Form)
        {
            if (hasPhone && hasEmail)
            {
                behavior = 40;
                reasons.Add("Hành vi: Điền form website cung cấp đủ SĐT và Email (+40đ)");
            }
            else if (hasPhone || hasEmail)
            {
                behavior = 30;
                reasons.Add("Hành vi: Điền form website cung cấp một thông tin liên hệ (+30đ)");
            }
            else
            {
                behavior = 10;
                reasons.Add("Hành vi: Điền form không để lại thông tin liên hệ (+10đ)");
            }
        }
        else if (source == LeadSource.Call)
        {
            behavior = hasPhone ? 30 : 10;
            reasons.Add(hasPhone ? "Hành vi: Cuộc gọi trực tiếp có số điện thoại (+30đ)" : "Hành vi: Cuộc gọi không rõ số (+10đ)");
        }
        else // Message
        {
            behavior = (hasPhone || hasEmail) ? 25 : 10;
            reasons.Add((hasPhone || hasEmail) ? "Hành vi: Tin nhắn có thông tin liên hệ (+25đ)" : "Hành vi: Tin nhắn vãng lai (+10đ)");
        }

        // 2. Kênh (Channel) - Max 20
        int channel;
        if (source == LeadSource.Form)
        {
            channel = 20;
            reasons.Add("Kênh: Form Website (+20đ)");
        }
        else if (source == LeadSource.Call)
        {
            channel = 15;
            reasons.Add("Kênh: Cuộc gọi thoại stub-call (+15đ)");
        }
        else // Message
        {
            channel = 15;
            reasons.Add("Kênh: Tin nhắn stub-zalo (+15đ)");
        }

        // 3. Chiến dịch (Campaign) - Max 20
        int campaign;
        if (campaignId.HasValue && campaignId.Value != Guid.Empty)
        {
            campaign = 20;
            reasons.Add("Chiến dịch: Có gắn chiến dịch tiếp thị (+20đ)");
        }
        else
        {
            campaign = 0;
            reasons.Add("Chiến dịch: Không gắn chiến dịch (+0đ)");
        }

        // 4. Thời gian (Time) - Max 10 (Giờ vàng UTC+7: 8h-18h)
        var vnTime = nowUtc.ToOffset(TimeSpan.FromHours(7));
        int time;
        if (vnTime.Hour is >= 8 and < 18)
        {
            time = 10;
            reasons.Add("Thời gian: Khung giờ vàng 8h-18h (+10đ)");
        }
        else
        {
            time = 5;
            reasons.Add("Thời gian: Ngoài khung giờ vàng (+5đ)");
        }

        // 5. Ý định (Intent) - Max 10
        int intent = 5;
        var intentMatched = false;
        if (!string.IsNullOrWhiteSpace(name))
        {
            var lower = name.ToLowerInvariant();
            foreach (var kw in HighIntentKeywords)
            {
                if (lower.Contains(kw, StringComparison.OrdinalIgnoreCase))
                {
                    intent = 10;
                    intentMatched = true;
                    reasons.Add($"Ý định: Có từ khóa nhu cầu cao '{kw}' (+10đ)");
                    break;
                }
            }
        }
        if (!intentMatched)
        {
            reasons.Add("Ý định: Nhu cầu tiêu chuẩn (+5đ)");
        }

        var total = Math.Clamp(behavior + channel + campaign + time + intent, 0, 100);

        var label = total switch
        {
            >= 80 => LeadLabel.Hot,
            >= 50 => LeadLabel.Warm,
            >= 20 => LeadLabel.Cold,
            _ => LeadLabel.Junk
        };

        var breakdown = new ScoreBreakdown(behavior, channel, campaign, time, intent, total);
        return (total, label, breakdown, reasons);
    }
}
