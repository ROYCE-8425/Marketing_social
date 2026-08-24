using DXOS.Application;
using DXOS.Domain;
using DXOS.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DXOS.Unit.Tests;

public sealed class InboxActionsTests
{
    private sealed class MockHealthStore : IPageHealthStore
    {
        public Task<PageHealthData> GetHealthDataAsync(string pageId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PageHealthData(
                PageId: pageId,
                PageName: "Royce Shop",
                FanCount: 100,
                FollowersCount: 100,
                Posts: Array.Empty<PagePostData>(),
                TotalConversations: 3,
                UnrepliedConversations: 3,
                ConversationsWithPhone: 1,
                TotalLeads: 0,
                HotLeads: 0,
                WarmLeads: 0,
                CommentsPermissionForbidden: false,
                InsightsPartialOrForbidden: false,
                CommentsStatus: "unknown"
            ));
        }

        public Task<IReadOnlyList<UnrepliedConversationData>> GetUnrepliedConversationsAsync(string pageId, int limit = 10, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<UnrepliedConversationData> list = new[]
            {
                new UnrepliedConversationData("c1", "Nguyễn Văn A", "Shop ơi tư vấn size L giúp mình", null, null),
                new UnrepliedConversationData("c2", "Trần Thị B", "Áo này bao nhiêu tiền vậy shop?", "0987654321", "sales_alice"),
                new UnrepliedConversationData("c3", "Lê Văn C", "Mình chốt 2 áo size XL nhé, gọi số 0909123456 cho mình", null, "sales_alice")
            };
            return Task.FromResult(list);
        }
    }

    [Fact]
    public async Task GetInboxActionsAsync_DraftsSuggestedRepliesForUnrepliedConversations()
    {
        var store = new MockHealthStore();
        var clock = new SystemClock();
        var service = new PageHealthService(store, clock);
        var chatClient = new MockChatClient(NullLogger<MockChatClient>.Instance);

        var actions = await service.GetInboxActionsAsync("988656934325292", chatClient, 10, TestContext.Current.CancellationToken);

        Assert.Equal(3, actions.Count);

        // c1: No phone in record or snippet -> asks for SĐT
        Assert.Equal("c1", actions[0].Id);
        Assert.Equal("Nguyễn Văn A", actions[0].CustomerName);
        Assert.Equal("Shop ơi tư vấn size L giúp mình", actions[0].Snippet);
        Assert.Null(actions[0].CustomerPhone);
        Assert.False(string.IsNullOrWhiteSpace(actions[0].SuggestedReply));
        Assert.Contains("SĐT", actions[0].SuggestedReply);

        // c2: Phone already in record -> acks phone, does NOT ask for SĐT
        Assert.Equal("c2", actions[1].Id);
        Assert.Equal("Trần Thị B", actions[1].CustomerName);
        Assert.Equal("0987654321", actions[1].CustomerPhone);
        Assert.Equal("sales_alice", actions[1].AssignedToActor);
        Assert.False(string.IsNullOrWhiteSpace(actions[1].SuggestedReply));
        Assert.Contains("0987654321", actions[1].SuggestedReply);
        Assert.DoesNotContain("xin SĐT", actions[1].SuggestedReply);

        // c3: Phone in snippet -> extracts phone, acks phone, does NOT ask for SĐT
        Assert.Equal("c3", actions[2].Id);
        Assert.Equal("Lê Văn C", actions[2].CustomerName);
        Assert.Equal("0909123456", actions[2].CustomerPhone);
        Assert.Equal("sales_alice", actions[2].AssignedToActor);
        Assert.False(string.IsNullOrWhiteSpace(actions[2].SuggestedReply));
        Assert.Contains("0909123456", actions[2].SuggestedReply);
        Assert.DoesNotContain("xin SĐT", actions[2].SuggestedReply);
    }
}
