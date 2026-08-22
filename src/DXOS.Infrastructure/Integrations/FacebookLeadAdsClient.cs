using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DXOS.Infrastructure.Integrations;

public sealed record FacebookFieldData(string Name, IReadOnlyList<string>? Values);

public sealed record FacebookLeadPayload(
    string Id,
    string? CreatedTime,
    string? FormId,
    string? PageId,
    IReadOnlyList<FacebookFieldData>? FieldData);

public sealed class FacebookLeadAdsClient
{
    private readonly HttpClient _httpClient;

    public FacebookLeadAdsClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public static bool VerifySignature(string rawBody, string? headerSignature, string? appSecret)
    {
        if (string.IsNullOrWhiteSpace(appSecret))
        {
            // When App Secret is not configured, signature verification is bypassed in local/dev mode
            return true;
        }

        if (string.IsNullOrWhiteSpace(headerSignature))
        {
            return false;
        }

        var expectedPrefix = "sha256=";
        if (!headerSignature.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var signatureHex = headerSignature.Substring(expectedPrefix.Length).Trim();
        var keyBytes = Encoding.UTF8.GetBytes(appSecret);
        var bodyBytes = Encoding.UTF8.GetBytes(rawBody);

        using var hmac = new HMACSHA256(keyBytes);
        var computedHash = hmac.ComputeHash(bodyBytes);
        var computedHex = Convert.ToHexString(computedHash).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHex),
            Encoding.UTF8.GetBytes(signatureHex.ToLowerInvariant()));
    }

    public async Task<FacebookLeadPayload?> FetchLeadAsync(
        string leadgenId,
        string pageAccessToken,
        string apiVersion = "v21.0",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(leadgenId) || string.IsNullOrWhiteSpace(pageAccessToken))
        {
            return null;
        }

        var url = $"https://graph.facebook.com/{apiVersion}/{leadgenId}?access_token={Uri.EscapeDataString(pageAccessToken)}";
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var id = root.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? leadgenId : leadgenId;
        var createdTime = root.TryGetProperty("created_time", out var ctProp) ? ctProp.GetString() : null;
        var formId = root.TryGetProperty("form_id", out var fProp) ? fProp.GetString() : null;
        var pageId = root.TryGetProperty("page_id", out var pProp) ? pProp.GetString() : null;

        var fields = new List<FacebookFieldData>();
        if (root.TryGetProperty("field_data", out var fdProp) && fdProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in fdProp.EnumerateArray())
            {
                var name = item.TryGetProperty("name", out var nProp) ? nProp.GetString() ?? "" : "";
                var values = new List<string>();
                if (item.TryGetProperty("values", out var valArr) && valArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var v in valArr.EnumerateArray())
                    {
                        var s = v.GetString();
                        if (s is not null) values.Add(s);
                    }
                }
                fields.Add(new FacebookFieldData(name, values));
            }
        }

        return new FacebookLeadPayload(id, createdTime, formId, pageId, fields);
    }

    public static (string Name, string? Phone, string? Email) ExtractLeadFields(
        IReadOnlyList<FacebookFieldData>? fieldData,
        string fallbackName = "Khách hàng Facebook")
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

            if (key is "full_name" or "name" or "họ tên" or "họ và tên" or "ho ten")
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
            else if (key is "phone_number" or "phone" or "số điện thoại" or "so dien thoai" or "điện thoại" or "dien thoai")
            {
                phone = val;
            }
            else if (key is "email" or "e-mail" or "thư điện tử")
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
