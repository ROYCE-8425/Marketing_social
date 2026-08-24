using System.Security.Cryptography;
using System.Text;
using DXOS.Infrastructure.Integrations;
using Xunit;

namespace DXOS.Unit.Tests;

public sealed class TikTokLeadAdsTests
{
    [Fact]
    public void VerifySignature_ValidHmac_ReturnsTrue()
    {
        var secret = "test_tiktok_app_secret_123456";
        var body = "{\"event\":\"leadgen\",\"advertiser_id\":\"7123456789012345678\",\"lead_id\":\"1787380000000001\"}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        var signature = Convert.ToHexString(hash).ToLowerInvariant();

        var isValid = TikTokLeadAdsClient.VerifySignature(body, signature, secret);

        Assert.True(isValid);
    }

    [Fact]
    public void VerifySignature_WithSha256Prefix_ReturnsTrue()
    {
        var secret = "test_tiktok_app_secret_123456";
        var body = "{\"event\":\"leadgen\"}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        var signature = "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();

        var isValid = TikTokLeadAdsClient.VerifySignature(body, signature, secret);

        Assert.True(isValid);
    }

    [Fact]
    public void VerifySignature_InvalidHmac_ReturnsFalse()
    {
        var secret = "test_tiktok_app_secret_123456";
        var body = "{\"event\":\"leadgen\"}";
        var badSignature = "0000000000000000000000000000000000000000000000000000000000000000";

        var isValid = TikTokLeadAdsClient.VerifySignature(body, badSignature, secret);

        Assert.False(isValid);
    }

    [Fact]
    public void VerifySignature_NoSecretConfigured_BypassesAndReturnsTrue()
    {
        var body = "{\"event\":\"leadgen\"}";

        Assert.True(TikTokLeadAdsClient.VerifySignature(body, null, null));
        Assert.True(TikTokLeadAdsClient.VerifySignature(body, "", ""));
    }

    [Fact]
    public void ParseWebhookPayload_StandardLeadgenEvent_ParsesCorrectly()
    {
        var json = @"
        {
            ""event"": ""leadgen"",
            ""advertiser_id"": ""7123456789012345678"",
            ""data"": {
                ""lead_id"": ""tt_lead_9988776655"",
                ""form_id"": ""form_12345"",
                ""ad_id"": ""ad_67890"",
                ""create_time"": ""2026-08-23T05:00:00Z"",
                ""field_data"": [
                    { ""name"": ""full_name"", ""values"": [""Nguyễn Văn TikTok""] },
                    { ""name"": ""phone_number"", ""values"": [""0912345678""] },
                    { ""name"": ""email"", ""values"": [""tiktok.user@example.com""] }
                ]
            }
        }";

        var payload = TikTokLeadAdsClient.ParseWebhookPayload(json);

        Assert.NotNull(payload);
        Assert.Equal("tt_lead_9988776655", payload.LeadId);
        Assert.Equal("7123456789012345678", payload.AdvertiserId);
        Assert.Equal("form_12345", payload.FormId);
        Assert.Equal("ad_67890", payload.AdId);
        Assert.NotNull(payload.FieldData);
        Assert.Equal(3, payload.FieldData.Count);

        var (name, phone, email) = TikTokLeadAdsClient.ExtractLeadFields(payload.FieldData);
        Assert.Equal("Nguyễn Văn TikTok", name);
        Assert.Equal("0912345678", phone);
        Assert.Equal("tiktok.user@example.com", email);
    }

    [Fact]
    public void ParseWebhookPayload_TestLeadFormat_ExtractsCorrectly()
    {
        var json = @"
        {
            ""advertiser_id"": ""7123456789012345678"",
            ""lead_id"": ""tt_test_lead_001"",
            ""page_id"": ""form_test_001"",
            ""name"": ""Trần Thị TikTok Test"",
            ""phone"": ""0987654321"",
            ""email"": ""tranthitiktok@test.vn""
        }";

        var payload = TikTokLeadAdsClient.ParseWebhookPayload(json);

        Assert.NotNull(payload);
        Assert.Equal("tt_test_lead_001", payload.LeadId);
        Assert.Equal("7123456789012345678", payload.AdvertiserId);

        var (name, phone, email) = TikTokLeadAdsClient.ExtractLeadFields(payload.FieldData);
        Assert.Equal("Trần Thị TikTok Test", name);
        Assert.Equal("0987654321", phone);
        Assert.Equal("tranthitiktok@test.vn", email);
    }

    [Fact]
    public void ExtractLeadFields_VietnameseFieldNames_ExtractsCorrectly()
    {
        var fields = new List<TikTokFieldData>
        {
            new("Họ và tên", new[] { "Lê Hoàng TikTok" }),
            new("Số điện thoại", new[] { "0909998877" }),
            new("Thư điện tử", new[] { "lehoang@tiktokvn.com" })
        };

        var (name, phone, email) = TikTokLeadAdsClient.ExtractLeadFields(fields);

        Assert.Equal("Lê Hoàng TikTok", name);
        Assert.Equal("0909998877", phone);
        Assert.Equal("lehoang@tiktokvn.com", email);
    }
}
