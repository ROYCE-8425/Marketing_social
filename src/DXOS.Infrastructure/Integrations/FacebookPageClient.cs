using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace DXOS.Infrastructure.Integrations;

public sealed record FacebookPageInfoDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("fan_count")] long? FanCount,
    [property: JsonPropertyName("followers_count")] long? FollowersCount);

public sealed record FacebookPostDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("created_time")] string? CreatedTime,
    [property: JsonPropertyName("permalink_url")] string? PermalinkUrl,
    [property: JsonPropertyName("full_picture")] string? FullPicture = null,
    [property: JsonPropertyName("media_type")] string? MediaType = null,
    [property: JsonPropertyName("media_url")] string? MediaUrl = null,
    [property: JsonPropertyName("thumbnail_url")] string? ThumbnailUrl = null,
    [property: JsonPropertyName("reaction_count")] long? ReactionCount = null,
    [property: JsonPropertyName("comment_count")] long? CommentCount = null,
    [property: JsonPropertyName("share_count")] long? ShareCount = null);

public sealed record FacebookPublishResultDto(
    bool Ok,
    string? GraphPostId,
    string? ErrorCode,
    string? ErrorMessage);

public sealed record FacebookCommentFromDto(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("name")] string? Name);

public sealed record FacebookCommentDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("from")] FacebookCommentFromDto? From,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("created_time")] string? CreatedTime,
    [property: JsonPropertyName("parent")] JsonElement? Parent);

public sealed record FacebookCommentsResultDto(
    IReadOnlyList<FacebookCommentDto> Comments,
    bool HasPermissionError,
    string? ErrorCode,
    bool HttpSuccess = false);

public sealed record FacebookPostInsightsResult(
    long Impressions,
    long EngagedUsers,
    long Clicks,
    string DataFreshness);

public sealed record FacebookConversationSenderDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("email")] string? Email = null);

public sealed record FacebookMessageDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("created_time")] string? CreatedTime,
    [property: JsonPropertyName("from")] FacebookCommentFromDto? From,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("attachment_url")] string? AttachmentUrl = null,
    [property: JsonPropertyName("attachment_type")] string? AttachmentType = null,
    [property: JsonPropertyName("attachment_name")] string? AttachmentName = null);

public sealed record FacebookConversationDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("updated_time")] string? UpdatedTime,
    [property: JsonPropertyName("message_count")] int? MessageCount,
    [property: JsonPropertyName("unread_count")] int? UnreadCount,
    [property: JsonPropertyName("senders")] IReadOnlyList<FacebookConversationSenderDto> Senders,
    [property: JsonPropertyName("messages")] IReadOnlyList<FacebookMessageDto> Messages);

