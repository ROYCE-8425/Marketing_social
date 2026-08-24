using System.Text.Json;
using DXOS.Application;
using DXOS.Domain;
using DXOS.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DXOS.Unit.Tests;

public sealed class MockAdvisorTests
{
    private sealed class InMemoryPageHealthStore : IPageHealthStore
    {
        public Task<PageHealthData> GetHealthDataAsync(string pageId, CancellationToken cancellationToken = default)
        {
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new PageHealthData(
                PageId: pageId,
                PageName: "Royce Shop",
                FanCount: 200,
                FollowersCount: 200,
                Posts: new[]
                {
                    new PagePostData("p1", "Sale sđt 0901234567 inbox shop", now.AddDays(-2), 10, 2, 1, 500, 50, 20, "fresh")
                },
                TotalConversations: 10,
                UnrepliedConversations: 3,
                ConversationsWithPhone: 0,
                TotalLeads: 5,
                HotLeads: 2,
                WarmLeads: 2,
                CommentsPermissionForbidden: false,
                InsightsPartialOrForbidden: false,
                CommentsStatus: "ok"
            ));
        }

        public Task<IReadOnlyList<UnrepliedConversationData>> GetUnrepliedConversationsAsync(string pageId, int limit = 10, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<UnrepliedConversationData> list = new[]
            {
                new UnrepliedConversationData("conv_1", "Khách 1", "Shop ơi còn hàng không", null, null),
                new UnrepliedConversationData("conv_2", "Khách 2", "Cho mình hỏi giá áo", "0912345678", "sales_alice")
            };
            return Task.FromResult(list);
        }
    }

    [Fact]
    public async Task MockChatClient_ReturnsStructuredVietnameseRecommendations()
    {
        var client = new MockChatClient(NullLogger<MockChatClient>.Instance);
        var response = await client.CompleteAsync("system prompt", "Đánh giá page Royce Shop", TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        Assert.NotEmpty(response);

        using var doc = JsonDocument.Parse(response);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("recommendations", out var recs));
        Assert.Equal(3, recs.GetArrayLength());

        foreach (var rec in recs.EnumerateArray())
        {
            var hasTitle = (rec.TryGetProperty("title", out var titleEl) || rec.TryGetProperty("Title", out titleEl)) && !string.IsNullOrWhiteSpace(titleEl.GetString());
            var hasAction = (rec.TryGetProperty("actionText", out var actionEl) || rec.TryGetProperty("ActionText", out actionEl)) && !string.IsNullOrWhiteSpace(actionEl.GetString());
            var hasCat = (rec.TryGetProperty("category", out var catEl) || rec.TryGetProperty("Category", out catEl)) && !string.IsNullOrWhiteSpace(catEl.GetString());
            Assert.True(hasTitle);
            Assert.True(hasAction);
            Assert.True(hasCat);
        }
    }

    [Fact]
    public async Task MockChatClient_WithUnrepliedReasons_DerivesInboxRecommendation()
    {
        var client = new MockChatClient(NullLogger<MockChatClient>.Instance);
        var prompt = "Đánh giá Fanpage (ID: 988656934325292): Điểm 57/100, Trạng thái: Watch. Lý do: Tỉ lệ chưa trả lời tin nhắn cao (100.0%); Tỉ lệ khách để lại SĐT thấp";
        var response = await client.CompleteAsync("system prompt", prompt, TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        using var doc = JsonDocument.Parse(response);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("recommendations", out var recs));
        Assert.Equal(3, recs.GetArrayLength());

        bool foundInbox = false;
        foreach (var rec in recs.EnumerateArray())
        {
            var cat = rec.GetProperty("category").GetString();
            if (cat is "Inbox" or "Hộp thư")
            {
                foundInbox = true;
                Assert.Contains("SĐT", rec.GetProperty("title").GetString()!);
                break;
            }
        }

        Assert.True(foundInbox, "Expected at least one recommendation category to be Inbox when prompt contains 'chưa trả lời'");
    }

    [Fact]
    public async Task PageHealthService_GetPageAdviceAsync_CombinesEvaluationAndAdvisorWithoutWrites()
    {
        var store = new InMemoryPageHealthStore();
        var clock = new SystemClock();
        var healthService = new PageHealthService(store, clock);
        var chatClient = new MockChatClient(NullLogger<MockChatClient>.Instance);

        var advice = await healthService.GetPageAdviceAsync("988656934325292", chatClient, TestContext.Current.CancellationToken);

        Assert.NotNull(advice);
        Assert.NotNull(advice.Evaluation);
        Assert.Equal("page-eval", advice.Evaluation.ModelId);
        Assert.True(advice.Recommendations.TryGetProperty("recommendations", out var recs));
        Assert.Equal(3, recs.GetArrayLength());
        Assert.True(advice.Recommendations.TryGetProperty("disclaimer", out var discEl));
        Assert.Contains("không tự động đăng bài", discEl.GetString()!);
    }
}
