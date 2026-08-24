using System.Net;
using System.Text;
using DXOS.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DXOS.Unit.Tests;

public sealed class GeminiChatClientTests
{
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    private static GeminiChatClient CreateClient(HttpMessageHandler handler, string? apiKey = "test-gemini-key")
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GEMINI_API_KEY"] = apiKey,
                ["GEMINI_MODEL"] = "gemini-2.5-flash-lite"
            })
            .Build();

        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com/")
        };
        var fallback = new MockChatClient(NullLogger<MockChatClient>.Instance);
        return new GeminiChatClient(http, config, NullLogger<GeminiChatClient>.Instance, fallback);
    }

    [Fact]
    public async Task CompleteAsync_ParsesGenerateContentText_AndSendsKeyInHeaderNotQuery()
    {
        HttpRequestMessage? captured = null;
        var handler = new MockHttpMessageHandler(req =>
        {
            captured = req;
            var json = """
            {"candidates":[{"content":{"parts":[{"text":"{\"advisor\":\"DX-OS Marketing AI Expert\",\"recommendations\":[]}"}]}}]}
            """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });

        var client = CreateClient(handler);
        var result = await client.CompleteAsync(
            "Bạn là cố vấn, không được đăng bài.",
            "Đánh giá Fanpage: Hộp thư chưa trả lời.",
            TestContext.Current.CancellationToken);

        Assert.Contains("recommendations", result, StringComparison.Ordinal);
        Assert.NotNull(captured);
        Assert.Contains("gemini-2.5-flash-lite:generateContent", captured!.RequestUri!.ToString());
        Assert.DoesNotContain("key=", captured.RequestUri.Query, StringComparison.OrdinalIgnoreCase);
        Assert.True(captured.Headers.TryGetValues("x-goog-api-key", out var values));
        Assert.Equal("test-gemini-key", values.Single());
    }

    [Fact]
    public async Task CompleteAsync_WhenGeminiFails_FallsBackToMock()
    {
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("""{"error":{"message":"quota"}}""", Encoding.UTF8, "application/json")
        });

        var client = CreateClient(handler);
        var result = await client.CompleteAsync(
            "Bạn là trợ lý tư vấn Royce Shop.",
            "Khách hàng A nhắn: hi. Hãy soạn 1 tin trả lời ngắn, xin SĐT, không hứa giá/kho.",
            TestContext.Current.CancellationToken);

        Assert.Contains("SĐT", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompleteAsync_WhenApiKeyMissing_DoesNotCallHttp()
    {
        var called = false;
        var handler = new MockHttpMessageHandler(_ =>
        {
            called = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var client = CreateClient(handler, apiKey: "");
        var result = await client.CompleteAsync("cố vấn", "Hộp thư chưa trả lời", TestContext.Current.CancellationToken);
        Assert.False(called);
        Assert.Contains("Inbox", result, StringComparison.OrdinalIgnoreCase);
    }
}
