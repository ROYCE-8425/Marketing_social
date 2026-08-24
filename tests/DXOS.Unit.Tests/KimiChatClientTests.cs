using System.Net;
using System.Text;
using DXOS.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DXOS.Unit.Tests;

public sealed class KimiChatClientTests
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

    private static KimiChatClient CreateClient(HttpMessageHandler handler, string? apiKey = "test-key")
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KIMI_API_KEY"] = apiKey,
                ["KIMI_MODEL"] = "kimi-k2.5"
            })
            .Build();

        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.moonshot.ai/v1/") };
        var fallback = new MockChatClient(NullLogger<MockChatClient>.Instance);
        return new KimiChatClient(http, config, NullLogger<KimiChatClient>.Instance, fallback);
    }

    [Fact]
    public async Task CompleteAsync_WhenProviderReturnsAssistantJson_ReturnsContentWithoutLoggingKey()
    {
        HttpRequestMessage? captured = null;
        var handler = new MockHttpMessageHandler(req =>
        {
            captured = req;
            var json = """
            {"choices":[{"message":{"role":"assistant","content":"{\"advisor\":\"DX-OS Marketing AI Expert\",\"disclaimer\":\"x\",\"recommendations\":[]}"}}]}
            """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });

        var client = CreateClient(handler);
        var result = await client.CompleteAsync(
            "Bạn là cố vấn, không được đăng bài / chi tiền / xóa. Chỉ đề xuất.",
            "Đánh giá Fanpage: Hộp thư chưa trả lời 100%.",
            TestContext.Current.CancellationToken);

        Assert.Contains("recommendations", result, StringComparison.Ordinal);
        Assert.NotNull(captured);
        Assert.Equal("Bearer", captured!.Headers.Authorization?.Scheme);
        Assert.Equal("test-key", captured.Headers.Authorization?.Parameter);
        Assert.Contains("chat/completions", captured.RequestUri!.ToString());
    }

    [Fact]
    public async Task CompleteAsync_WhenProviderFails_FallsBackToMockDraft()
    {
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("""{"error":{"message":"invalid"}}""", Encoding.UTF8, "application/json")
        });

        var client = CreateClient(handler);
        var result = await client.CompleteAsync(
            "Bạn là trợ lý tư vấn Royce Shop, chỉ soạn tin ngắn xin SĐT/Zalo.",
            "Khách hàng A nhắn tin: \"Shop ơi\". Hãy soạn 1 tin trả lời ngắn, xin SĐT, không hứa giá/kho.",
            TestContext.Current.CancellationToken);

        Assert.Contains("SĐT", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("recommendations", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompleteAsync_WhenApiKeyMissing_UsesMockWithoutCallingHttp()
    {
        var called = false;
        var handler = new MockHttpMessageHandler(_ =>
        {
            called = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var client = CreateClient(handler, apiKey: "");
        var result = await client.CompleteAsync(
            "Bạn là cố vấn, không được đăng bài.",
            "Hộp thư: tỉ lệ chưa trả lời 100%.",
            TestContext.Current.CancellationToken);

        Assert.False(called);
        Assert.Contains("Inbox", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StripMarkdownFence_RemovesJsonFence()
    {
        var raw = "```json\n{\"advisor\":\"x\"}\n```";
        Assert.Equal("{\"advisor\":\"x\"}", KimiChatClient.StripMarkdownFence(raw));
    }

    [Fact]
    public void IsDraftPrompt_DetectsInboxDraft()
    {
        Assert.True(KimiChatClient.IsDraftPrompt("x", "Hãy soạn 1 tin trả lời ngắn, xin SĐT"));
        Assert.False(KimiChatClient.IsDraftPrompt("Bạn là cố vấn, không được đăng bài", "Đánh giá Fanpage"));
    }
}
