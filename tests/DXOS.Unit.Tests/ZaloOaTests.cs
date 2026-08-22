using System.Net;
using System.Security.Cryptography;
using System.Text;
using DXOS.Infrastructure.Integrations;
using Xunit;

namespace DXOS.Unit.Tests;

public sealed class ZaloOaTests
{
    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseContent;

        public FakeHttpMessageHandler(HttpStatusCode statusCode, string responseContent)
        {
            _statusCode = statusCode;
            _responseContent = responseContent;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseContent, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    [Fact]
    public void VerifyWebhookSignature_ValidHmac_ReturnsTrue()
    {
        var secret = "test_oa_secret_key_123456";
        var body = "{\"event_name\":\"user_send_text\",\"oa_id\":\"1234567890\"}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        var signature = "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();

        var isValid = ZaloOaClient.VerifyWebhookSignature(body, signature, secret);

        Assert.True(isValid);
    }

    [Fact]
    public void VerifyWebhookSignature_InvalidHmac_ReturnsFalse()
    {
        var secret = "test_oa_secret_key_123456";
        var body = "{\"event_name\":\"user_send_text\"}";
        var badSignature = "sha256=0000000000000000000000000000000000000000000000000000000000000000";

        var isValid = ZaloOaClient.VerifyWebhookSignature(body, badSignature, secret);

        Assert.False(isValid);
    }

    [Fact]
    public void VerifyWebhookSignature_NoSecretsConfigured_BypassesAndReturnsTrue()
    {
        var body = "{\"event_name\":\"user_send_text\"}";

        Assert.True(ZaloOaClient.VerifyWebhookSignature(body, null, null, null));
        Assert.True(ZaloOaClient.VerifyWebhookSignature(body, "", "", ""));
    }

    [Fact]
    public void VerifyWebhookSignature_ZaloMacFormat_ReturnsTrue()
    {
        var secret = "my_oa_secret";
        var appId = "app_123";
        var timestamp = "1700000000000";
        var rawBody = "{\"event_name\":\"user_send_text\",\"sender\":{\"id\":\"u1\"}}";

        var macInput = $"{appId}{rawBody}{timestamp}{secret}";
        var computedMac = "mac=" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(macInput))).ToLowerInvariant();

        var isValid = ZaloOaClient.VerifyWebhookSignature(rawBody, computedMac, secret, null, timestamp, appId);

