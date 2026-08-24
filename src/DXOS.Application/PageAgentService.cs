using System.Text.Json;
using DXOS.Application.Abstractions;
using DXOS.Domain;

namespace DXOS.Application;

public sealed record PageAgentActionPayload(
    string? ConversationId = null,
    string? SuggestedReply = null,
    string? SuggestedPost = null);

public sealed record PageAgentAction(
    string Id,
    string Type, // "reply_inbox", "compose_post", "sync_page", "ask_owner", "wait"
    string Title,
    string Rationale,
    PageAgentActionPayload? Payload,
    string RequiresPermission, // "inbox.reply", "page.publish", "page.posts.read"
    bool AutoExecute = false);

public sealed record PageAgentResult(
    string Summary,
    string Focus, // "inbox", "content", "leads", "engagement", "data"
    IReadOnlyList<PageAgentAction> Actions,
    string Disclaimer);

public sealed record PageAgentRunResponse(
    PageAgentResult Agent,
    PageEvaluationResult Evaluation,
    string CommentsStatus,
    IReadOnlyList<string> ToolTrace);

public sealed class PageAgentService
{
    private static readonly HashSet<string> AllowedActionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "reply_inbox",
        "compose_post",
        "sync_page",
        "ask_owner",
        "wait"
    };

    private readonly IPageHealthStore _store;
    private readonly PageHealthService _healthService;
    private readonly IClock _clock;

    public PageAgentService(IPageHealthStore store, PageHealthService healthService, IClock clock)
    {
        _store = store;
        _healthService = healthService;
        _clock = clock;
    }

    public async Task<PageAgentRunResponse> RunAsync(
        string pageId,
        IChatClient chatClient,
        CancellationToken cancellationToken = default)
    {
        var toolTrace = new List<string>();
        var executedToolKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var (initialEval, initialCommentsStatus) = await _healthService.GetHealthEvaluationWithStatusAsync(pageId, cancellationToken);
        var currentEval = initialEval;
        var currentCommentsStatus = initialCommentsStatus;

        var systemPrompt = """
Bạn là Facebook Page Operator Agent chuyên gia đồng hành cho Fanpage Royce Shop.
Vòng lặp vận hành: Bạn có thể yêu cầu tối đa 3 lượt gọi công cụ để thu thập thông tin, sau đó ĐỀ XUẤT tối đa 5 hành động khả thi cho chủ shop/nhân viên.

Công cụ khả dụng (CHỈ ĐỌC / ĐỀ XUẤT):
1. {"tool": "page_health", "args": {}} - Xem điểm sức khỏe 4 trục, lý do đánh giá và trạng thái quyền bình luận.
2. {"tool": "inbox_unreplied", "args": {}} - Xem các hội thoại chưa trả lời kèm SĐT và tin nhắn mẫu.
3. {"tool": "list_posts", "args": {}} - Xem 5 bài viết gần nhất và chỉ số tương tác thực tế.
4. {"tool": "draft_inbox", "args": {"conversationId": "..."}} - Lấy tin nhắn mẫu cho hội thoại cụ thể.

Để gọi công cụ, trả về duy nhất JSON:
{"tool": "<name>", "args": { ... }}

Khi đã đủ thông tin, trả về kết quả cuối cùng theo JSON contract (bắt buộc autoExecute: false, tối đa 5 hành động):
{
  "summary": "1-3 câu tiếng Việt tóm tắt tình trạng và ưu tiên hàng đầu.",
  "focus": "inbox" | "content" | "leads" | "engagement" | "data",
  "actions": [
    {
      "id": "a1",
      "type": "reply_inbox" | "compose_post" | "sync_page" | "ask_owner" | "wait",
      "title": "Tiêu đề hành động ngắn gọn",
      "rationale": "Lý do vì sao đề xuất hành động này",
      "payload": {
        "conversationId": "id_hoi_thoai_neu_co",
        "suggestedReply": "noi_dung_tin_nhan_mau_neu_co",
        "suggestedPost": "noi_dung_bai_viet_mau_neu_co"
      },
      "requiresPermission": "inbox.reply" | "page.publish" | "page.posts.read",
      "autoExecute": false
    }
  ],
  "disclaimer": "AI không tự đăng bài, không tự gửi tin, không chi tiền."
}

Quy tắc bất biến:
1. Bạn KHÔNG được tự đăng bài, tự gửi tin, tự chi tiền hay tự xóa. Mọi hành động có autoExecute: false.
2. Các action type hợp lệ: reply_inbox, compose_post, sync_page, ask_owner, wait.
3. Nếu commentsStatus không phải "ok", không được đề xuất trả lời bình luận công khai.
""";

        var conversationPrompt = $"Trang mục tiêu: {pageId} (Royce Shop). Hãy sử dụng công cụ nếu cần thêm thông tin, hoặc trả về JSON kết quả đề xuất vận hành:";

        PageAgentResult? finalResult = null;

        for (int round = 1; round <= 3; round++)
        {
            var responseText = await chatClient.CompleteAsync(systemPrompt, conversationPrompt, cancellationToken);
            var clean = StripMarkdownFences(responseText?.Trim() ?? string.Empty);

            if (IsToolCall(clean, out var toolName, out var argsJson))
            {
                var toolKey = $"{toolName}:{argsJson}";
                if (!executedToolKeys.Add(toolKey))
                {
                    conversationPrompt += $"\n\n[Lưu ý] Công cụ '{toolName}' với đối số này đã được thực thi trước đó. Vui lòng tổng hợp và trả về JSON kết quả cuối cùng:";
                    continue;
                }

                toolTrace.Add(toolName);
                var toolOutput = await ExecuteToolAsync(pageId, toolName, argsJson, chatClient, cancellationToken);

                if (string.Equals(toolName, "page_health", StringComparison.OrdinalIgnoreCase))
                {
                    var (eval, cStatus) = await _healthService.GetHealthEvaluationWithStatusAsync(pageId, cancellationToken);
                    currentEval = eval;
                    currentCommentsStatus = cStatus;
                }

                conversationPrompt += $"\n\n[Kết quả công cụ '{toolName}']:\n{toolOutput}\nHãy tiếp tục gọi công cụ khác hoặc trả về JSON kết quả cuối cùng:";
            }
            else
            {
                finalResult = ParseAgentResponse(clean);
                break;
            }
        }

        if (finalResult is null)
        {
            var finalAttempt = await chatClient.CompleteAsync(
                systemPrompt,
                conversationPrompt + "\n\nĐã hết số lượt gọi công cụ (3 lượt). Vui lòng trả về ngay JSON kết quả đề xuất cuối cùng:",
                cancellationToken);

            finalResult = ParseAgentResponse(finalAttempt);
        }

        return new PageAgentRunResponse(finalResult, currentEval, currentCommentsStatus, toolTrace);
    }

    private async Task<string> ExecuteToolAsync(
        string pageId,
        string toolName,
        string? argsJson,
        IChatClient chatClient,
        CancellationToken ct)
    {
        try
        {
            switch (toolName.ToLowerInvariant())
            {
                case "page_health":
                    {
                        var (eval, commentsStatus) = await _healthService.GetHealthEvaluationWithStatusAsync(pageId, ct);
                        var obj = new
                        {
                            overallScore = eval.OverallScore,
                            label = eval.Label,
                            axes = eval.Axes,
                            reasons = eval.Reasons,
                            commentsStatus
                        };
                        return JsonSerializer.Serialize(obj);
                    }
                case "inbox_unreplied":
                    {
                        var unreplied = await _healthService.GetInboxActionsAsync(pageId, chatClient, 5, ct);
                        var list = unreplied.Select(u => new
                        {
                            u.Id,
                            u.CustomerName,
                            Snippet = Truncate(u.Snippet, 200),
                            u.CustomerPhone,
                            u.SuggestedReply
                        }).ToList();
                        return JsonSerializer.Serialize(list);
                    }
                case "list_posts":
                    {
                        var healthData = await _store.GetHealthDataAsync(pageId, ct);
                        var posts = healthData.Posts.Take(5).Select(p => new
                        {
                            p.PostId,
                            Message = Truncate(p.Message, 200),
                            p.ReactionCount,
                            p.CommentCount,
                            p.ShareCount,
                            MediaType = "post",
                            p.DataFreshness
                        }).ToList();
                        return JsonSerializer.Serialize(posts);
                    }
                case "draft_inbox":
                    {
                        string? targetConvId = null;
                        if (!string.IsNullOrWhiteSpace(argsJson))
                        {
                            try
                            {
                                using var doc = JsonDocument.Parse(argsJson);
                                if (doc.RootElement.TryGetProperty("conversationId", out var cEl))
                                {
                                    targetConvId = cEl.GetString();
                                }
                            }
                            catch { }
                        }

                        var unreplied = await _healthService.GetInboxActionsAsync(pageId, chatClient, 5, ct);
                        var matched = unreplied.FirstOrDefault(u => string.Equals(u.Id, targetConvId, StringComparison.OrdinalIgnoreCase))
                                      ?? unreplied.FirstOrDefault();

                        if (matched != null)
                        {
                            return JsonSerializer.Serialize(new
                            {
                                conversationId = matched.Id,
                                customerName = matched.CustomerName,
                                suggestedReply = matched.SuggestedReply
                            });
                        }

                        return JsonSerializer.Serialize(new { message = "Không tìm thấy hội thoại chưa trả lời." });
                    }
                default:
                    return JsonSerializer.Serialize(new { error = $"Công cụ '{toolName}' không được hỗ trợ hoặc bị cấm." });
            }
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Lỗi khi thực thi công cụ '{toolName}': {ex.Message}" });
        }
    }

    private static string? Truncate(string? str, int maxLen)
    {
        if (string.IsNullOrEmpty(str)) return str;
        return str.Length <= maxLen ? str : str[..maxLen] + "...";
    }

    public static bool IsToolCall(string json, out string toolName, out string? argsJson)
    {
        toolName = string.Empty;
        argsJson = null;

        if (string.IsNullOrWhiteSpace(json)) return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("tool", out var toolEl) &&
                toolEl.ValueKind == JsonValueKind.String)
            {
                toolName = toolEl.GetString() ?? string.Empty;
                if (root.TryGetProperty("args", out var argsEl))
                {
                    argsJson = argsEl.GetRawText();
                }
                return !string.IsNullOrWhiteSpace(toolName);
            }
        }
        catch { }

        return false;
    }

    public static PageAgentResult ParseAgentResponse(string? responseText)
    {
        const string defaultDisclaimer = "AI không tự đăng bài, không tự gửi tin, không chi tiền.";

        if (string.IsNullOrWhiteSpace(responseText))
        {
            return new PageAgentResult(
                "Chưa nhận được phản hồi từ AI Agent.",
                "data",
                [new PageAgentAction("a1", "wait", "Chờ phản hồi", "Chưa có dữ liệu từ AI", null, "page.posts.read", false)],
                defaultDisclaimer);
        }

        var clean = StripMarkdownFences(responseText.Trim());

        try
        {
            using var doc = JsonDocument.Parse(clean);
            var root = doc.RootElement;

            var summary = root.TryGetProperty("summary", out var sEl) ? sEl.GetString() : null;
            summary ??= "Phân tích vận hành Fanpage từ AI Agent.";

            var focus = root.TryGetProperty("focus", out var fEl) ? fEl.GetString() : null;
            focus = string.IsNullOrWhiteSpace(focus) ? "inbox" : focus.ToLowerInvariant();

            var actions = new List<PageAgentAction>();
            if (root.TryGetProperty("actions", out var aEl) && aEl.ValueKind == JsonValueKind.Array)
            {
                int idx = 1;
                foreach (var item in aEl.EnumerateArray())
                {
                    if (actions.Count >= 5) break;

                    var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() : $"a{idx}";
                    var type = item.TryGetProperty("type", out var tEl) ? tEl.GetString() : "wait";

                    // Enforce allowed action types
                    if (string.IsNullOrWhiteSpace(type) || !AllowedActionTypes.Contains(type))
                    {
                        continue;
                    }

                    var title = item.TryGetProperty("title", out var titleEl) ? titleEl.GetString() : "Hành động đề xuất";
                    var rationale = item.TryGetProperty("rationale", out var rEl) ? rEl.GetString() : string.Empty;
                    var reqPerm = item.TryGetProperty("requiresPermission", out var pEl) ? pEl.GetString() : "page.posts.read";

                    PageAgentActionPayload? payload = null;
                    if (item.TryGetProperty("payload", out var plEl) && plEl.ValueKind == JsonValueKind.Object)
                    {
                        var convId = plEl.TryGetProperty("conversationId", out var cIdEl) ? cIdEl.GetString() : null;
                        var sugReply = plEl.TryGetProperty("suggestedReply", out var srEl) ? srEl.GetString() : null;
                        var sugPost = plEl.TryGetProperty("suggestedPost", out var spEl) ? spEl.GetString() : null;
                        payload = new PageAgentActionPayload(convId, sugReply, sugPost);
                    }

                    actions.Add(new PageAgentAction(
                        id ?? $"a{idx}",
                        type,
                        title ?? "Hành động đề xuất",
                        rationale ?? string.Empty,
                        payload,
                        reqPerm ?? "page.posts.read",
                        false)); // Invariant: ALWAYS false

                    idx++;
                }
            }

            if (actions.Count == 0)
            {
                actions.Add(new PageAgentAction("a1", "wait", "Theo dõi hoạt động", "Chưa có hành động khẩn cấp", null, "page.posts.read", false));
            }

            return new PageAgentResult(summary, focus, actions, defaultDisclaimer);
        }
        catch
        {
            return new PageAgentResult(
                clean.Length > 200 ? clean[..200] + "..." : clean,
                "data",
                [new PageAgentAction("a1", "wait", "Chờ xác nhận", "Phản hồi AI không đúng định dạng JSON chuẩn", null, "page.posts.read", false)],
                defaultDisclaimer);
        }
    }

    public static string StripMarkdownFences(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[7..].Trim();
        }
        else if (trimmed.StartsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[3..].Trim();
        }

        if (trimmed.EndsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^3].Trim();
        }
        return trimmed;
    }
}
