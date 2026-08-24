using System.Text.Json;
using DXOS.Application;
using DXOS.Application.Abstractions;
using DXOS.Domain;
using DXOS.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DXOS.Unit.Tests;

public sealed class PageAgentServiceTests
{
    private sealed class MockHealthStore : IPageHealthStore
    {
        public Task<PageHealthData> GetHealthDataAsync(string pageId, CancellationToken cancellationToken = default)
        {
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new PageHealthData(
                PageId: pageId,
                PageName: "Royce Shop",
                FanCount: 200,
                FollowersCount: 250,
                Posts: new[]
                {
                    new PagePostData("post_1", "Ưu đãi hot hôm nay!", now.AddDays(-1), 10, 2, 1, 300, 40, 15, "fresh")
                },
                TotalConversations: 5,
                UnrepliedConversations: 2,
                ConversationsWithPhone: 1,
                TotalLeads: 3,
                HotLeads: 1,
                WarmLeads: 1,
                CommentsPermissionForbidden: false,
                InsightsPartialOrForbidden: false,
                CommentsStatus: "ok"
            ));
        }

        public Task<IReadOnlyList<UnrepliedConversationData>> GetUnrepliedConversationsAsync(string pageId, int limit = 10, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<UnrepliedConversationData> list = new[]
            {
                new UnrepliedConversationData("c1", "Nguyễn Văn A", "Tư vấn áo giúp mình", "0901234567", null),
                new UnrepliedConversationData("c2", "Trần Thị B", "Shop còn size M không ạ", null, "sales_alice")
            };
            return Task.FromResult(list);
        }
    }

    private sealed class QueueChatClient : IChatClient
    {
        private readonly Queue<string> _agentResponses;

        public QueueChatClient(IEnumerable<string> agentResponses)
        {
            _agentResponses = new Queue<string>(agentResponses);
        }

        public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
        {
            if (systemPrompt.Contains("Bạn là trợ lý tư vấn", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult("Dạ Royce Shop sẽ liên hệ qua số điện thoại để hỗ trợ bạn ngay ạ!");
            }

            if (_agentResponses.Count > 0)
            {
                return Task.FromResult(_agentResponses.Dequeue());
            }

            return Task.FromResult("""{"summary":"Fallback","focus":"data","actions":[],"disclaimer":"AI không tự đăng bài, không tự gửi tin, không chi tiền."}""");
        }
    }

    [Fact]
    public async Task RunAsync_WithMockChatClient_ReturnsAutoExecuteFalseAndDisclaimer()
    {
        var store = new MockHealthStore();
        var clock = new SystemClock();
        var healthService = new PageHealthService(store, clock);
        var agentService = new PageAgentService(store, healthService, clock);
        var chatClient = new MockChatClient(NullLogger<MockChatClient>.Instance);

        var result = await agentService.RunAsync("988656934325292", chatClient, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotNull(result.Agent);
        Assert.NotEmpty(result.Agent.Summary);
        Assert.Equal("inbox", result.Agent.Focus);
        Assert.Equal("AI không tự đăng bài, không tự gửi tin, không chi tiền.", result.Agent.Disclaimer);
        Assert.NotEmpty(result.Agent.Actions);
        Assert.NotEmpty(result.ToolTrace);
        Assert.Contains("page_health", result.ToolTrace);

        foreach (var action in result.Agent.Actions)
        {
            Assert.False(action.AutoExecute, "Every proposed action MUST have autoExecute = false");
            Assert.NotEmpty(action.Id);
            Assert.NotEmpty(action.Title);
            Assert.NotEmpty(action.RequiresPermission);
        }
    }

    [Fact]
    public async Task RunAsync_WithMultiStepToolCallThenFinal_ReturnsToolTraceAndActions()
    {
        var store = new MockHealthStore();
        var clock = new SystemClock();
        var healthService = new PageHealthService(store, clock);
        var agentService = new PageAgentService(store, healthService, clock);

        var responses = new[]
        {
            """{"tool": "inbox_unreplied", "args": {}}""",
            """
            {
              "summary": "Đã xem inbox, cần xử lý 2 hội thoại.",
              "focus": "inbox",
              "actions": [
                {
                  "id": "a1",
                  "type": "reply_inbox",
                  "title": "Trả lời tin nhắn khách",
                  "rationale": "Khách để lại số điện thoại",
                  "payload": {
                    "conversationId": "c1",
                    "suggestedReply": "Dạ shop sẽ liên hệ qua 0901234567 ạ!"
                  },
                  "requiresPermission": "inbox.reply",
                  "autoExecute": false
                }
              ],
              "disclaimer": "AI không tự đăng bài, không tự gửi tin, không chi tiền."
            }
            """
        };

        var chatClient = new QueueChatClient(responses);
        var result = await agentService.RunAsync("988656934325292", chatClient, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Single(result.ToolTrace);
        Assert.Equal("inbox_unreplied", result.ToolTrace[0]);
        Assert.Single(result.Agent.Actions);
        Assert.False(result.Agent.Actions[0].AutoExecute);
        Assert.Equal("reply_inbox", result.Agent.Actions[0].Type);
    }

    [Fact]
    public async Task RunAsync_WithUnknownToolName_DoesNotThrowAndCompletes()
    {
        var store = new MockHealthStore();
        var clock = new SystemClock();
        var healthService = new PageHealthService(store, clock);
        var agentService = new PageAgentService(store, healthService, clock);

        var responses = new[]
        {
            """{"tool": "unknown_forbidden_tool", "args": {}}""",
            """
            {
              "summary": "Tóm tắt sau khi gọi công cụ lạ.",
              "focus": "data",
              "actions": [
                {
                  "id": "a1",
                  "type": "wait",
                  "title": "Chờ xác nhận",
                  "rationale": "Không có thao tác khẩn cấp",
                  "requiresPermission": "page.posts.read",
                  "autoExecute": false
                }
              ],
              "disclaimer": "AI không tự đăng bài, không tự gửi tin, không chi tiền."
            }
            """
        };

        var chatClient = new QueueChatClient(responses);
        var result = await agentService.RunAsync("988656934325292", chatClient, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Single(result.ToolTrace);
        Assert.Equal("unknown_forbidden_tool", result.ToolTrace[0]);
        Assert.Single(result.Agent.Actions);
        Assert.False(result.Agent.Actions[0].AutoExecute);
    }

    [Fact]
    public async Task RunAsync_WithMoreThan3ToolCalls_StopsAtMaxRoundsAndSynthesizes()
    {
        var store = new MockHealthStore();
        var clock = new SystemClock();
        var healthService = new PageHealthService(store, clock);
        var agentService = new PageAgentService(store, healthService, clock);

        var responses = new[]
        {
            """{"tool": "page_health", "args": {}}""",
            """{"tool": "inbox_unreplied", "args": {}}""",
            """{"tool": "list_posts", "args": {}}""",
            """
            {
              "summary": "Tóm tắt cuối cùng sau 3 lượt công cụ.",
              "focus": "content",
              "actions": [
                {
                  "id": "a1",
                  "type": "compose_post",
                  "title": "Đăng bài mới",
                  "rationale": "Kêu gọi hành động",
                  "requiresPermission": "page.publish",
                  "autoExecute": false
                }
              ],
              "disclaimer": "AI không tự đăng bài, không tự gửi tin, không chi tiền."
            }
            """
        };

        var chatClient = new QueueChatClient(responses);
        var result = await agentService.RunAsync("988656934325292", chatClient, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(3, result.ToolTrace.Count);
        Assert.Equal("page_health", result.ToolTrace[0]);
        Assert.Equal("inbox_unreplied", result.ToolTrace[1]);
        Assert.Equal("list_posts", result.ToolTrace[2]);
        Assert.Single(result.Agent.Actions);
        Assert.False(result.Agent.Actions[0].AutoExecute);
    }

    [Fact]
    public void ParseAgentResponse_WithMarkdownCodeFences_ParsesSuccessfully()
    {
        var fencedJson = """
        ```json
        {
          "summary": "Fanpage hoạt động ổn định nhưng cần phản hồi 2 tin nhắn gấp.",
          "focus": "inbox",
          "actions": [
            {
              "id": "a1",
              "type": "reply_inbox",
              "title": "Trả lời tin nhắn khách",
              "rationale": "Khách để lại số điện thoại",
              "payload": {
                "conversationId": "c1",
                "suggestedReply": "Dạ shop sẽ liên hệ ngay qua số 0901234567 ạ!"
              },
              "requiresPermission": "inbox.reply",
              "autoExecute": false
            }
          ],
          "disclaimer": "AI không tự đăng bài, không tự gửi tin, không chi tiền."
        }
        ```
        """;

        var parsed = PageAgentService.ParseAgentResponse(fencedJson);

        Assert.NotNull(parsed);
        Assert.Equal("Fanpage hoạt động ổn định nhưng cần phản hồi 2 tin nhắn gấp.", parsed.Summary);
        Assert.Equal("inbox", parsed.Focus);
        Assert.Single(parsed.Actions);
        Assert.Equal("a1", parsed.Actions[0].Id);
        Assert.Equal("reply_inbox", parsed.Actions[0].Type);
        Assert.Equal("0901234567", parsed.Actions[0].Payload?.SuggestedReply?.Split(" ")[^2]);
        Assert.False(parsed.Actions[0].AutoExecute);
    }

    [Fact]
    public void ParseAgentResponse_WithInvalidJson_ReturnsFallbackWithWaitActionAndDisclaimer()
    {
        var invalidText = "Xin chào tôi là AI, tôi nghĩ bạn nên đăng thêm bài viết.";

        var parsed = PageAgentService.ParseAgentResponse(invalidText);

        Assert.NotNull(parsed);
        Assert.Contains("Xin chào", parsed.Summary);
        Assert.Equal("data", parsed.Focus);
        Assert.Equal("AI không tự đăng bài, không tự gửi tin, không chi tiền.", parsed.Disclaimer);
        Assert.Single(parsed.Actions);
        Assert.Equal("wait", parsed.Actions[0].Type);
        Assert.False(parsed.Actions[0].AutoExecute);
    }

    [Fact]
    public void ParseAgentResponse_EnforcesAutoExecuteFalse_EvenIfJsonHasTrue()
    {
        var jsonWithTrue = """
        {
          "summary": "Tóm tắt kiểm tra bảo mật.",
          "focus": "content",
          "actions": [
            {
              "id": "a1",
              "type": "compose_post",
              "title": "Đăng bài tự động",
              "rationale": "Test invariant",
              "requiresPermission": "page.publish",
              "autoExecute": true
            }
          ],
          "disclaimer": "AI không tự đăng bài, không tự gửi tin, không chi tiền."
        }
        """;

        var parsed = PageAgentService.ParseAgentResponse(jsonWithTrue);

        Assert.NotNull(parsed);
        Assert.Single(parsed.Actions);
        Assert.False(parsed.Actions[0].AutoExecute, "AutoExecute MUST be strictly false regardless of LLM output.");
    }

    [Fact]
    public void ParseAgentResponse_DropsForbiddenActionTypes()
    {
        var jsonWithForbiddenType = """
        {
          "summary": "Tóm tắt thử nghiệm.",
          "focus": "content",
          "actions": [
            {
              "id": "a1",
              "type": "publish_post_directly_to_graph",
              "title": "Hành động cấm",
              "rationale": "Thử nghiệm cấm",
              "requiresPermission": "page.publish",
              "autoExecute": false
            }
          ],
          "disclaimer": "AI không tự đăng bài, không tự gửi tin, không chi tiền."
        }
        """;

        var parsed = PageAgentService.ParseAgentResponse(jsonWithForbiddenType);

        Assert.NotNull(parsed);
        Assert.Single(parsed.Actions);
        Assert.Equal("wait", parsed.Actions[0].Type);
    }

    [Fact]
    public void ParseAgentResponse_LimitsActionsToMax5()
    {
        var actionsJson = string.Join(",", Enumerable.Range(1, 8).Select(i => $$"""
        {
          "id": "a{{i}}",
          "type": "compose_post",
          "title": "Action {{i}}",
          "rationale": "Rationale {{i}}",
          "requiresPermission": "page.publish",
          "autoExecute": false
        }
        """));

        var jsonWith8Actions = $$"""
        {
          "summary": "Tóm tắt với nhiều hành động.",
          "focus": "content",
          "actions": [ {{actionsJson}} ],
          "disclaimer": "AI không tự đăng bài, không tự gửi tin, không chi tiền."
        }
        """;

        var parsed = PageAgentService.ParseAgentResponse(jsonWith8Actions);

        Assert.NotNull(parsed);
        Assert.Equal(5, parsed.Actions.Count);
    }

    [Fact]
    public void AgentRun_RequiresPagePostsRead_SalesLacksPermission_OwnerAndViewerHavePermission()
    {
        var salesPerms = AppPermissions.SeedRoles["Sales"];
        var ownerPerms = AppPermissions.SeedRoles["Owner"];
        var viewerPerms = AppPermissions.SeedRoles["Viewer"];

        Assert.DoesNotContain(AppPermissions.PagePostsRead, salesPerms);
        Assert.Contains(AppPermissions.PagePostsRead, ownerPerms);
        Assert.Contains(AppPermissions.PagePostsRead, viewerPerms);
    }
}