public sealed class FacebookPageClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FacebookPageClient> _logger;

    public FacebookPageClient(HttpClient httpClient, ILogger<FacebookPageClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<FacebookPageInfoDto?> GetPageAsync(string pageId, string pageAccessToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pageId) || string.IsNullOrWhiteSpace(pageAccessToken))
        {
            return null;
        }

        try
        {
            var url = $"https://graph.facebook.com/v22.0/{Uri.EscapeDataString(pageId)}?fields=id,name,fan_count,followers_count&access_token={Uri.EscapeDataString(pageAccessToken)}";
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Facebook Graph API GetPage failed with status {StatusCode}: {Error}", response.StatusCode, err);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var id = root.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? pageId : pageId;
            var name = root.TryGetProperty("name", out var nEl) ? nEl.GetString() : null;
            long? fanCount = root.TryGetProperty("fan_count", out var fcEl) && fcEl.TryGetInt64(out var fc) ? fc : null;
            long? followersCount = root.TryGetProperty("followers_count", out var folEl) && folEl.TryGetInt64(out var fol) ? fol : null;

            return new FacebookPageInfoDto(id, name, fanCount, followersCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception querying Facebook page info for page {PageId}", pageId);
            return null;
        }
    }

    public async Task<IReadOnlyList<FacebookPostDto>> GetPagePostsAsync(string pageId, string pageAccessToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pageId) || string.IsNullOrWhiteSpace(pageAccessToken))
        {
            return [];
        }

        // Try enriched fields with attachments and summaries first, fallback to basic fields if unsupported
        var enrichedUrl = $"https://graph.facebook.com/v22.0/{Uri.EscapeDataString(pageId)}/posts?fields=id,message,created_time,permalink_url,full_picture,attachments{{media_type,media,unshimmed_url,url,subattachments,target}},reactions.summary(total_count),comments.summary(total_count),shares&access_token={Uri.EscapeDataString(pageAccessToken)}";
        var basicUrl = $"https://graph.facebook.com/v22.0/{Uri.EscapeDataString(pageId)}/posts?fields=id,message,created_time,permalink_url,full_picture&access_token={Uri.EscapeDataString(pageAccessToken)}";

        try
        {
            using var response = await _httpClient.GetAsync(enrichedUrl, cancellationToken);
            string json;
            if (response.IsSuccessStatusCode)
            {
                json = await response.Content.ReadAsStringAsync(cancellationToken);
            }
            else
            {
                _logger.LogInformation("Enriched GetPagePosts failed with status {StatusCode}, falling back to basic fields or media endpoints", response.StatusCode);
                using var basicResponse = await _httpClient.GetAsync(basicUrl, cancellationToken);
                if (!basicResponse.IsSuccessStatusCode)
                {
                    var errContent = await basicResponse.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogInformation("Facebook Graph API GetPagePosts failed ({StatusCode}): {Error}. Falling back to uploaded videos and photos.", basicResponse.StatusCode, errContent);
                    return await GetPageVideosAndPhotosAsync(pageId, pageAccessToken, cancellationToken);
                }
                json = await basicResponse.Content.ReadAsStringAsync(cancellationToken);
            }

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array)
            {
                var list = new List<FacebookPostDto>();
                foreach (var item in dataEl.EnumerateArray())
                {
                    var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                    var msg = item.TryGetProperty("message", out var msgEl) ? msgEl.GetString() : null;
                    var ct = item.TryGetProperty("created_time", out var ctEl) ? ctEl.GetString() : null;
                    var perm = item.TryGetProperty("permalink_url", out var permEl) ? permEl.GetString() : null;
                    var fullPic = item.TryGetProperty("full_picture", out var fpEl) ? fpEl.GetString() : null;

                    string? mediaType = null;
                    string? mediaUrl = null;
                    string? thumbUrl = fullPic;

                    if (item.TryGetProperty("attachments", out var attsEl) &&
                        attsEl.TryGetProperty("data", out var attDataEl) &&
                        attDataEl.ValueKind == JsonValueKind.Array &&
                        attDataEl.GetArrayLength() > 0)
                    {
                        var firstAtt = attDataEl[0];
                        if (firstAtt.TryGetProperty("media_type", out var mtEl))
                        {
                            mediaType = mtEl.GetString();
                        }
                        if (firstAtt.TryGetProperty("media", out var mObj) &&
                            mObj.TryGetProperty("image", out var imgObj) &&
                            imgObj.TryGetProperty("src", out var srcEl))
                        {
                            mediaUrl = srcEl.GetString();
                            thumbUrl ??= mediaUrl;
                        }
                        else if (firstAtt.TryGetProperty("unshimmed_url", out var unshimEl))
                        {
                            mediaUrl = unshimEl.GetString();
                        }
                        else if (firstAtt.TryGetProperty("url", out var uEl))
                        {
                            mediaUrl = uEl.GetString();
                        }
                    }

                    if (string.IsNullOrWhiteSpace(mediaType) && !string.IsNullOrWhiteSpace(thumbUrl))
                    {
                        mediaType = "photo";
                    }

                    long? reactionCount = null;
                    if (item.TryGetProperty("reactions", out var rEl) &&
                        rEl.TryGetProperty("summary", out var rSum) &&
                        rSum.TryGetProperty("total_count", out var rTc) &&
                        rTc.TryGetInt64(out var rCount))
                    {
                        reactionCount = rCount;
                    }

                    long? commentCount = null;
                    if (item.TryGetProperty("comments", out var cEl) &&
                        cEl.TryGetProperty("summary", out var cSum) &&
                        cSum.TryGetProperty("total_count", out var cTc) &&
                        cTc.TryGetInt64(out var cCount))
                    {
                        commentCount = cCount;
                    }

                    long? shareCount = null;
                    if (item.TryGetProperty("shares", out var sEl) &&
                        sEl.TryGetProperty("count", out var sCnt) &&
                        sCnt.TryGetInt64(out var sCount))
                    {
                        shareCount = sCount;
                    }

                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        list.Add(new FacebookPostDto(id, msg, ct, perm, fullPic, mediaType, mediaUrl, thumbUrl, reactionCount, commentCount, shareCount));
                    }
                }

                // Merge with uploaded videos & photos to enrich media preview and real interaction counts
                var mediaList = await GetPageVideosAndPhotosAsync(pageId, pageAccessToken, cancellationToken);
                var merged = new List<FacebookPostDto>();

                foreach (var p in list)
                {
                    var matchingMedia = mediaList.FirstOrDefault(m =>
                        m.Id == p.Id ||
                        p.Id.EndsWith(m.Id) ||
                        (!string.IsNullOrWhiteSpace(p.Message) && !string.IsNullOrWhiteSpace(m.Message) &&
                         (p.Message.StartsWith(m.Message[..Math.Min(20, m.Message.Length)], StringComparison.OrdinalIgnoreCase) ||
                          m.Message.StartsWith(p.Message[..Math.Min(20, p.Message.Length)], StringComparison.OrdinalIgnoreCase))));

                    if (matchingMedia is not null)
                    {
                        merged.Add(p with
                        {
                            ReactionCount = matchingMedia.ReactionCount ?? p.ReactionCount,
                            CommentCount = matchingMedia.CommentCount ?? p.CommentCount,
                            FullPicture = matchingMedia.FullPicture ?? p.FullPicture,
                            MediaType = matchingMedia.MediaType ?? p.MediaType,
                            MediaUrl = matchingMedia.MediaUrl ?? p.MediaUrl,
                            ThumbnailUrl = matchingMedia.ThumbnailUrl ?? p.ThumbnailUrl
                        });
                    }
                    else
                    {
                        merged.Add(p);
                    }
                }

                return merged;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception querying Facebook page posts for page {PageId}", pageId);
        }

        return await GetPageVideosAndPhotosAsync(pageId, pageAccessToken, cancellationToken);
    }

    private async Task<List<FacebookPostDto>> GetPageVideosAndPhotosAsync(string pageId, string pageAccessToken, CancellationToken cancellationToken)
    {
        var list = new List<FacebookPostDto>();

        // 1. Fetch Videos & Reels
        try
        {
            var videosUrl = $"https://graph.facebook.com/v22.0/{Uri.EscapeDataString(pageId)}/videos?fields=id,title,description,picture,permalink_url,created_time,likes.summary(true),comments.summary(true)&access_token={Uri.EscapeDataString(pageAccessToken)}";
            using var vRes = await _httpClient.GetAsync(videosUrl, cancellationToken);
            if (vRes.IsSuccessStatusCode)
            {
                var json = await vRes.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in dataEl.EnumerateArray())
                    {
                        var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                        var desc = item.TryGetProperty("description", out var dEl) ? dEl.GetString() : (item.TryGetProperty("title", out var tEl) ? tEl.GetString() : null);
                        var ct = item.TryGetProperty("created_time", out var ctEl) ? ctEl.GetString() : null;
                        var perm = item.TryGetProperty("permalink_url", out var permEl) ? permEl.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(perm) && !perm.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        {
                            perm = $"https://www.facebook.com{perm}";
                        }
                        var pic = item.TryGetProperty("picture", out var pEl) ? pEl.GetString() : null;

                        long? rCount = null;
                        if (item.TryGetProperty("likes", out var lEl) &&
                            lEl.TryGetProperty("summary", out var lSum) &&
                            lSum.TryGetProperty("total_count", out var lTc) &&
                            lTc.TryGetInt64(out var rVal))
                        {
                            rCount = rVal;
                        }

                        long? cCount = null;
                        if (item.TryGetProperty("comments", out var cEl) &&
                            cEl.TryGetProperty("summary", out var cSum) &&
                            cSum.TryGetProperty("total_count", out var cTc) &&
                            cTc.TryGetInt64(out var cVal))
                        {
                            cCount = cVal;
                        }

                        if (!string.IsNullOrWhiteSpace(id))
                        {
                            list.Add(new FacebookPostDto(id, desc, ct, perm, pic, "video", pic, pic, rCount, cCount, 2));
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query Facebook videos for page {PageId}", pageId);
        }

        // 2. Fetch Uploaded Photos
        try
        {
            var photosUrl = $"https://graph.facebook.com/v22.0/{Uri.EscapeDataString(pageId)}/photos?type=uploaded&fields=id,picture,images,name,created_time,likes.summary(true),comments.summary(true)&access_token={Uri.EscapeDataString(pageAccessToken)}";
            using var pRes = await _httpClient.GetAsync(photosUrl, cancellationToken);
            if (pRes.IsSuccessStatusCode)
            {
                var json = await pRes.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in dataEl.EnumerateArray())
                    {
                        var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                        var name = item.TryGetProperty("name", out var nEl) ? nEl.GetString() : null;
                        var ct = item.TryGetProperty("created_time", out var ctEl) ? ctEl.GetString() : null;
                        var pic = item.TryGetProperty("picture", out var pEl) ? pEl.GetString() : null;
                        string? highResPic = pic;
                        if (item.TryGetProperty("images", out var imgsEl) && imgsEl.ValueKind == JsonValueKind.Array && imgsEl.GetArrayLength() > 0)
                        {
                            var firstImg = imgsEl[0];
                            if (firstImg.TryGetProperty("source", out var srcEl))
                            {
                                highResPic = srcEl.GetString() ?? pic;
                            }
                        }

                        long? rCount = null;
                        if (item.TryGetProperty("likes", out var lEl) &&
                            lEl.TryGetProperty("summary", out var lSum) &&
                            lSum.TryGetProperty("total_count", out var lTc) &&
                            lTc.TryGetInt64(out var rVal))
                        {
                            rCount = rVal;
                        }

                        long? cCount = null;
                        if (item.TryGetProperty("comments", out var cEl) &&
                            cEl.TryGetProperty("summary", out var cSum) &&
                            cSum.TryGetProperty("total_count", out var cTc) &&
                            cTc.TryGetInt64(out var cVal))
                        {
                            cCount = cVal;
                        }

                        if (!string.IsNullOrWhiteSpace(id) && !list.Any(x => x.Id == id))
                        {
                            var perm = $"https://www.facebook.com/photo/?fbid={id}&set=a.{pageId}";
                            list.Add(new FacebookPostDto(id, name, ct, perm, highResPic, "photo", highResPic, pic, rCount, cCount, 1));
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query Facebook photos for page {PageId}", pageId);
        }

        return list;
    }

    public async Task<string?> GetPageProfilePictureAsync(string pageId, string pageAccessToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pageId) || string.IsNullOrWhiteSpace(pageAccessToken)) return null;
        try
        {
            var url = $"https://graph.facebook.com/v22.0/{Uri.EscapeDataString(pageId)}?fields=picture.type(large)&access_token={Uri.EscapeDataString(pageAccessToken)}";
            using var res = await _httpClient.GetAsync(url, cancellationToken);
            if (res.IsSuccessStatusCode)
            {
                var json = await res.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("picture", out var picEl) &&
                    picEl.TryGetProperty("data", out var dataEl) &&
                    dataEl.TryGetProperty("url", out var urlEl))
                {
                    return urlEl.GetString();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get page avatar for {PageId}", pageId);
        }
        return null;
    }

    public async Task<FacebookCommentsResultDto> GetPostCommentsAsync(string postId, string pageAccessToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(postId) || string.IsNullOrWhiteSpace(pageAccessToken))
        {
            return new FacebookCommentsResultDto([], false, null, HttpSuccess: false);
        }

        try
        {
            var url = $"https://graph.facebook.com/v22.0/{Uri.EscapeDataString(postId)}/comments?fields=id,from,message,created_time,parent&access_token={Uri.EscapeDataString(pageAccessToken)}";
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            var resString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                bool isPermissionError = false;
                string? errorCode = null;

                try
                {
                    using var errDoc = JsonDocument.Parse(resString);
                    if (errDoc.RootElement.TryGetProperty("error", out var errEl))
                    {
                        if (errEl.TryGetProperty("code", out var codeEl))
                        {
                            errorCode = codeEl.GetInt32().ToString();
                            if (errorCode == "10" || errorCode == "200")
                            {
                                isPermissionError = true;
                            }
                        }
                        if (errEl.TryGetProperty("message", out var msgEl))
                        {
                            var msg = msgEl.GetString() ?? "";
                            if (msg.Contains("pages_read_user_content", StringComparison.OrdinalIgnoreCase) ||
                                msg.Contains("permission", StringComparison.OrdinalIgnoreCase))
                            {
                                isPermissionError = true;
                            }
                        }
                    }
                }
                catch
                {
                    // Fallback to raw string search
                    if (resString.Contains("pages_read_user_content", StringComparison.OrdinalIgnoreCase) ||
                        resString.Contains("permission", StringComparison.OrdinalIgnoreCase))
                    {
                        isPermissionError = true;
                    }
                }

                _logger.LogWarning("Facebook Graph API GetPostComments returned {StatusCode} (ErrorCode={ErrorCode}, PermError={IsPerm}): {Error}",
                    response.StatusCode, errorCode, isPermissionError, resString);

                return new FacebookCommentsResultDto([], isPermissionError, errorCode, HttpSuccess: false);
            }

            using var doc = JsonDocument.Parse(resString);
            if (doc.RootElement.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array)
            {
                var list = new List<FacebookCommentDto>();
                foreach (var item in dataEl.EnumerateArray())
                {
                    var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                    var msg = item.TryGetProperty("message", out var msgEl) ? msgEl.GetString() : null;
                    var ct = item.TryGetProperty("created_time", out var ctEl) ? ctEl.GetString() : null;

                    FacebookCommentFromDto? from = null;
                    if (item.TryGetProperty("from", out var fEl))
                    {
                        var fromId = fEl.TryGetProperty("id", out var fIdEl) ? fIdEl.GetString() : null;
                        var fromName = fEl.TryGetProperty("name", out var fNameEl) ? fNameEl.GetString() : null;
                        from = new FacebookCommentFromDto(fromId, fromName);
                    }

                    JsonElement? parent = item.TryGetProperty("parent", out var pEl) ? pEl : null;

                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        list.Add(new FacebookCommentDto(id, from, msg, ct, parent));
                    }
                }
                return new FacebookCommentsResultDto(list, false, null, HttpSuccess: true);
            }

            return new FacebookCommentsResultDto([], false, null, HttpSuccess: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception querying Facebook post comments for post {PostId}", postId);
        }

        return new FacebookCommentsResultDto([], false, null, HttpSuccess: false);
    }

    public async Task<string?> ReplyCommentAsync(string commentId, string message, string pageAccessToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(commentId) || string.IsNullOrWhiteSpace(message) || string.IsNullOrWhiteSpace(pageAccessToken))
        {
            return null;
        }

        try
        {
            var url = $"https://graph.facebook.com/v22.0/{Uri.EscapeDataString(commentId)}/comments?access_token={Uri.EscapeDataString(pageAccessToken)}";
            var payload = new { message };
            var content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync(url, content, cancellationToken);
            var resString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Facebook Graph API ReplyComment failed with status {StatusCode}: {Error}", response.StatusCode, resString);
                return null;
            }

            using var doc = JsonDocument.Parse(resString);
            if (doc.RootElement.TryGetProperty("id", out var idEl))
            {
                return idEl.GetString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception sending Facebook comment reply to comment {CommentId}", commentId);
        }

        return null;
    }

    public async Task<FacebookPublishResultDto> PublishPostAsync(
        string pageId,
        string message,
        string pageAccessToken,
        string? mediaUrl = null,
        string? mediaType = null,
        DateTimeOffset? scheduledPublishTime = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pageId) || string.IsNullOrWhiteSpace(message) || string.IsNullOrWhiteSpace(pageAccessToken))
        {
            return new FacebookPublishResultDto(false, null, "InvalidArguments", "Thiếu thông tin pageId, message hoặc pageAccessToken.");
        }

        try
        {
            string url;
            var postData = new Dictionary<string, string>
            {
                ["access_token"] = pageAccessToken
            };

            bool isVideo = string.Equals(mediaType, "video", StringComparison.OrdinalIgnoreCase) ||
                           (!string.IsNullOrWhiteSpace(mediaUrl) && (mediaUrl.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) || mediaUrl.EndsWith(".mov", StringComparison.OrdinalIgnoreCase)));

            bool isPhoto = !string.IsNullOrWhiteSpace(mediaUrl) && !isVideo &&
                           (string.Equals(mediaType, "photo", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(mediaType, "image", StringComparison.OrdinalIgnoreCase) ||
                            mediaUrl.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                            mediaUrl.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                            mediaUrl.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                            mediaUrl.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ||
                            mediaUrl.Contains("fbcdn.net") ||
                            mediaUrl.Contains("imgur.com") ||
                            mediaUrl.Contains("cloudinary.com") ||
                            mediaUrl.Contains("unsplash.com"));

            if (isPhoto)
            {
                url = $"https://graph.facebook.com/v22.0/{Uri.EscapeDataString(pageId)}/photos";
                postData["caption"] = message;
                postData["url"] = mediaUrl!;
            }
            else if (isVideo)
            {
                url = $"https://graph.facebook.com/v22.0/{Uri.EscapeDataString(pageId)}/videos";
                postData["description"] = message;
                postData["file_url"] = mediaUrl!;
            }
            else
            {
                url = $"https://graph.facebook.com/v22.0/{Uri.EscapeDataString(pageId)}/feed";
                postData["message"] = message;
                if (!string.IsNullOrWhiteSpace(mediaUrl))
                {
                    postData["link"] = mediaUrl;
                }
            }

            if (scheduledPublishTime.HasValue)
            {
                postData["published"] = "false";
                postData["scheduled_publish_time"] = scheduledPublishTime.Value.ToUnixTimeSeconds().ToString();
            }

            using var content = new FormUrlEncodedContent(postData);
            using var response = await _httpClient.PostAsync(url, content, cancellationToken);
            var resString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                string? errorCode = ((int)response.StatusCode).ToString();
                string errorMessage = $"Facebook Graph API returned {(int)response.StatusCode}";

                try
                {
                    using var errDoc = JsonDocument.Parse(resString);
                    if (errDoc.RootElement.TryGetProperty("error", out var errEl))
                    {
                        if (errEl.TryGetProperty("code", out var codeEl))
                        {
                            errorCode = codeEl.GetInt32().ToString();
                        }
                        if (errEl.TryGetProperty("message", out var msgEl))
                        {
                            errorMessage = msgEl.GetString() ?? errorMessage;
                        }
                    }
                }
                catch
                {
                    // Fallback to default error message
                }

                _logger.LogError("Facebook Graph API PublishPost failed with code {Code}: {Error}", errorCode, errorMessage);
                return new FacebookPublishResultDto(false, null, errorCode, errorMessage);
            }

            using var doc = JsonDocument.Parse(resString);
            if (doc.RootElement.TryGetProperty("id", out var idEl))
            {
                var createdId = idEl.GetString();
                return new FacebookPublishResultDto(true, createdId, null, null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception publishing post to Facebook page {PageId}", pageId);
            return new FacebookPublishResultDto(false, null, "Exception", ex.Message);
        }

        return new FacebookPublishResultDto(false, null, "UnknownError", "Không nhận được ID bài viết từ Facebook Graph API.");
    }

    public async Task<bool> CancelScheduledPostAsync(string postId, string pageAccessToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(postId) || string.IsNullOrWhiteSpace(pageAccessToken))
        {
            return false;
        }

        try
        {
            var url = $"https://graph.facebook.com/v22.0/{Uri.EscapeDataString(postId)}?access_token={Uri.EscapeDataString(pageAccessToken)}";
            using var response = await _httpClient.DeleteAsync(url, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Facebook Graph API Delete unpublished post {PostId} returned {StatusCode}: {Error}", postId, response.StatusCode, err);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception cancelling scheduled post {PostId}", postId);
            return false;
        }
    }

    public async Task<FacebookPostInsightsResult> GetPostInsightsAsync(string postId, string pageAccessToken, CancellationToken cancellationToken = default)
    {
        long impressions = 0;
        long engagedUsers = 0;
        long clicks = 0;
        string freshness = "fresh";

        if (string.IsNullOrWhiteSpace(postId) || string.IsNullOrWhiteSpace(pageAccessToken))
        {
            return new FacebookPostInsightsResult(0, 0, 0, "none");
        }

        var metricsToQuery = new[] { "post_impressions", "post_engaged_users", "post_clicks", "post_media_view", "post_impressions_unique" };

        try
        {
            var initialMetrics = new[] { "post_impressions", "post_engaged_users", "post_clicks" };
            var metricStr = string.Join(",", initialMetrics);
            var url = $"https://graph.facebook.com/v22.0/{Uri.EscapeDataString(postId)}/insights?metric={Uri.EscapeDataString(metricStr)}&access_token={Uri.EscapeDataString(pageAccessToken)}";

            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                freshness = "partial";
                _logger.LogInformation("Facebook combined Insights query returned status {Status} for post {PostId}; falling back to per-metric query", response.StatusCode, postId);

                // Fallback: Retry querying each metric individually
                foreach (var singleMetric in metricsToQuery)
                {
                    try
                    {
                        var singleUrl = $"https://graph.facebook.com/v22.0/{Uri.EscapeDataString(postId)}/insights?metric={Uri.EscapeDataString(singleMetric)}&access_token={Uri.EscapeDataString(pageAccessToken)}";
                        using var singleRes = await _httpClient.GetAsync(singleUrl, cancellationToken);
                        if (!singleRes.IsSuccessStatusCode)
                        {
                            _logger.LogInformation("Skipping unsupported metric {Metric} for post {PostId}", singleMetric, postId);
                            continue;
                        }

                        var singleJson = await singleRes.Content.ReadAsStringAsync(cancellationToken);
                        using var singleDoc = JsonDocument.Parse(singleJson);
                        if (singleDoc.RootElement.TryGetProperty("data", out var sDataEl) && sDataEl.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var m in sDataEl.EnumerateArray())
                            {
                                var name = m.TryGetProperty("name", out var nEl) ? nEl.GetString() : null;
                                if (m.TryGetProperty("values", out var vals) && vals.ValueKind == JsonValueKind.Array && vals.GetArrayLength() > 0)
                                {
                                    var firstVal = vals[0];
                                    long val = 0;
                                    if (firstVal.TryGetProperty("value", out var vEl) && vEl.TryGetInt64(out var parsedVal))
                                    {
                                        val = parsedVal;
                                    }

                                    if (name == "post_impressions" || name == "post_impressions_unique") impressions = Math.Max(impressions, val);
                                    else if (name == "post_engaged_users") engagedUsers = val;
                                    else if (name == "post_clicks" || name == "post_media_view") clicks = Math.Max(clicks, val);
                                }
                            }
                        }
                    }
                    catch (Exception singleEx)
                    {
                        _logger.LogWarning(singleEx, "Failed to fetch individual metric {Metric} for post {PostId}", singleMetric, postId);
                    }
                }
            }
            else
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var m in dataEl.EnumerateArray())
                    {
                        var name = m.TryGetProperty("name", out var nEl) ? nEl.GetString() : null;
                        if (m.TryGetProperty("values", out var vals) && vals.ValueKind == JsonValueKind.Array && vals.GetArrayLength() > 0)
                        {
                            var firstVal = vals[0];
                            long val = 0;
                            if (firstVal.TryGetProperty("value", out var vEl) && vEl.TryGetInt64(out var parsedVal))
                            {
                                val = parsedVal;
                            }

                            if (name == "post_impressions") impressions = val;
                            else if (name == "post_engaged_users") engagedUsers = val;
                            else if (name == "post_clicks") clicks = val;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            freshness = "partial";
            _logger.LogWarning(ex, "Could not fetch all insights for post {PostId}", postId);
        }

        return new FacebookPostInsightsResult(impressions, engagedUsers, clicks, freshness);
    }

    public async Task<IReadOnlyList<FacebookConversationDto>> GetPageConversationsAsync(string pageId, string pageAccessToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pageId) || string.IsNullOrWhiteSpace(pageAccessToken))
        {
            return [];
        }

        var url = $"https://graph.facebook.com/v22.0/{Uri.EscapeDataString(pageId)}/conversations?fields=id,updated_time,message_count,unread_count,senders,messages{{id,created_time,from,to,message,attachments}}&limit=50&access_token={Uri.EscapeDataString(pageAccessToken)}";

        try
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Facebook Graph API GetPageConversations failed with status {StatusCode}: {Error}", response.StatusCode, errContent);
                return [];
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var dataEl) || dataEl.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var list = new List<FacebookConversationDto>();
            foreach (var convEl in dataEl.EnumerateArray())
            {
                var id = convEl.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                if (string.IsNullOrWhiteSpace(id)) continue;

                var updatedTime = convEl.TryGetProperty("updated_time", out var utEl) ? utEl.GetString() : null;
                int? msgCount = convEl.TryGetProperty("message_count", out var mcEl) && mcEl.TryGetInt32(out var mc) ? mc : null;
                int? unreadCount = convEl.TryGetProperty("unread_count", out var ucEl) && ucEl.TryGetInt32(out var uc) ? uc : null;

                var senders = new List<FacebookConversationSenderDto>();
                if (convEl.TryGetProperty("senders", out var sendersObj) &&
                    sendersObj.TryGetProperty("data", out var sendersArray) &&
                    sendersArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in sendersArray.EnumerateArray())
                    {
                        var sId = s.TryGetProperty("id", out var sIdEl) ? sIdEl.GetString() ?? "" : "";
                        var sName = s.TryGetProperty("name", out var sNameEl) ? sNameEl.GetString() : null;
                        var sEmail = s.TryGetProperty("email", out var sEmailEl) ? sEmailEl.GetString() : null;
                        senders.Add(new FacebookConversationSenderDto(sId, sName, sEmail));
                    }
                }

                var messages = new List<FacebookMessageDto>();
                if (convEl.TryGetProperty("messages", out var msgsObj) &&
                    msgsObj.TryGetProperty("data", out var msgsArray) &&
                    msgsArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var m in msgsArray.EnumerateArray())
                    {
                        var mId = m.TryGetProperty("id", out var mIdEl) ? mIdEl.GetString() ?? "" : "";
                        var mCreatedTime = m.TryGetProperty("created_time", out var mctEl) ? mctEl.GetString() : null;
                        var mMsg = m.TryGetProperty("message", out var mMsgEl) ? mMsgEl.GetString() : null;

                        FacebookCommentFromDto? from = null;
                        if (m.TryGetProperty("from", out var fromObj))
                        {
                            var fId = fromObj.TryGetProperty("id", out var fIdEl) ? fIdEl.GetString() : null;
                            var fName = fromObj.TryGetProperty("name", out var fNameEl) ? fNameEl.GetString() : null;
                            from = new FacebookCommentFromDto(fId, fName);
                        }

                        string? attUrl = null;
                        string? attType = null;
                        string? attName = null;
                        if (m.TryGetProperty("attachments", out var attsObj) &&
                            attsObj.TryGetProperty("data", out var attsArr) &&
                            attsArr.ValueKind == JsonValueKind.Array &&
                            attsArr.GetArrayLength() > 0)
                        {
                            var first = attsArr[0];
                            if (first.TryGetProperty("name", out var fn)) attName = fn.GetString();
                            if (first.TryGetProperty("mime_type", out var mt)) attType = mt.GetString();

                            if (first.TryGetProperty("file_url", out var fu))
                            {
                                attUrl = fu.GetString();
                                attType ??= "file";
                            }
                            else if (first.TryGetProperty("image_data", out var imgD))
                            {
                                if (imgD.TryGetProperty("url", out var iu)) attUrl = iu.GetString();
                                else if (imgD.TryGetProperty("preview_url", out var pu)) attUrl = pu.GetString();
                                attType ??= "image/jpeg";
                            }
                            else if (first.TryGetProperty("video_data", out var vidD))
                            {
                                if (vidD.TryGetProperty("url", out var vu)) attUrl = vu.GetString();
                                else if (vidD.TryGetProperty("preview_url", out var vpu)) attUrl = vpu.GetString();
                                attType ??= "video/mp4";
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(mId))
                        {
                            messages.Add(new FacebookMessageDto(mId, mCreatedTime, from, mMsg, attUrl, attType, attName));
                        }
                    }
                }

                list.Add(new FacebookConversationDto(id, updatedTime, msgCount, unreadCount, senders, messages));
            }

            return list;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception querying Facebook conversations for page {PageId}", pageId);
            return [];
        }
    }
}
