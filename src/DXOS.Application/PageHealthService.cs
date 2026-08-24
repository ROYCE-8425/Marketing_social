using System.Text.Json;
using DXOS.Application.Abstractions;
using DXOS.Domain;

namespace DXOS.Application;

public sealed record PageAdviceResponse(
    PageEvaluationResult Evaluation,
    JsonElement Recommendations,
    string RawAdvisory);

public sealed record InboxActionItem(
    string Id,
    string? CustomerName,
    string? Snippet,
    string? CustomerPhone,
    string? AssignedToActor,
    string SuggestedReply);

public sealed class PageHealthService
{
    private readonly IPageHealthStore _store;
    private readonly IClock _clock;

    public PageHealthService(IPageHealthStore store, IClock clock)
    {
        _store = store;
        _clock = clock;
    }

    public async Task<PageEvaluationResult> EvaluatePageHealthAsync(string pageId, CancellationToken cancellationToken = default)
    {
        var (eval, _) = await GetHealthEvaluationWithStatusAsync(pageId, cancellationToken);
        return eval;
    }

    public async Task<(PageEvaluationResult Evaluation, string CommentsStatus)> GetHealthEvaluationWithStatusAsync(string pageId, CancellationToken cancellationToken = default)
    {
        var data = await _store.GetHealthDataAsync(pageId, cancellationToken);
        var postEvidences = data.Posts.Select(p => new PagePostEvidence(
            p.PostId,
            p.Message,
            p.CreatedTimeUtc,
            p.ReactionCount,
            p.CommentCount,
            p.ShareCount,
            p.Impressions,
            p.EngagedUsers,
            p.Clicks,
            p.DataFreshness
        )).ToList();

        var evidence = new PageEvaluationEvidence(
            data.PageId,
            data.PageName,
            data.FanCount,
            data.FollowersCount,
            postEvidences,
            data.TotalConversations,
            data.UnrepliedConversations,
            data.ConversationsWithPhone,
            data.TotalLeads,
            data.HotLeads,
            data.WarmLeads,
            data.CommentsPermissionForbidden,
            data.InsightsPartialOrForbidden);

        var eval = PageEvaluation.Evaluate(evidence, _clock.UtcNow);
        return (eval, data.CommentsStatus);
    }

    public async Task<PageAdviceResponse> GetPageAdviceAsync(
        string pageId,
        IChatClient chatClient,
        CancellationToken cancellationToken = default)
    {
        var evaluation = await EvaluatePageHealthAsync(pageId, cancellationToken);

        var systemPrompt = "Bạn là cố vấn, không được đăng bài / chi tiền / xóa. Chỉ đề xuất.";
        var userPrompt = $"Đánh giá Fanpage (ID: {pageId}): Điểm {evaluation.OverallScore}/100, Trạng thái: {evaluation.Label}. Lý do: {string.Join("; ", evaluation.Reasons)}";

        var responseJson = await chatClient.CompleteAsync(systemPrompt, userPrompt, cancellationToken);

        JsonElement recEl;
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            recEl = doc.RootElement.Clone();
        }
        catch
        {
            using var fallbackDoc = JsonDocument.Parse("{}");
            recEl = fallbackDoc.RootElement.Clone();
        }

        return new PageAdviceResponse(evaluation, recEl, responseJson);
    }

    public async Task<IReadOnlyList<InboxActionItem>> GetInboxActionsAsync(
        string pageId,
        IChatClient chatClient,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var unreplied = await _store.GetUnrepliedConversationsAsync(pageId, limit, cancellationToken);
        var results = new List<InboxActionItem>();

        foreach (var conv in unreplied)
        {
            var phone = conv.CustomerPhone;
            if (string.IsNullOrWhiteSpace(phone))
            {
                phone = PhoneExtractor.ExtractFirstPhoneNumber(conv.Snippet);
            }

            string suggestedReply;
            if (!string.IsNullOrWhiteSpace(phone))
            {
                suggestedReply = $"Dạ Royce Shop đã nhận thông tin và sẽ liên hệ tư vấn trực tiếp qua số {phone} cho bạn ngay nhé ạ!";
                try
                {
                    var systemPrompt = "Bạn là trợ lý tư vấn Royce Shop. Khách đã để lại số điện thoại. Soạn 1 tin xác nhận ngắn, hẹn liên hệ qua số điện thoại đó, không xin lại số điện thoại, không hứa giá/kho.";
                    var userPrompt = $"Khách hàng {conv.CustomerName ?? "Ẩn danh"} (SĐT: {phone}) nhắn tin: \"{conv.Snippet ?? ""}\". Hãy soạn 1 tin xác nhận ngắn hẹn gọi/nhắn qua số {phone}.";
                    var draft = await chatClient.CompleteAsync(systemPrompt, userPrompt, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(draft))
                    {
                        suggestedReply = draft.Trim();
                    }
                }
                catch
                {
                    // Fallback
                }
            }
            else
            {
                suggestedReply = "Dạ Royce Shop chào bạn! Để shop tư vấn chuẩn size và gửi ưu đãi tốt nhất, bạn cho shop xin SĐT/Zalo để chuyên viên liên hệ hỗ trợ ngay nhé ạ!";
                try
                {
                    var systemPrompt = "Bạn là trợ lý tư vấn Royce Shop, chỉ soạn tin ngắn xin SĐT/Zalo, không cam kết giá hoặc kho hàng.";
                    var userPrompt = $"Khách hàng {conv.CustomerName ?? "Ẩn danh"} nhắn tin: \"{conv.Snippet ?? ""}\". Hãy soạn 1 tin trả lời ngắn, xin SĐT, không hứa giá/kho.";
                    var draft = await chatClient.CompleteAsync(systemPrompt, userPrompt, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(draft))
                    {
                        suggestedReply = draft.Trim();
                    }
                }
                catch
                {
                    // Fallback
                }
            }

            results.Add(new InboxActionItem(
                conv.Id,
                conv.CustomerName,
                conv.Snippet,
                phone,
                conv.AssignedToActor,
                suggestedReply));
        }

        return results;
    }
}
