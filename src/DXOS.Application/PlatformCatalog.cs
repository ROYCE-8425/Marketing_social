using DXOS.Domain;

namespace DXOS.Application;

public static class PlatformCatalog
{
    public const string ReadLeads = "READ_LEADS";
    public const string Webhook = "WEBHOOK";
    public const string ReadMessages = "READ_MESSAGES";

    public static IReadOnlyList<PlatformConnectionInfo> MockConnections { get; } =
    [
        new("facebook", "Mock Facebook", [ReadLeads, Webhook], "demo"),
        new("tiktok", "Mock TikTok", [ReadLeads, Webhook], "demo"),
        new("zalo", "Mock Zalo OA", [ReadLeads, ReadMessages, Webhook], "demo")
    ];

    public static bool TryParseSource(string? provider, out LeadSource source)
    {
        source = LeadSource.Form;
        if (string.IsNullOrWhiteSpace(provider))
        {
            return false;
        }

        switch (provider.Trim().ToLowerInvariant())
        {
            case "facebook":
            case "fb":
                source = LeadSource.Facebook;
                return true;
            case "tiktok":
            case "tt":
                source = LeadSource.TikTok;
                return true;
            case "zalo":
            case "zalo_oa":
                source = LeadSource.Zalo;
                return true;
            default:
                return false;
        }
    }

    public static string CanonicalProvider(LeadSource source) => source switch
    {
        LeadSource.Facebook => "facebook",
        LeadSource.TikTok => "tiktok",
        LeadSource.Zalo => "zalo",
        LeadSource.Form => "website",
        LeadSource.Message => "message",
        LeadSource.Call => "call",
        _ => "unknown"
    };
}

public sealed record PlatformConnectionInfo(
    string Provider,
    string DisplayName,
    IReadOnlyList<string> Capabilities,
    string Mode);