        Assert.True(isValid);
    }

    [Fact]
    public void ParseWebhookEvent_UserSendText_ParsesCorrectly()
    {
        var json = @"
        {
            ""app_id"": ""2184057745525782"",
            ""user_id_by_app"": ""app_user_123"",
            ""oa_id"": ""988656934325292"",
            ""event_name"": ""user_send_text"",
            ""sender"": {
                ""id"": ""zalo_user_998877""
            },
            ""recipient"": {
                ""id"": ""988656934325292""
            },
            ""message"": {
                ""text"": ""Xin chao OA"",
                ""msg_id"": ""msg_zalo_112233""
            },
            ""timestamp"": 1787380000000
        }";

        var ev = ZaloOaClient.ParseWebhookEvent(json);

        Assert.Equal("2184057745525782", ev.AppId);
        Assert.Equal("988656934325292", ev.OaId);
        Assert.Equal("user_send_text", ev.EventName);
        Assert.Equal("zalo_user_998877", ev.SenderId);
        Assert.Equal("988656934325292", ev.RecipientId);
        Assert.Equal("Xin chao OA", ev.Text);
        Assert.Equal("msg_zalo_112233", ev.MessageId);
        Assert.Equal(1787380000000L, ev.Timestamp);
    }

    [Fact]
    public void ParseWebhookEvent_UserSendImage_ExtractsAttachments()
    {
        var json = @"
        {
            ""app_id"": ""2184057745525782"",
            ""oa_id"": ""988656934325292"",
            ""event_name"": ""user_send_image"",
            ""sender"": { ""id"": ""zalo_user_image_1"" },
            ""message"": {
                ""msg_id"": ""msg_img_001"",
                ""text"": """",
                ""attachments"": [
                    {
                        ""type"": ""image"",
                        ""payload"": {
                            ""url"": ""https://zalo.me/photos/test1.jpg"",
                            ""thumbnail"": ""https://zalo.me/photos/thumb1.jpg""
                        }
                    }
                ]
            },
            ""timestamp"": 1787380100000
        }";

        var ev = ZaloOaClient.ParseWebhookEvent(json);

        Assert.Equal("user_send_image", ev.EventName);
        Assert.Equal("zalo_user_image_1", ev.SenderId);
        Assert.NotNull(ev.Attachments);
        Assert.Single(ev.Attachments);
        Assert.Equal("image", ev.Attachments[0].Type);
        Assert.Equal("https://zalo.me/photos/test1.jpg", ev.Attachments[0].Url);
    }

    [Fact]
    public void ParseWebhookEvent_FollowEvent_SetsFollowText()
    {
        var json = @"
        {
            ""app_id"": ""2184057745525782"",
            ""oa_id"": ""988656934325292"",
            ""event_name"": ""follow"",
            ""follower"": { ""id"": ""zalo_follower_123"" },
            ""timestamp"": 1787380200000
        }";

        var ev = ZaloOaClient.ParseWebhookEvent(json);

        Assert.Equal("follow", ev.EventName);
        Assert.Equal("zalo_follower_123", ev.SenderId);
        Assert.Equal("[Khách hàng quan tâm OA]", ev.Text);
    }

    [Fact]
    public async Task SendMessageAsync_SuccessfulResponse_ReturnsSuccess()
    {
        var responseJson = @"
        {
            ""error"": 0,
            ""message"": ""Success"",
            ""data"": {
                ""message_id"": ""zalo_sent_msg_888""
            }
        }";

        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, responseJson);
        var httpClient = new HttpClient(handler);
        var client = new ZaloOaClient(httpClient);

        var result = await client.SendMessageAsync("u123", "Shop da nhan tin", "valid_oa_token", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal("zalo_sent_msg_888", result.MessageId);
        Assert.Equal(0, result.ErrorCode);
    }

    [Fact]
    public async Task SendMessageAsync_ApiErrorResponse_ReturnsFailure()
    {
        var responseJson = @"
        {
            ""error"": -216,
            ""message"": ""Access token has expired""
        }";

        var handler = new FakeHttpMessageHandler(HttpStatusCode.BadRequest, responseJson);
        var httpClient = new HttpClient(handler);
        var client = new ZaloOaClient(httpClient);

        var result = await client.SendMessageAsync("u123", "Shop da nhan tin", "expired_token", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(-216, result.ErrorCode);
        Assert.Contains("Access token has expired", result.Message);
    }

    [Fact]
    public async Task FetchUserProfileAsync_SuccessfulResponse_ReturnsProfile()
    {
        var responseJson = @"
        {
            ""error"": 0,
            ""message"": ""Success"",
            ""data"": {
                ""user_id"": ""u123"",
                ""display_name"": ""Tran Nhu Y"",
                ""avatar"": ""https://zalo.me/avatar/u123.jpg"",
                ""user_gender"": ""2"",
                ""phone"": ""0901234567""
            }
        }";

        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, responseJson);
        var httpClient = new HttpClient(handler);
        var client = new ZaloOaClient(httpClient);

        var profile = await client.FetchUserProfileAsync("u123", "valid_token", cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(profile);
        Assert.Equal("u123", profile.UserId);
        Assert.Equal("Tran Nhu Y", profile.DisplayName);
        Assert.Equal("https://zalo.me/avatar/u123.jpg", profile.Avatar);
        Assert.Equal("0901234567", profile.Phone);
    }
}
