namespace DXOS.Application;

public sealed record PagePostData(
    string PostId,
    string? Message,
    DateTimeOffset? CreatedTimeUtc,
    long ReactionCount,
    long CommentCount,
    long ShareCount,
    long Impressions,
    long EngagedUsers,
    long Clicks,
    string DataFreshness);

public sealed record UnrepliedConversationData(
    string Id,
    string? CustomerName,
    string? Snippet,
    string? CustomerPhone,
    string? AssignedToActor);

public sealed record PageHealthData(
    string PageId,
    string? PageName,
    long? FanCount,
    long? FollowersCount,
    IReadOnlyList<PagePostData> Posts,
    int TotalConversations,
    int UnrepliedConversations,
    int ConversationsWithPhone,
    int TotalLeads,
    int HotLeads,
    int WarmLeads,
    bool CommentsPermissionForbidden,
    bool InsightsPartialOrForbidden,
    string CommentsStatus = "unknown");

public interface IPageHealthStore
{
    Task<PageHealthData> GetHealthDataAsync(string pageId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UnrepliedConversationData>> GetUnrepliedConversationsAsync(string pageId, int limit = 10, CancellationToken cancellationToken = default);
}
