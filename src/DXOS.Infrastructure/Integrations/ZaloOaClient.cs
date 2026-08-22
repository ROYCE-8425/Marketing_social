using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DXOS.Infrastructure.Integrations;

public sealed record ZaloWebhookAttachment(
    string Type,
    string? Url,
    string? Title,
    string? PayloadJson);

public sealed record ZaloWebhookEvent(
    string? AppId,
    string? OaId,
    string? EventName,
    string? SenderId,
    string? RecipientId,
    string? MessageId,
    string? Text,
    long? Timestamp,
    IReadOnlyList<ZaloWebhookAttachment>? Attachments,
    string RawJson);

public sealed record ZaloSendMessageResult(
    bool Success,
    string? MessageId,
    int ErrorCode,
    string? Message);

public sealed record ZaloUserProfile(
    string? UserId,
    string? DisplayName,
    string? Avatar,
    string? UserGender,
    string? Phone);

public sealed class ZaloOaClient
{
    private readonly HttpClient _httpClient;

    public ZaloOaClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public static bool VerifyWebhookSignature(
        string rawBody,
        string? headerSignature,
        string? oaSecret,
        string? appSecret = null,
        string? timestamp = null,
        string? appId = null)
    {
        // When neither OA Secret nor App Secret is configured, signature verification is bypassed in local/dev mode
        if (string.IsNullOrWhiteSpace(oaSecret) && string.IsNullOrWhiteSpace(appSecret))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(headerSignature))
        {
            // If signature is missing when secret is present, fail verification
            return false;
        }

        var secret = !string.IsNullOrWhiteSpace(oaSecret) ? oaSecret : appSecret!;
        var signatureToMatch = headerSignature.Trim();

        // 1. Check direct SHA256 / HMAC-SHA256 signature
        if (signatureToMatch.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            signatureToMatch = signatureToMatch.Substring("sha256=".Length).Trim();
        }
        else if (signatureToMatch.StartsWith("mac=", StringComparison.OrdinalIgnoreCase))
        {
            signatureToMatch = signatureToMatch.Substring("mac=".Length).Trim();
        }

        // Try standard HMAC-SHA256 of rawBody
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var bodyBytes = Encoding.UTF8.GetBytes(rawBody);
        var computedHmac = Convert.ToHexString(hmac.ComputeHash(bodyBytes)).ToLowerInvariant();

