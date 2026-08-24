using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DXOS.Infrastructure.Integrations;

public sealed record TikTokFieldData(string Name, IReadOnlyList<string>? Values);

public sealed record TikTokLeadPayload(
    string LeadId,
    string? AdvertiserId,
    string? FormId,
    string? AdId,
    string? CreateTime,
    IReadOnlyList<TikTokFieldData>? FieldData);

public sealed class TikTokLeadAdsClient
{
    private readonly HttpClient _httpClient;

    public TikTokLeadAdsClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    /// <summary>
    /// Verifies TikTok Webhook Signature.
    /// TikTok sends the HMAC-SHA256 signature in the X-TikTok-Signature header (or X-Signature), computed from App Secret and raw body.
    /// If appSecret is empty/null, verification is bypassed in local/dev mode.
    /// </summary>
    public static bool VerifySignature(string rawBody, string? headerSignature, string? appSecret)
    {
        if (string.IsNullOrWhiteSpace(appSecret))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(headerSignature))
        {
            return false;
        }

        var signatureHex = headerSignature.Trim();
        if (signatureHex.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            signatureHex = signatureHex.Substring("sha256=".Length).Trim();
        }

        var keyBytes = Encoding.UTF8.GetBytes(appSecret);
        var bodyBytes = Encoding.UTF8.GetBytes(rawBody);

        using var hmac = new HMACSHA256(keyBytes);
        var computedHash = hmac.ComputeHash(bodyBytes);
        var computedHex = Convert.ToHexString(computedHash).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHex),
            Encoding.UTF8.GetBytes(signatureHex.ToLowerInvariant()));
    }

    /// <summary>
    /// Parses TikTok Webhook Payload (both test leads and live events).
    /// </summary>
    public static TikTokLeadPayload? ParseWebhookPayload(string rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody)) return null;

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;

            // Check if nested in 'entry' or 'data' or top-level
            var dataEl = root;
            if (root.TryGetProperty("data", out var dProp) && dProp.ValueKind == JsonValueKind.Object)
            {
                dataEl = dProp;
            }
            else if (root.TryGetProperty("entry", out var eProp) && eProp.ValueKind == JsonValueKind.Array && eProp.GetArrayLength() > 0)
            {
                dataEl = eProp[0];
            }

            var leadId = dataEl.TryGetProperty("lead_id", out var lidProp) ? lidProp.GetString()
                       : (dataEl.TryGetProperty("id", out var idProp) ? idProp.GetString() : null);

            var advertiserId = dataEl.TryGetProperty("advertiser_id", out var advProp) ? advProp.GetString()
                             : (root.TryGetProperty("advertiser_id", out var rAdvProp) ? rAdvProp.GetString() : null);

            var formId = dataEl.TryGetProperty("page_id", out var pProp) ? pProp.GetString()
                       : (dataEl.TryGetProperty("form_id", out var fProp) ? fProp.GetString() : null);

            var adId = dataEl.TryGetProperty("ad_id", out var aProp) ? aProp.GetString() : null;
            var createTime = dataEl.TryGetProperty("create_time", out var ctProp) ? ctProp.GetString() : null;

            var fields = new List<TikTokFieldData>();

            // If fields are structured as 'lead_data' or 'field_data' or 'fields'
            JsonElement fieldsArray = default;
            if (dataEl.TryGetProperty("field_data", out var fd) && fd.ValueKind == JsonValueKind.Array)
            {
                fieldsArray = fd;
            }
            else if (dataEl.TryGetProperty("lead_data", out var ld) && ld.ValueKind == JsonValueKind.Array)
            {
                fieldsArray = ld;
            }
            else if (dataEl.TryGetProperty("fields", out var f) && f.ValueKind == JsonValueKind.Array)
            {
                fieldsArray = f;
            }

            if (fieldsArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in fieldsArray.EnumerateArray())
                {
                    var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? ""
                             : (item.TryGetProperty("field_name", out var fn) ? fn.GetString() ?? "" : "");

                    var values = new List<string>();
                    if (item.TryGetProperty("values", out var valArr) && valArr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var v in valArr.EnumerateArray())
                        {
                            var s = v.GetString();
                            if (s is not null) values.Add(s);
                        }
                    }
                    else if (item.TryGetProperty("value", out var valProp) && valProp.ValueKind == JsonValueKind.String)
                    {
                        var s = valProp.GetString();
                        if (s is not null) values.Add(s);
                    }

                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        fields.Add(new TikTokFieldData(name, values));
                    }
                }
            }

            // Also extract top-level field properties if provided directly (e.g. name, phone, email in test lead)
            if (dataEl.TryGetProperty("name", out var topName) && topName.ValueKind == JsonValueKind.String)
            {
                fields.Add(new TikTokFieldData("name", new[] { topName.GetString()! }));
            }
            if (dataEl.TryGetProperty("phone", out var topPhone) && topPhone.ValueKind == JsonValueKind.String)
            {
                fields.Add(new TikTokFieldData("phone", new[] { topPhone.GetString()! }));
            }
            else if (dataEl.TryGetProperty("phone_number", out var topPhoneNum) && topPhoneNum.ValueKind == JsonValueKind.String)
            {
                fields.Add(new TikTokFieldData("phone_number", new[] { topPhoneNum.GetString()! }));
            }
            if (dataEl.TryGetProperty("email", out var topEmail) && topEmail.ValueKind == JsonValueKind.String)
            {
                fields.Add(new TikTokFieldData("email", new[] { topEmail.GetString()! }));
            }

            return new TikTokLeadPayload(leadId ?? $"tt_lead_{Guid.NewGuid():N}", advertiserId, formId, adId, createTime, fields);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts lead contact details across English and Vietnamese keys.
    /// </summary>
    public static (string Name, string? Phone, string? Email) ExtractLeadFields(
        IReadOnlyList<TikTokFieldData>? fieldData,
        string fallbackName = "Khách hàng TikTok")
    {
        if (fieldData is null || fieldData.Count == 0)
        {
            return (fallbackName, null, null);
        }

        string? name = null;
        string? phone = null;
        string? email = null;
        string? firstName = null;
        string? lastName = null;

        foreach (var field in fieldData)
        {
            var key = field.Name.Trim().ToLowerInvariant();
            var val = field.Values?.FirstOrDefault()?.Trim();
            if (string.IsNullOrWhiteSpace(val)) continue;

            if (key is "name" or "full_name" or "họ tên" or "họ và tên" or "ho ten" or "user_name" or "contact_name")
            {
                name = val;
            }
            else if (key is "first_name" or "tên")
            {
                firstName = val;
            }
            else if (key is "last_name" or "họ")
            {
                lastName = val;
            }
            else if (key is "phone" or "phone_number" or "mobile" or "số điện thoại" or "so dien thoai" or "điện thoại" or "dien thoai")
            {
                phone = val;
            }
            else if (key is "email" or "e-mail" or "email_address" or "thư điện tử")
            {
                email = val;
            }
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            if (!string.IsNullOrWhiteSpace(firstName) || !string.IsNullOrWhiteSpace(lastName))
            {
                name = $"{lastName} {firstName}".Trim();
            }
            else
            {
                name = fallbackName;
            }
        }

        return (name, phone, email);
    }
}
