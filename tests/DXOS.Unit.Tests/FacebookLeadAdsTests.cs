using System.Net;
using System.Security.Cryptography;
using System.Text;
using DXOS.Infrastructure.Integrations;
using Xunit;

namespace DXOS.Unit.Tests;

public sealed class FacebookLeadAdsTests
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
    public void VerifySignature_ValidHmac_ReturnsTrue()
    {
        var secret = "test_app_secret_123456";
        var body = "{\"object\":\"page\",\"entry\":[{\"id\":\"123\"}]}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        var signature = "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();

        var isValid = FacebookLeadAdsClient.VerifySignature(body, signature, secret);

        Assert.True(isValid);
    }

    [Fact]
    public void VerifySignature_InvalidHmac_ReturnsFalse()
    {
        var secret = "test_app_secret_123456";
        var body = "{\"object\":\"page\",\"entry\":[{\"id\":\"123\"}]}";
        var badSignature = "sha256=0000000000000000000000000000000000000000000000000000000000000000";

        var isValid = FacebookLeadAdsClient.VerifySignature(body, badSignature, secret);

        Assert.False(isValid);
    }

    [Fact]
    public void VerifySignature_MissingHeaderWithSecret_ReturnsFalse()
    {
        var secret = "test_app_secret_123456";
        var body = "{\"object\":\"page\"}";

        Assert.False(FacebookLeadAdsClient.VerifySignature(body, null, secret));
        Assert.False(FacebookLeadAdsClient.VerifySignature(body, "", secret));
        Assert.False(FacebookLeadAdsClient.VerifySignature(body, "invalid_prefix_signature", secret));
    }

    [Fact]
    public void VerifySignature_NoAppSecretConfigured_ReturnsTrue()
    {
        var body = "{\"object\":\"page\"}";

        Assert.True(FacebookLeadAdsClient.VerifySignature(body, null, null));
        Assert.True(FacebookLeadAdsClient.VerifySignature(body, null, ""));
    }

    [Fact]
    public void ExtractLeadFields_MapsVietnameseAndEnglishFields()
    {
        var englishFields = new List<FacebookFieldData>
        {
            new("full_name", ["Nguyen Van Test"]),
            new("phone_number", ["0901234567"]),
            new("email", ["test@example.com"])
        };

        var (nameEn, phoneEn, emailEn) = FacebookLeadAdsClient.ExtractLeadFields(englishFields);
        Assert.Equal("Nguyen Van Test", nameEn);
        Assert.Equal("0901234567", phoneEn);
        Assert.Equal("test@example.com", emailEn);

        var vietnameseFields = new List<FacebookFieldData>
        {
            new("họ tên", ["Tran Thi Viet"]),
            new("số điện thoại", ["0912345678"]),
            new("thư điện tử", ["viet@example.vn"])
        };

        var (nameVi, phoneVi, emailVi) = FacebookLeadAdsClient.ExtractLeadFields(vietnameseFields);
        Assert.Equal("Tran Thi Viet", nameVi);
        Assert.Equal("0912345678", phoneVi);
        Assert.Equal("viet@example.vn", emailVi);
    }

    [Fact]
    public void ExtractLeadFields_FirstAndLastName_CombinesCorrectly()
    {
        var fields = new List<FacebookFieldData>
        {
            new("first_name", ["An"]),
            new("last_name", ["Le"]),
            new("phone", ["0923456789"])
        };

        var (name, phone, email) = FacebookLeadAdsClient.ExtractLeadFields(fields);
        Assert.Equal("Le An", name);
        Assert.Equal("0923456789", phone);
        Assert.Null(email);
    }

    [Fact]
    public async Task FetchLeadAsync_SuccessfulGraphResponse_ReturnsParsedPayload()
    {
        var graphJson = @"
        {
            ""id"": ""1122334455"",
            ""created_time"": ""2026-08-22T08:00:00+0000"",
            ""form_id"": ""form-999"",
            ""page_id"": ""page-888"",
            ""field_data"": [
                { ""name"": ""full_name"", ""values"": [""Pham Hoang Lead""] },
                { ""name"": ""phone_number"", ""values"": [""0934567890""] },
                { ""name"": ""email"", ""values"": [""lead@dxos.marketing""] }
            ]
        }";

        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, graphJson);
        var httpClient = new HttpClient(handler);
        var client = new FacebookLeadAdsClient(httpClient);

        var payload = await client.FetchLeadAsync("1122334455", "EAAtest_token_123", cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(payload);
        Assert.Equal("1122334455", payload.Id);
        Assert.Equal("form-999", payload.FormId);
        Assert.Equal("page-888", payload.PageId);
        Assert.Equal(3, payload.FieldData?.Count);

        var (name, phone, email) = FacebookLeadAdsClient.ExtractLeadFields(payload.FieldData);
        Assert.Equal("Pham Hoang Lead", name);
        Assert.Equal("0934567890", phone);
        Assert.Equal("lead@dxos.marketing", email);
    }

    [Fact]
    public async Task FetchLeadAsync_GraphErrorResponse_ReturnsNull()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.NotFound, "{\"error\":{\"message\":\"Lead not found\"}}");
        var httpClient = new HttpClient(handler);
        var client = new FacebookLeadAdsClient(httpClient);

        var payload = await client.FetchLeadAsync("invalid_id", "EAAtest_token_123", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(payload);
    }
}