        if (CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHmac),
            Encoding.UTF8.GetBytes(signatureToMatch.ToLowerInvariant())))
        {
            return true;
        }

        // Try Zalo Mac format: sha256(appId + rawBody + timestamp + secret)
        if (!string.IsNullOrWhiteSpace(timestamp) || !string.IsNullOrWhiteSpace(appId))
        {
            var macInput = $"{appId ?? ""}{rawBody}{timestamp ?? ""}{secret}";
            var computedMac = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(macInput))).ToLowerInvariant();
            if (CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computedMac),
                Encoding.UTF8.GetBytes(signatureToMatch.ToLowerInvariant())))
            {
                return true;
            }
        }

        return false;
    }

    public static ZaloWebhookEvent ParseWebhookEvent(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return new ZaloWebhookEvent(null, null, null, null, null, null, null, null, null, rawJson);
        }

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            var appId = root.TryGetProperty("app_id", out var aProp) ? aProp.GetString() : null;
            var oaId = root.TryGetProperty("oa_id", out var oaProp) ? oaProp.GetString() : null;
            var eventName = root.TryGetProperty("event_name", out var enProp) ? enProp.GetString() : null;

            string? senderId = null;
            if (root.TryGetProperty("sender", out var senderProp))
            {
                if (senderProp.ValueKind == JsonValueKind.Object && senderProp.TryGetProperty("id", out var sIdProp))
                {
                    senderId = sIdProp.GetString();
                }
                else if (senderProp.ValueKind == JsonValueKind.String)
                {
                    senderId = senderProp.GetString();
                }
            }
            if (string.IsNullOrWhiteSpace(senderId) && root.TryGetProperty("user_id_by_app", out var ubaProp))
            {
                senderId = ubaProp.GetString();
            }

            string? recipientId = null;
            if (root.TryGetProperty("recipient", out var recProp))
            {
                if (recProp.ValueKind == JsonValueKind.Object && recProp.TryGetProperty("id", out var rIdProp))
                {
                    recipientId = rIdProp.GetString();
                }
                else if (recProp.ValueKind == JsonValueKind.String)
                {
                    recipientId = recProp.GetString();
                }
            }
            if (string.IsNullOrWhiteSpace(recipientId))
            {
                recipientId = oaId;
            }

            string? messageId = null;
            string? text = null;
            var attachments = new List<ZaloWebhookAttachment>();

            if (root.TryGetProperty("message", out var msgProp) && msgProp.ValueKind == JsonValueKind.Object)
            {
                if (msgProp.TryGetProperty("msg_id", out var mIdProp))
                {
                    messageId = mIdProp.GetString();
                }
                if (msgProp.TryGetProperty("text", out var tProp))
                {
                    text = tProp.GetString();
                }

                // Attachments
                if (msgProp.TryGetProperty("attachments", out var attArray) && attArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in attArray.EnumerateArray())
                    {
                        var type = item.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? "file" : "file";
                        string? url = null;
                        string? title = null;

                        if (item.TryGetProperty("payload", out var plProp) && plProp.ValueKind == JsonValueKind.Object)
                        {
                            if (plProp.TryGetProperty("url", out var uProp)) url = uProp.GetString();
                            if (plProp.TryGetProperty("thumbnail", out var thProp) && string.IsNullOrWhiteSpace(url)) url = thProp.GetString();
                            if (plProp.TryGetProperty("title", out var tiProp)) title = tiProp.GetString();
                            if (plProp.TryGetProperty("name", out var nProp) && string.IsNullOrWhiteSpace(title)) title = nProp.GetString();
                        }

                        attachments.Add(new ZaloWebhookAttachment(type, url, title, item.GetRawText()));
                    }
                }

                // Handle sticker, image, audio, video in payload directly
                if (msgProp.TryGetProperty("attachments", out var directAttachments) && directAttachments.ValueKind == JsonValueKind.Object)
                {
                    // Single attachment object case
                    var type = directAttachments.TryGetProperty("type", out var dt) ? dt.GetString() ?? "image" : "image";
                    string? url = null;
                    if (directAttachments.TryGetProperty("payload", out var pld) && pld.TryGetProperty("url", out var u))
                    {
                        url = u.GetString();
                    }
                    attachments.Add(new ZaloWebhookAttachment(type, url, null, directAttachments.GetRawText()));
                }
            }

            // Fallback for follower / unfollower events
            if (eventName is "follow" or "unfollow")
            {
                if (root.TryGetProperty("follower", out var fProp) && fProp.TryGetProperty("id", out var fId))
                {
                    senderId = fId.GetString() ?? senderId;
                }
                text = eventName == "follow" ? "[Khách hàng quan tâm OA]" : "[Khách hàng hủy quan tâm OA]";
            }

            long? timestamp = null;
            if (root.TryGetProperty("timestamp", out var tsProp))
            {
                if (tsProp.ValueKind == JsonValueKind.Number && tsProp.TryGetInt64(out var tsNum))
                {
                    timestamp = tsNum;
                }
                else if (tsProp.ValueKind == JsonValueKind.String && long.TryParse(tsProp.GetString(), out var tsParsed))
                {
                    timestamp = tsParsed;
                }
            }

            return new ZaloWebhookEvent(
                appId,
                oaId,
                eventName,
                senderId,
                recipientId,
                messageId,
                text,
                timestamp,
                attachments,
                rawJson);
        }
        catch
        {
            return new ZaloWebhookEvent(null, null, null, null, null, null, null, null, null, rawJson);
        }
    }

    public async Task<ZaloSendMessageResult> SendMessageAsync(
        string userId,
        string text,
        string oaAccessToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(oaAccessToken))
        {
            return new ZaloSendMessageResult(false, null, -1, "UserId and OaAccessToken are required");
        }

        try
        {
            var url = "https://openapi.zalo.me/v3.0/oa/message/cs";
            var payload = new
            {
                recipient = new
                {
                    user_id = userId
                },
                message = new
                {
                    text = text
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("access_token", oaAccessToken);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var resJson = await response.Content.ReadAsStringAsync(cancellationToken);

            using var doc = JsonDocument.Parse(resJson);
            var root = doc.RootElement;

            var error = root.TryGetProperty("error", out var errProp) ? errProp.GetInt32() : -1;
            var message = root.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : null;
            string? messageId = null;

            if (root.TryGetProperty("data", out var dataProp) && dataProp.TryGetProperty("message_id", out var mIdProp))
            {
                messageId = mIdProp.GetString();
            }

            var success = response.IsSuccessStatusCode && error == 0;
            return new ZaloSendMessageResult(success, messageId, error, message ?? response.StatusCode.ToString());
        }
        catch (Exception ex)
        {
            return new ZaloSendMessageResult(false, null, -1, ex.Message);
        }
    }

    public async Task<ZaloUserProfile?> FetchUserProfileAsync(
        string userId,
        string oaAccessToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(oaAccessToken))
        {
            return null;
        }

        try
        {
            var dataQuery = Uri.EscapeDataString(JsonSerializer.Serialize(new { user_id = userId }));
            var url = $"https://openapi.zalo.me/v3.0/oa/user/detail?data={dataQuery}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("access_token", oaAccessToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var errProp) && errProp.GetInt32() != 0)
            {
                return null;
            }

            if (root.TryGetProperty("data", out var dataProp))
            {
                var displayName = dataProp.TryGetProperty("display_name", out var dn) ? dn.GetString() : null;
                var avatar = dataProp.TryGetProperty("avatar", out var av) ? av.GetString() : null;
                var userGender = dataProp.TryGetProperty("user_gender", out var ug) ? ug.GetString() : null;
                var phone = dataProp.TryGetProperty("phone", out var ph) ? ph.GetString() : null;

                return new ZaloUserProfile(userId, displayName, avatar, userGender, phone);
            }
        }
        catch
        {
            // Fallback gracefully
        }

        return null;
    }
}
