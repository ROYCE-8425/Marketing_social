using System.Globalization;
using System.Text.Json;
using DXOS.Application;
using DXOS.Domain;
using DXOS.Infrastructure;
using DXOS.Infrastructure.Integrations;
using DXOS.Infrastructure.Persistence;
using DXOS.Infrastructure.Persistence.Entities;
using Elsa.Workflows;
using Microsoft.EntityFrameworkCore;

namespace DXOS.Api;

internal static class MarketingEndpoints
{
    public static void MapMarketingSlice(this WebApplication app)
    {
        app.MapPost("/campaigns", async (CreateCampaignRequest request, CampaignService campaigns, HttpContext http, CancellationToken cancellationToken) =>
        {
            return await ExecuteAsync(http, actor =>
            {
                var dto = new CreateCampaignDraftDto(
                    request.Title,
                    request.Topic,
                    request.Kind,
                    request.Description,
                    request.Platforms,
                    request.EventStart,
                    request.EventEnd,
                    request.Location,
                    request.ImageUrls,
                    request.LandingUrl,
                    request.Product);
                return campaigns.CreateDraftAsync(actor, dto, cancellationToken);
            });
        });

        app.MapPut("/campaigns/{id:guid}", async (Guid id, UpdateCampaignRequest request, CampaignService campaigns, HttpContext http, CancellationToken cancellationToken) =>
        {
            return await ExecuteAsync(http, actor =>
            {
                var dto = new UpdateCampaignBriefDto(
                    request.Title,
                    request.Topic,
                    request.Copy,
                    request.Kind,
                    request.Description,
                    request.Platforms,
                    request.EventStart,
                    request.EventEnd,
                    request.Location,
                    request.ImageUrls,
                    request.LandingUrl,
                    request.Product);
                return campaigns.UpdateBriefAsync(actor, id, dto, cancellationToken);
            });
        });

        app.MapPost("/campaigns/{id:guid}/ai-drafts", async (
            Guid id,
            CampaignService campaigns,
            DXOS.Application.Abstractions.IChatClient chatClient,
            RbacService rbac,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var (allowed, forbidden, profile) = await CheckPermissionAsync(http, rbac, AppPermissions.PagePostsRead, cancellationToken);
            if (!allowed)
            {
                var (pubAllowed, pubForbidden, _) = await CheckPermissionAsync(http, rbac, AppPermissions.PagePublish, cancellationToken);
                if (!pubAllowed) return forbidden;
            }

            return await ExecuteAsync(http, actor => campaigns.GenerateAiDraftsAsync(actor, id, chatClient, cancellationToken));
        });

        app.MapPost("/campaigns/{id:guid}/apply-draft", async (
            Guid id,
            ApplyCampaignDraftRequest request,
            CampaignService campaigns,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request?.Caption))
            {
                return Results.BadRequest(new { error = "Caption cannot be empty.", code = "InvalidCaption" });
            }

            return await ExecuteAsync(http, actor => campaigns.ApplyDraftCopyAsync(actor, id, request.Caption, cancellationToken));
        });

        app.MapPost("/campaigns/{id:guid}/submit-review", async (Guid id, CampaignService campaigns, HttpContext http, CancellationToken cancellationToken) =>
        {
            return await ExecuteAsync(http, actor => campaigns.SubmitReviewAsync(actor, id, cancellationToken));
        });

        app.MapPost("/campaigns/{id:guid}/send-to-owner", async (Guid id, CampaignService campaigns, HttpContext http, CancellationToken cancellationToken) =>
        {
            return await ExecuteAsync(http, actor => campaigns.SendToOwnerAsync(actor, id, cancellationToken));
        });

        app.MapPost("/campaigns/{id:guid}/approve", async (Guid id, CampaignService campaigns, RbacService rbac, HttpContext http, CancellationToken cancellationToken) =>
        {
            var (allowed, forbidden, _) = await CheckPermissionAsync(http, rbac, AppPermissions.CampaignApprove, cancellationToken);
            if (!allowed) return forbidden;

            return await ExecuteAsync(http, actor => campaigns.ApproveAsync(actor, id, cancellationToken));
        });

        app.MapPost("/campaigns/{id:guid}/undo", async (Guid id, CampaignService campaigns, HttpContext http, CancellationToken cancellationToken) =>
        {
            return await ExecuteAsync(http, actor => campaigns.UndoApprovalAsync(actor, id, cancellationToken));
        });

        app.MapPost("/campaigns/{id:guid}/reject", async (Guid id, RejectRequest request, CampaignService campaigns, HttpContext http, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request?.Reason))
            {
                return Results.BadRequest(new { error = "Lý do từ chối chiến dịch là bắt buộc.", code = "InvalidReason" });
            }

            return await ExecuteAsync(http, actor => campaigns.RejectAsync(actor, id, request.Reason, cancellationToken));
        });

        app.MapGet("/campaigns/{id:guid}", async (Guid id, CampaignService campaigns, HttpContext http, CancellationToken cancellationToken) =>
        {
            try
            {
                ReadActor(http);
                var campaign = await campaigns.GetAsync(id, cancellationToken);
                return campaign is null
                    ? Results.NotFound(new { error = $"Campaign '{id}' was not found." })
                    : Results.Ok(ToCampaignResponse(campaign));
            }
            catch (DomainRuleException ex)
            {
                return MapDomainException(ex);
            }
        });

        app.MapGet("/campaigns", async (CampaignService campaigns, HttpContext http, CancellationToken cancellationToken) =>
        {
            try
            {
                ReadActor(http);
                var items = await campaigns.ListAsync(cancellationToken);
                return Results.Ok(items.Select(ToCampaignResponse).ToList());
            }
            catch (DomainRuleException ex)
            {
                return MapDomainException(ex);
            }
        });

        app.MapPost("/campaigns/{id:guid}/traffic", async (
            Guid id,
            RecordTrafficRequest request,
            IWorkflowRunner workflowRunner,
            HttpContext http,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            ActorContext actor;
            try
            {
                actor = ReadActor(http);
            }
            catch (DomainRuleException ex)
            {
                return MapDomainException(ex);
            }

            var correlationId = Guid.NewGuid().ToString("N");
            var workflow = new DXOS.Workflows.Traffic.TrafficIngestWorkflow();
            var runWorkflowOptions = new Elsa.Workflows.Options.RunWorkflowOptions
            {
                CorrelationId = correlationId,
                Input = new Dictionary<string, object>
                {
                    ["CampaignId"] = id,
                    ["PeriodDate"] = request.PeriodDate?.ToString("yyyy-MM-dd") ?? DateTimeOffset.UtcNow.ToString("yyyy-MM-dd"),
                    ["Impressions"] = request.Impressions,
                    ["Clicks"] = request.Clicks,
                    ["Visits"] = request.Visits,
                    ["SpendVnd"] = request.SpendVnd,
                    ["ActorRole"] = actor.Role.ToString(),
                    ["ActorId"] = actor.ActorId
                }
            };

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, http.RequestAborted, cancellationToken);

            try
            {
                var result = await workflowRunner.RunAsync(workflow, runWorkflowOptions, linkedCts.Token);
                var workflowState = result.WorkflowState;
                if (workflowState.Status != Elsa.Workflows.WorkflowStatus.Finished || workflowState.SubStatus != Elsa.Workflows.WorkflowSubStatus.Finished)
                {
                    var logger = loggerFactory.CreateLogger("MarketingEndpoints");
                    logger.LogError("Traffic ingest workflow did not finish cleanly: Status={Status}, SubStatus={SubStatus}", workflowState.Status, workflowState.SubStatus);
                    return Results.Json(new
                    {
                        status = "Failed",
                        error = "Traffic ingest workflow did not reach terminal Finished state."
                    }, statusCode: StatusCodes.Status500InternalServerError);
                }

                if (workflowState.Output.TryGetValue("IngestResult", out var ingestObj) && ingestObj is TrafficIngestResult ingestResult)
                {
                    return Results.Ok(ToTrafficIngestResponse(ingestResult));
                }

                var hasSnap = workflowState.Output.TryGetValue("Snapshot", out var snapObj);
                var hasTot = workflowState.Output.TryGetValue("Totals", out var totObj);
                if (hasSnap && snapObj is TrafficSnapshot snapshot && hasTot && totObj is CampaignTrafficTotals totals)
                {
                    return Results.Ok(ToTrafficIngestResponse(new TrafficIngestResult(snapshot, totals)));
                }

                return Results.Json(new
                {
                    status = "Failed",
                    error = "Workflow output missing IngestResult."
                }, statusCode: StatusCodes.Status500InternalServerError);
            }
            catch (DomainRuleException ex)
            {
                return MapDomainException(ex);
            }
        });

        app.MapGet("/campaigns/{id:guid}/traffic", async (
            Guid id,
            TrafficService trafficService,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            try
            {
                ReadActor(http);
                var summary = await trafficService.GetCampaignTrafficAsync(id, cancellationToken);
                return Results.Ok(new
                {
                    campaignId = summary.Campaign.Id,
                    topic = summary.Campaign.Topic,
                    snapshots = summary.Snapshots.Select(ToTrafficSnapshotResponse).ToList(),
                    totals = ToCampaignTrafficTotalsResponse(summary.Totals)
                });
            }
            catch (DomainRuleException ex)
            {
                return MapDomainException(ex);
            }
        });

        app.MapPost("/webhooks/{provider}/leads", async (
            string provider,
            PlatformLeadWebhookRequest request,
            LeadService leads,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            try
            {
                ReadActor(http);
                var raw = System.Text.Json.JsonSerializer.Serialize(request);
                var result = await leads.IntakePlatformWebhookAsync(
                    provider,
                    request.ExternalEventId ?? string.Empty,
                    request.Name ?? string.Empty,
                    request.Phone,
                    request.Email,
                    request.CampaignId,
                    raw,
                    cancellationToken);
                return Results.Ok(new
                {
                    duplicate = result.Duplicate,
                    lead = ToLeadResponse(result.Lead, DateTimeOffset.UtcNow)
                });
            }
            catch (DomainRuleException ex)
            {
                return MapDomainException(ex);
            }
        });

        app.MapGet("/platform-connections", (IConfiguration config, HttpContext http) =>
        {
            try { ReadActor(http); } catch { }
            var fbToken = config["FACEBOOK_PAGE_ACCESS_TOKEN"] ?? config["Facebook:PageAccessToken"];
            var fbMode = config["FACEBOOK_MODE"] ?? config["Facebook:Mode"];
            var isFbLive = !string.IsNullOrWhiteSpace(fbToken) || string.Equals(fbMode, "live", StringComparison.OrdinalIgnoreCase);

            var tiktokToken = config["TIKTOK_ACCESS_TOKEN"] ?? config["TikTok:AccessToken"];
            var tiktokMode = config["TIKTOK_MODE"] ?? config["TikTok:Mode"];
            var isTikTokLive = !string.IsNullOrWhiteSpace(tiktokToken) || string.Equals(tiktokMode, "live", StringComparison.OrdinalIgnoreCase);

            var zaloToken = config["ZALO_OA_ACCESS_TOKEN"] ?? config["Zalo:OaAccessToken"];
            var zaloMode = config["ZALO_MODE"] ?? config["Zalo:Mode"];
            var isZaloLive = !string.IsNullOrWhiteSpace(zaloToken) || string.Equals(zaloMode, "live", StringComparison.OrdinalIgnoreCase);

            return Results.Ok(PlatformCatalog.MockConnections.Select(c =>
            {
                var isLive = c.Provider switch
                {
                    "facebook" => isFbLive,
                    "tiktok" => isTikTokLive,
                    "zalo" => isZaloLive,
                    _ => false
                };

                return new
                {
                    provider = c.Provider,
                    displayName = c.DisplayName,
                    capabilities = isLive
                        ? (c.Provider == "tiktok" ? new[] { "READ_LEADS", "WEBHOOK", "TIKTOK_MARKETING_API" } : new[] { "READ_LEADS", "WEBHOOK", "GRAPH_API_LEAD_ADS" })
                        : c.Capabilities,
                    mode = isLive ? "development-live" : c.Mode,
                    adsLive = false,
                    token = (string?)null
                };
            }));
        });

        // Official Meta Graph API Webhook for Facebook Lead Ads
        app.MapGet("/integrations/facebook/webhook", (
            HttpContext http,
            IConfiguration config) =>
        {
            var mode = http.Request.Query["hub.mode"].ToString();
            var verifyToken = http.Request.Query["hub.verify_token"].ToString();
            var challenge = http.Request.Query["hub.challenge"].ToString();

            var expectedVerifyToken = config["FACEBOOK_VERIFY_TOKEN"]
                ?? config["Facebook:VerifyToken"]
                ?? "dxos_marketing_verify_token_2026";

            if (string.Equals(mode, "subscribe", StringComparison.Ordinal) &&
                string.Equals(verifyToken, expectedVerifyToken, StringComparison.Ordinal))
            {
                return Results.Content(challenge, "text/plain");
            }

            return Results.Unauthorized();
        });

        app.MapPost("/integrations/facebook/webhook", async (
            HttpContext http,
            LeadService leads,
            FacebookLeadAdsClient facebookClient,
            BootstrapDbContext db,
            IConfiguration config,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var logger = loggerFactory.CreateLogger("DXOS.FacebookWebhooks");
            http.Request.EnableBuffering();
            using var reader = new StreamReader(http.Request.Body, System.Text.Encoding.UTF8, leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync(cancellationToken);
            http.Request.Body.Position = 0;

            var appSecret = config["FACEBOOK_APP_SECRET"] ?? config["Facebook:AppSecret"];
            var signature = http.Request.Headers["X-Hub-Signature-256"].ToString();

            if (!FacebookLeadAdsClient.VerifySignature(rawBody, signature, appSecret))
            {
                logger.LogWarning("Facebook webhook signature verification failed.");
                return Results.Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(rawBody))
            {
                return Results.Ok("EVENT_RECEIVED");
            }

            try
            {
                using var doc = JsonDocument.Parse(rawBody);
                var root = doc.RootElement;
                if (root.TryGetProperty("entry", out var entries) && entries.ValueKind == JsonValueKind.Array)
                {
                    var pageToken = config["FACEBOOK_PAGE_ACCESS_TOKEN"] ?? config["Facebook:PageAccessToken"];
                    var fbMode = config["FACEBOOK_MODE"] ?? config["Facebook:Mode"] ?? "mock";

                    foreach (var entry in entries.EnumerateArray())
                    {
                        var entryPageId = entry.TryGetProperty("id", out var entryIdProp) ? entryIdProp.GetString() : "988656934325292";

                        // 1. Messenger Messaging Events
                        if (entry.TryGetProperty("messaging", out var messagingList) && messagingList.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var msgEvent in messagingList.EnumerateArray())
                            {
                                var senderId = msgEvent.TryGetProperty("sender", out var sProp) && sProp.TryGetProperty("id", out var sId) ? sId.GetString() : null;
                                var recipientId = msgEvent.TryGetProperty("recipient", out var rProp) && rProp.TryGetProperty("id", out var rId) ? rId.GetString() : entryPageId;

                                if (string.IsNullOrWhiteSpace(senderId)) continue;

                                if (msgEvent.TryGetProperty("message", out var msgObj))
                                {
                                    var mid = msgObj.TryGetProperty("mid", out var mProp) ? mProp.GetString() : $"msg_{Guid.NewGuid():N}";
                                    var text = msgObj.TryGetProperty("text", out var tProp) ? tProp.GetString() : null;
                                    var timestampMs = msgEvent.TryGetProperty("timestamp", out var tsProp) ? tsProp.GetInt64() : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                                    var messageTime = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs);

                                    string messageType = "text";
                                    var attachmentsList = new List<object>();

                                    if (msgObj.TryGetProperty("attachments", out var attArray) && attArray.ValueKind == JsonValueKind.Array)
                                    {
                                        foreach (var att in attArray.EnumerateArray())
                                        {
                                            var attType = att.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : "file";
                                            string? url = null;
                                            string? title = null;
                                            if (att.TryGetProperty("payload", out var payloadProp))
                                            {
                                                url = payloadProp.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null;
                                                title = payloadProp.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : null;
                                            }

                                            if (!string.IsNullOrWhiteSpace(url))
                                            {
                                                attachmentsList.Add(new { type = attType ?? "file", url = url, title = title });
                                                if (messageType == "text")
                                                {
                                                    messageType = attType ?? "file";
                                                }
                                            }
                                        }
                                    }

                                    if (string.IsNullOrWhiteSpace(text))
                                    {
                                        text = messageType switch
                                        {
                                            "image" => "[Hình ảnh]",
                                            "audio" => "[Tin nhắn thoại]",
                                            "video" => "[Video]",
                                            "file" => "[Tập tin]",
                                            _ => "(Đính kèm)"
                                        };
                                    }
                                    var attachmentsJson = attachmentsList.Count > 0 ? JsonSerializer.Serialize(attachmentsList) : "[]";

                                    var isEcho = (msgObj.TryGetProperty("is_echo", out var echoProp) && echoProp.GetBoolean()) || string.Equals(senderId, entryPageId, StringComparison.OrdinalIgnoreCase);

                                    string pageId = entryPageId ?? "988656934325292";
                                    string customerPsid;
                                    string senderType;
                                    string senderName;

                                    if (isEcho)
                                    {
                                        customerPsid = recipientId ?? "";
                                        senderType = "agent";
                                        senderName = "Royce Shop";
                                    }
                                    else
                                    {
                                        customerPsid = senderId;
                                        senderType = "customer";
                                        senderName = $"Khách Facebook {senderId.Substring(0, Math.Min(4, senderId.Length))}";
                                        if (!string.IsNullOrWhiteSpace(pageToken) && !string.Equals(fbMode, "mock", StringComparison.OrdinalIgnoreCase))
                                        {
                                            var fetchedName = await facebookClient.FetchUserProfileNameAsync(senderId, pageToken, cancellationToken: cancellationToken);
                                            if (!string.IsNullOrWhiteSpace(fetchedName))
                                            {
                                                senderName = fetchedName;
                                            }
                                        }
                                    }

                                    var convId = $"fb_{pageId}_{customerPsid}";
                                    var custId = $"fb_user_{customerPsid}";

                                    await IngestSocialMessageAsync(
                                        db,
                                        pageId,
                                        custId,
                                        isEcho ? "Khách hàng" : senderName,
                                        convId,
                                        mid ?? Guid.NewGuid().ToString("N"),
                                        senderId,
                                        senderName,
                                        senderType,
                                        text ?? "",
                                        messageTime,
                                        cancellationToken,
                                        leads: leads,
                                        logger: logger,
                                        messageType: messageType,
                                        attachmentsJson: attachmentsJson);

                                    logger.LogInformation("Processed Messenger message ({Type}) from {Sender}: {Text}", senderType, senderName, text);
                                }
                            }
                        }

                        // 2. Lead Ads & Feed/Comment Changes Events
                        if (entry.TryGetProperty("changes", out var changes) && changes.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var change in changes.EnumerateArray())
                            {
                                var field = change.TryGetProperty("field", out var f) ? f.GetString() : null;
                                if (field == "leadgen" && change.TryGetProperty("value", out var val))
                                {
                                    var leadgenId = val.TryGetProperty("leadgen_id", out var lgId) ? lgId.GetString() : null;
                                    if (string.IsNullOrWhiteSpace(leadgenId)) continue;

                                    string name = "Khách hàng Facebook";
                                    string? phone = null;
                                    string? email = null;

                                    if (!string.IsNullOrWhiteSpace(pageToken) && !string.Equals(fbMode, "mock", StringComparison.OrdinalIgnoreCase))
                                    {
                                        var leadPayload = await facebookClient.FetchLeadAsync(leadgenId, pageToken, cancellationToken: cancellationToken);
                                        if (leadPayload is not null)
                                        {
                                            var extracted = FacebookLeadAdsClient.ExtractLeadFields(leadPayload.FieldData, name);
                                            name = extracted.Name;
                                            phone = extracted.Phone;
                                            email = extracted.Email;
                                        }
                                        else
                                        {
                                            if (val.TryGetProperty("name", out var nVal)) name = nVal.GetString() ?? name;
                                            if (val.TryGetProperty("phone", out var pVal)) phone = pVal.GetString();
                                            if (val.TryGetProperty("email", out var eVal)) email = eVal.GetString();
                                        }
                                    }
                                    else
                                    {
                                        // Mock / Test Payload fallback
                                        if (val.TryGetProperty("name", out var nVal)) name = nVal.GetString() ?? name;
                                        if (val.TryGetProperty("phone", out var pVal)) phone = pVal.GetString();
                                        if (val.TryGetProperty("email", out var eVal)) email = eVal.GetString();
                                    }

                                    var convId = $"fb_lead_conv_{leadgenId}";
                                    var custId = $"fb_lead_cust_{leadgenId}";
                                    var content = $"Khách hàng để lại form: SĐT: {phone ?? "—"}, Email: {email ?? "—"}";

                                    await IngestSocialMessageAsync(
                                        db,
                                        entryPageId ?? "988656934325292",
                                        custId,
                                        name,
                                        convId,
                                        $"msg_lead_{leadgenId}",
                                        leadgenId,
                                        name,
                                        "customer",
                                        content,
                                        DateTimeOffset.UtcNow,
                                        cancellationToken,
                                        leads: leads,
                                        logger: logger);

                                    await leads.IntakePlatformWebhookAsync(
                                        "facebook",
                                        leadgenId,
                                        name,
                                        phone,
                                        email,
                                        campaignId: null,
                                        rawPayload: rawBody,
                                        cancellationToken: cancellationToken);

                                    logger.LogInformation("Processed Facebook leadgen event {LeadgenId} for {Name}", leadgenId, name);
                                }
                                else if ((field == "feed" || field == "comments") && change.TryGetProperty("value", out var feedVal))
                                {
                                    try
                                    {
                                        var item = feedVal.TryGetProperty("item", out var itemProp) ? itemProp.GetString() : null;
                                        var commentId = feedVal.TryGetProperty("comment_id", out var cIdProp) ? cIdProp.GetString() : null;
                                        var postId = feedVal.TryGetProperty("post_id", out var pIdProp) ? pIdProp.GetString() : null;
                                        var parentId = feedVal.TryGetProperty("parent_id", out var parIdProp) ? parIdProp.GetString() : null;
                                        var message = feedVal.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : null;

                                        DateTimeOffset? createdTime = null;
                                        if (feedVal.TryGetProperty("created_time", out var ctProp))
                                        {
                                            if (ctProp.ValueKind == JsonValueKind.Number && ctProp.TryGetInt64(out var ctNum))
                                            {
                                                createdTime = DateTimeOffset.FromUnixTimeSeconds(ctNum);
                                            }
                                            else if (ctProp.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(ctProp.GetString(), out var ctParsed))
                                            {
                                                createdTime = ctParsed;
                                            }
                                        }
                                        createdTime ??= DateTimeOffset.UtcNow;

                                        string? fromId = null;
                                        string? fromName = null;
                                        if (feedVal.TryGetProperty("from", out var fromProp) && fromProp.ValueKind == JsonValueKind.Object)
                                        {
                                            fromId = fromProp.TryGetProperty("id", out var fIdProp) ? fIdProp.GetString() : null;
                                            fromName = fromProp.TryGetProperty("name", out var fNameProp) ? fNameProp.GetString() : null;
                                        }

                                        if (item == "comment" || !string.IsNullOrWhiteSpace(commentId))
                                        {
                                            var effectiveCommentId = commentId ?? parentId ?? $"comment_{Guid.NewGuid():N}";
                                            var effectivePostId = postId ?? parentId ?? "unknown_post";
                                            var parentCommentId = (!string.IsNullOrWhiteSpace(parentId) && parentId != effectivePostId) ? parentId : null;

                                            var existingComment = await db.SocialComments.FirstOrDefaultAsync(c => c.CommentId == effectiveCommentId, cancellationToken);
                                            if (existingComment is null)
                                            {
                                                var commentRecord = new SocialCommentRecord
                                                {
                                                    Id = $"comment_{effectiveCommentId}",
                                                    CommentId = effectiveCommentId,
                                                    PostId = effectivePostId,
                                                    FromId = fromId,
                                                    FromName = fromName,
                                                    Message = message,
                                                    ParentCommentId = parentCommentId,
                                                    CreatedTimeUtc = createdTime,
                                                    CreatedAtUtc = DateTimeOffset.UtcNow
                                                };
                                                db.SocialComments.Add(commentRecord);
                                            }
                                            else
                                            {
                                                existingComment.Message = message ?? existingComment.Message;
                                                if (!string.IsNullOrWhiteSpace(fromName)) existingComment.FromName = fromName;
                                                if (createdTime.HasValue) existingComment.CreatedTimeUtc = createdTime;
                                            }
                                            await db.SaveChangesAsync(cancellationToken);
                                            logger.LogInformation("Processed Facebook comment feed change for comment {CommentId}", effectiveCommentId);
                                        }
                                        else if (item is "post" or "status" or "share" || !string.IsNullOrWhiteSpace(postId))
                                        {
                                            var effectivePostId = postId ?? (feedVal.TryGetProperty("id", out var idProp) ? idProp.GetString() : null) ?? $"post_{Guid.NewGuid():N}";
                                            var existingPost = await db.SocialPosts.FirstOrDefaultAsync(p => p.PostId == effectivePostId, cancellationToken);
                                            if (existingPost is null)
                                            {
                                                var postRecord = new SocialPostRecord
                                                {
                                                    Id = $"post_{effectivePostId}",
                                                    PostId = effectivePostId,
                                                    PageId = entryPageId ?? "988656934325292",
                                                    Message = message,
                                                    Status = "published",
                                                    CreatedTimeUtc = createdTime,
                                                    CreatedAtUtc = DateTimeOffset.UtcNow
                                                };
                                                db.SocialPosts.Add(postRecord);
                                            }
                                            else
                                            {
                                                existingPost.Message = message ?? existingPost.Message;
                                                if (createdTime.HasValue) existingPost.CreatedTimeUtc = createdTime;
                                            }
                                            await db.SaveChangesAsync(cancellationToken);
                                            logger.LogInformation("Processed Facebook post feed change for post {PostId}", effectivePostId);
                                        }
                                    }
                                    catch (Exception feedEx)
                                    {
                                        logger.LogWarning(feedEx, "Failed to process feed change event in Facebook webhook");
                                    }
                                }
                            }
                        }
                    }
                }

                return Results.Ok("EVENT_RECEIVED");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing Facebook webhook");
                return Results.Ok("EVENT_RECEIVED");
            }
        });

        // ── Zalo Official Account Webhook Endpoints ──
        app.MapGet("/zalo_verifierUUI68vAK8oD0lAz_a85rHsIpy4Y8nIGNDp4p.html", () =>
            Results.Content("UUI68vAK8oD0lAz_a85rHsIpy4Y8nIGNDp4p", "text/html"));

        app.MapGet("/integrations/zalo/webhook", (HttpContext http) =>
        {
            var challenge = http.Request.Query["challenge"].ToString();
            if (!string.IsNullOrWhiteSpace(challenge))
            {
                return Results.Content(challenge, "text/plain");
            }
            return Results.Ok(new { status = "OK", provider = "zalo" });
        });

        app.MapPost("/integrations/zalo/webhook", async (
            HttpContext http,
            LeadService leads,
            ZaloOaClient zaloClient,
            BootstrapDbContext db,
            IConfiguration config,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var logger = loggerFactory.CreateLogger("DXOS.ZaloWebhooks");
            http.Request.EnableBuffering();
            using var reader = new StreamReader(http.Request.Body, System.Text.Encoding.UTF8, leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync(cancellationToken);
            http.Request.Body.Position = 0;

            var oaSecret = config["ZALO_OA_SECRET"] ?? config["Zalo:OaSecret"];
            var appSecret = config["ZALO_APP_SECRET"] ?? config["Zalo:AppSecret"];
            var signature = http.Request.Headers["X-ZEP-Signature"].ToString();
            if (string.IsNullOrWhiteSpace(signature))
            {
                signature = http.Request.Headers["X-Zalo-Signature"].ToString();
            }
            if (string.IsNullOrWhiteSpace(signature))
            {
                signature = http.Request.Headers["mac"].ToString();
            }
            var timestamp = http.Request.Headers["timestamp"].ToString();
            var appIdHeader = http.Request.Headers["app_id"].ToString();

            if (!ZaloOaClient.VerifyWebhookSignature(rawBody, signature, oaSecret, appSecret, timestamp, appIdHeader))
            {
                logger.LogWarning("Zalo webhook signature verification failed.");
                return Results.Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(rawBody))
            {
                return Results.Ok(new { error = 0, message = "SUCCESS" });
            }

            try
            {
                var ev = ZaloOaClient.ParseWebhookEvent(rawBody);
                var oaId = ev.OaId ?? config["ZALO_OA_ID"] ?? "zalo_oa";
                var senderId = ev.SenderId;

                if (!string.IsNullOrWhiteSpace(senderId))
                {
                    var oaAccessToken = config["ZALO_OA_ACCESS_TOKEN"] ?? config["Zalo:OaAccessToken"];
                    var zaloMode = config["ZALO_MODE"] ?? config["Zalo:Mode"] ?? "mock";

                    string senderName = $"Khách Zalo {(senderId.Length > 4 ? senderId.Substring(0, 4) : senderId)}";
                    if (!string.IsNullOrWhiteSpace(oaAccessToken) && !string.Equals(zaloMode, "mock", StringComparison.OrdinalIgnoreCase))
                    {
                        var profile = await zaloClient.FetchUserProfileAsync(senderId, oaAccessToken, cancellationToken);
                        if (!string.IsNullOrWhiteSpace(profile?.DisplayName))
                        {
                            senderName = profile.DisplayName;
                        }
                    }

                    var convId = $"zalo_{oaId}_{senderId}";
                    var custId = $"zalo_user_{senderId}";
                    var mid = ev.MessageId ?? $"zalo_msg_{Guid.NewGuid():N}";
                    var msgText = !string.IsNullOrWhiteSpace(ev.Text) ? ev.Text : "(Tin nhắn Zalo)";
                    var msgTime = ev.Timestamp.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(ev.Timestamp.Value) : DateTimeOffset.UtcNow;
                    var messageType = (ev.Attachments != null && ev.Attachments.Count > 0) ? ev.Attachments[0].Type : "text";
                    var attachmentsJson = (ev.Attachments != null && ev.Attachments.Count > 0) ? JsonSerializer.Serialize(ev.Attachments) : "[]";

                    await IngestSocialMessageAsync(
                        db,
                        oaId,
                        custId,
                        senderName,
                        convId,
                        mid,
                        senderId,
                        senderName,
                        "customer",
                        msgText,
                        msgTime,
                        cancellationToken,
                        leads: leads,
                        logger: logger,
                        messageType: messageType,
                        attachmentsJson: attachmentsJson);

                    await leads.IntakePlatformWebhookAsync(
                        "zalo",
                        mid,
                        senderName,
                        null,
                        null,
                        null,
                        rawBody,
                        cancellationToken);

                    logger.LogInformation("Processed Zalo message from {Sender}: {Text}", senderName, msgText);
                }

                return Results.Ok(new { error = 0, message = "SUCCESS" });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing Zalo webhook");
                return Results.Ok(new { error = 0, message = "SUCCESS" });
            }
        });

        // ── TikTok Marketing API Lead Generation Webhook Endpoints ──
        app.MapGet("/integrations/tiktok/webhook", (HttpContext http) =>
        {
            var challenge = http.Request.Query["challenge"].ToString();
            if (string.IsNullOrWhiteSpace(challenge))
            {
                challenge = http.Request.Query["hub.challenge"].ToString();
            }
            if (!string.IsNullOrWhiteSpace(challenge))
            {
                return Results.Content(challenge, "text/plain");
            }
            return Results.Ok(new { status = "OK", provider = "tiktok" });
        });

        app.MapPost("/integrations/tiktok/webhook", async (
            HttpContext http,
            LeadService leads,
            TikTokLeadAdsClient tiktokClient,
            BootstrapDbContext db,
            IConfiguration config,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var logger = loggerFactory.CreateLogger("DXOS.TikTokWebhooks");
            http.Request.EnableBuffering();
            using var reader = new StreamReader(http.Request.Body, System.Text.Encoding.UTF8, leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync(cancellationToken);
            http.Request.Body.Position = 0;

            var appSecret = config["TIKTOK_APP_SECRET"] ?? config["TikTok:AppSecret"];
            var signature = http.Request.Headers["X-TikTok-Signature"].ToString();
            if (string.IsNullOrWhiteSpace(signature))
            {
                signature = http.Request.Headers["X-Signature"].ToString();
            }
            if (string.IsNullOrWhiteSpace(signature))
            {
                signature = http.Request.Headers["Signature"].ToString();
            }

            if (!TikTokLeadAdsClient.VerifySignature(rawBody, signature, appSecret))
            {
                logger.LogWarning("TikTok webhook signature verification failed.");
                return Results.Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(rawBody))
            {
                return Results.Ok(new { code = 0, message = "SUCCESS" });
            }

            try
            {
                var payload = TikTokLeadAdsClient.ParseWebhookPayload(rawBody);
                if (payload is not null)
                {
                    var advId = payload.AdvertiserId ?? config["TIKTOK_ADVERTISER_ID"] ?? "tiktok_adv";
                    var leadId = payload.LeadId;
                    var extracted = TikTokLeadAdsClient.ExtractLeadFields(payload.FieldData, "Khách hàng TikTok");
                    var name = extracted.Name;
                    var phone = extracted.Phone;
                    var email = extracted.Email;

                    var convId = $"tiktok_{advId}_{leadId}";
                    var custId = $"tiktok_user_{leadId}";
                    var content = $"Khách hàng để lại Lead TikTok: SĐT: {phone ?? "—"}, Email: {email ?? "—"}";

                    await IngestSocialMessageAsync(
                        db,
                        advId,
                        custId,
                        name,
                        convId,
                        $"tt_msg_{leadId}",
                        leadId,
                        name,
                        "customer",
                        content,
                        DateTimeOffset.UtcNow,
                        cancellationToken,
                        leads: leads,
                        logger: logger);

                    await leads.IntakePlatformWebhookAsync(
                        "tiktok",
                        leadId,
                        name,
                        phone,
                        email,
                        campaignId: null,
                        rawPayload: rawBody,
                        cancellationToken: cancellationToken);

                    logger.LogInformation("Processed TikTok leadgen event {LeadId} for {Name}", leadId, name);
                }

                return Results.Ok(new { code = 0, message = "SUCCESS" });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing TikTok webhook");
                return Results.Ok(new { code = 0, message = "SUCCESS" });
            }
        });

        // ── Extension / Ingest Sync Endpoints (/api/status & /api/sync) ──
        app.MapGet("/api/status", () => Results.Ok(new
        {
            status = "online",
            version = "1.6.0",
            sync_interval_minutes = 5
        }));

        app.MapPost("/api/sync", async (
            JsonElement body,
            BootstrapDbContext db,
            LeadService leads,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var logger = loggerFactory.CreateLogger("DXOS.SyncReceiver");
            try
            {
                // BATCH format: { page_name, channel, ten_khach, url, thread_id, messages: [...] }
                if (body.TryGetProperty("messages", out var messagesProp) && messagesProp.ValueKind == JsonValueKind.Array)
                {
                    var pageName = body.TryGetProperty("page_name", out var pn) ? pn.GetString() : "Royce Shop";
                    var channel = body.TryGetProperty("channel", out var ch) ? ch.GetString() : "zalo";
                    var tenKhach = body.TryGetProperty("ten_khach", out var tk) ? tk.GetString() : "Khách hàng";
                    var url = body.TryGetProperty("url", out var u) ? u.GetString() : null;
                    var threadId = body.TryGetProperty("thread_id", out var tid) ? tid.GetString() : null;

                    var isZalo = (channel != null && channel.Contains("zalo", StringComparison.OrdinalIgnoreCase)) || (url != null && url.Contains("zalo", StringComparison.OrdinalIgnoreCase));
                    var pageId = isZalo ? (body.TryGetProperty("page_id", out var pi) ? pi.GetString() ?? "zalo_personal" : "zalo_personal") : "988656934325292";

                    int inserted = 0;
                    foreach (var msgEl in messagesProp.EnumerateArray())
                    {
                        var content = msgEl.TryGetProperty("content", out var c) ? c.GetString() : (msgEl.TryGetProperty("tin_nhan", out var tn) ? tn.GetString() : "");
                        if (string.IsNullOrWhiteSpace(content)) continue;

                        var senderType = msgEl.TryGetProperty("sender_type", out var st) ? st.GetString() ?? "customer" : "customer";
                        var senderName = msgEl.TryGetProperty("sender_name", out var sn) ? sn.GetString() ?? tenKhach : tenKhach;
                        var msgId = msgEl.TryGetProperty("pancake_msg_id", out var pmid) ? $"pm_{pmid.GetString()}" : (msgEl.TryGetProperty("msg_id", out var mid) ? mid.GetString() : $"sync_{Guid.NewGuid():N}");

                        var convId = !string.IsNullOrWhiteSpace(threadId)
                            ? (threadId.StartsWith("zalo_") || threadId.StartsWith("fb_") ? threadId : $"{(isZalo ? "zalo_" : "fb_")}{threadId}")
                            : $"{(isZalo ? "zalo_" : "fb_")}{pageId}_{tenKhach?.Replace(" ", "_") ?? "khach"}";

                        var custId = $"{(isZalo ? "zalo_user_" : "fb_user_")}{tenKhach?.Replace(" ", "_") ?? "unknown"}";
                        var createdTime = DateTimeOffset.UtcNow;
                        if (msgEl.TryGetProperty("timestamp", out var tsEl))
                        {
                            if (tsEl.ValueKind == JsonValueKind.Number && tsEl.TryGetInt64(out var tsNum))
                            {
                                createdTime = DateTimeOffset.FromUnixTimeMilliseconds(tsNum);
                            }
                            else if (tsEl.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(tsEl.GetString(), out var parsedTs))
                            {
                                createdTime = parsedTs;
                            }
                        }

                        await IngestSocialMessageAsync(
                            db,
                            pageId,
                            custId,
                            tenKhach ?? "Khách hàng",
                            convId,
                            msgId ?? $"sync_msg_{Guid.NewGuid():N}",
                            senderType == "agent" ? pageId : custId,
                            senderName ?? tenKhach ?? "Khách hàng",
                            senderType,
                            content,
                            createdTime,
                            cancellationToken,
                            leads: leads,
                            logger: logger);

                        inserted++;
                    }

                    return Results.Ok(new { success = true, total = messagesProp.GetArrayLength(), inserted, deduped = 0, failed = 0 });
                }

                // SINGLE format: { page_name, channel, ten_khach, url, thread_id, tin_nhan / content, ... }
                var singleContent = body.TryGetProperty("content", out var singleC) ? singleC.GetString() : (body.TryGetProperty("tin_nhan", out var singleTn) ? singleTn.GetString() : "");
                if (!string.IsNullOrWhiteSpace(singleContent))
                {
                    var pageName = body.TryGetProperty("page_name", out var pn) ? pn.GetString() : "Royce Shop";
                    var channel = body.TryGetProperty("channel", out var ch) ? ch.GetString() : "zalo";
                    var tenKhach = body.TryGetProperty("ten_khach", out var tk) ? tk.GetString() : "Khách hàng";
                    var threadId = body.TryGetProperty("thread_id", out var tid) ? tid.GetString() : null;
                    var senderType = body.TryGetProperty("sender_type", out var st) ? st.GetString() ?? "customer" : "customer";
                    var senderName = body.TryGetProperty("sender_name", out var sn) ? sn.GetString() ?? tenKhach : tenKhach;

                    var isZalo = (channel != null && channel.Contains("zalo", StringComparison.OrdinalIgnoreCase));
                    var pageId = isZalo ? "zalo_personal" : "988656934325292";
                    var convId = !string.IsNullOrWhiteSpace(threadId)
                        ? (threadId.StartsWith("zalo_") || threadId.StartsWith("fb_") ? threadId : $"{(isZalo ? "zalo_" : "fb_")}{threadId}")
                        : $"{(isZalo ? "zalo_" : "fb_")}{pageId}_{tenKhach?.Replace(" ", "_") ?? "khach"}";
                    var custId = $"{(isZalo ? "zalo_user_" : "fb_user_")}{tenKhach?.Replace(" ", "_") ?? "unknown"}";
                    var msgId = body.TryGetProperty("pancake_msg_id", out var pmid) ? $"pm_{pmid.GetString()}" : $"sync_{Guid.NewGuid():N}";

                    await IngestSocialMessageAsync(
                        db,
                        pageId,
                        custId,
                        tenKhach ?? "Khách hàng",
                        convId,
                        msgId,
                        senderType == "agent" ? pageId : custId,
                        senderName ?? tenKhach ?? "Khách hàng",
                        senderType,
                        singleContent,
                        DateTimeOffset.UtcNow,
                        cancellationToken,
                        leads: leads,
                        logger: logger);

                    return Results.Ok(new { success = true, inserted = 1, deduped = 0 });
                }

                return Results.BadRequest(new { error = "Invalid payload: missing messages[] or content/tin_nhan" });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing /api/sync");
                return Results.Json(new { error = ex.Message }, statusCode: 500);
            }
        });

        // ── Social CRM REST Endpoints for Admin UI ──
        app.MapGet("/pages", async (BootstrapDbContext db, RbacService rbac, HttpContext http, IConfiguration config, CancellationToken ct) =>
        {
            var (allowed, forbidden, _) = await CheckPermissionAsync(http, rbac, AppPermissions.InboxRead, ct);
            if (!allowed) return forbidden;

            var list = await db.SocialPages.AsNoTracking().ToListAsync(ct);
            if (list.Count == 0)
            {
                var fbPageId = config["FACEBOOK_PAGE_ID"] ?? "988656934325292";
                var zaloOaId = config["ZALO_OA_ID"] ?? "zalo_oa";
                return Results.Ok(new object[]
                {
                    new { id = fbPageId, name = "Royce Shop", type = "facebook", is_active = true, total_conversations = 0, total_messages = 0 },
                    new { id = zaloOaId, name = "Royce Shop", type = "zalo_oa", is_active = true, total_conversations = 0, total_messages = 0 }
                });
            }
            return Results.Ok(list);
        });

        app.MapGet("/customers", async (BootstrapDbContext db, RbacService rbac, HttpContext http, CancellationToken ct) =>
        {
            var (allowed, forbidden, _) = await CheckPermissionAsync(http, rbac, AppPermissions.InboxRead, ct);
            if (!allowed) return forbidden;

            var list = await db.SocialCustomers.AsNoTracking().OrderByDescending(c => c.LastSeenAt).ToListAsync(ct);
            return Results.Ok(list.Select(c => new
            {
                id = c.Id,
                name = c.Name,
                page_id = c.PageId,
                phone_numbers = c.PhoneNumbersJson,
                emails = c.EmailsJson,
                first_seen_at = c.FirstSeenAt,
                last_seen_at = c.LastSeenAt
            }));
        });

        app.MapGet("/conversations", async (BootstrapDbContext db, RbacService rbac, HttpContext http, CancellationToken ct) =>
        {
            var (allowed, forbidden, _) = await CheckPermissionAsync(http, rbac, AppPermissions.InboxRead, ct);
            if (!allowed) return forbidden;

            var list = await db.SocialConversations.AsNoTracking().OrderByDescending(c => c.UpdatedAt).ToListAsync(ct);
            return Results.Ok(list.Select(c =>
            {
                var platform = c.Id.StartsWith("fb_") ? (c.Id.Contains("lead") ? "lead" : "facebook")
                             : (c.Id.StartsWith("zalo_") ? "zalo"
                             : (c.Id.StartsWith("tiktok_") ? "tiktok" : "facebook"));
                string stage = c.Status ?? "active";
                if (c.Id.Contains("lead") || c.HasPhone) stage = "lead";
                else if (c.IsReplied) stage = "replied";

                if (!string.IsNullOrWhiteSpace(c.TagsJson) && c.TagsJson.Contains("status:"))
                {
                    try
                    {
                        var tags = JsonSerializer.Deserialize<string[]>(c.TagsJson);
                        var tag = tags?.FirstOrDefault(t => t.StartsWith("status:"));
                        if (tag is not null) stage = tag.Substring("status:".Length);
                    }
                    catch { }
                }

                return new
                {
                    id = c.Id,
                    page_id = c.PageId,
                    customer_id = c.CustomerId,
                    customer_name = c.CustomerName,
                    customer_phone = c.CustomerPhone,
                    snippet = c.Snippet,
                    message_count = c.MessageCount,
                    has_phone = c.HasPhone || !string.IsNullOrWhiteSpace(c.CustomerPhone),
                    is_replied = c.IsReplied,
                    stage = stage,
                    status = (c.Status ?? "open").ToUpperInvariant(),
                    assigned_to_actor = c.AssignedToActor,
                    internal_note = c.InternalNote,
                    platform = platform,
                    tags = c.TagsJson,
                    updated_at = c.UpdatedAt
                };
            }));
        });

        app.MapPost("/conversations/{id}/status", async (
            string id,
            UpdateConversationStatusRequest request,
            BootstrapDbContext db,
            RbacService rbac,
            HttpContext http,
            CancellationToken ct) =>
        {
            var (allowed, forbidden, profile) = await CheckPermissionAsync(http, rbac, AppPermissions.InboxModerate, ct);
            if (!allowed)
            {
                var (allowedReply, forbiddenReply, _) = await CheckPermissionAsync(http, rbac, AppPermissions.InboxReply, ct);
                if (!allowedReply) return forbidden;
            }

            var conv = await db.SocialConversations.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (conv is null) return Results.NotFound(new { error = "Conversation not found" });

            var validStatuses = new[] { "open", "pending", "done", "spam" };
            var newStatus = request.Status?.Trim().ToLowerInvariant() ?? "open";
            if (!validStatuses.Contains(newStatus))
            {
                return Results.BadRequest(new { error = "Status must be open, pending, done, or spam." });
            }

            conv.Status = newStatus;
            conv.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            await rbac.LogAuditAsync(profile.ActorId, AppPermissions.InboxModerate, "update_status", id, $"Changed status to {newStatus}", ct);

            return Results.Ok(new { success = true, id = conv.Id, status = conv.Status });
        });

        app.MapPost("/conversations/{id}/assign", async (
            string id,
            AssignConversationRequest request,
            BootstrapDbContext db,
            RbacService rbac,
            HttpContext http,
            CancellationToken ct) =>
        {
            var (allowed, forbidden, profile) = await CheckPermissionAsync(http, rbac, AppPermissions.InboxAssign, ct);
            if (!allowed) return forbidden;

            var conv = await db.SocialConversations.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (conv is null) return Results.NotFound(new { error = "Conversation not found" });

            conv.AssignedToActor = string.IsNullOrWhiteSpace(request.ActorId) ? null : request.ActorId.Trim();
            conv.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            await rbac.LogAuditAsync(profile.ActorId, AppPermissions.InboxAssign, "assign_conversation", id, $"Assigned to {conv.AssignedToActor ?? "None"}", ct);

            return Results.Ok(new { success = true, id = conv.Id, assigned_to_actor = conv.AssignedToActor });
        });

        app.MapPost("/conversations/{id}/note", async (
            string id,
            UpdateConversationNoteRequest request,
            BootstrapDbContext db,
            RbacService rbac,
            HttpContext http,
            CancellationToken ct) =>
        {
            var (allowed, forbidden, profile) = await CheckPermissionAsync(http, rbac, AppPermissions.InboxReply, ct);
            if (!allowed) return forbidden;

            var conv = await db.SocialConversations.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (conv is null) return Results.NotFound(new { error = "Conversation not found" });

            conv.InternalNote = request.Note?.Trim();
            conv.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            await rbac.LogAuditAsync(profile.ActorId, AppPermissions.InboxReply, "update_note", id, "Updated internal note", ct);

            return Results.Ok(new { success = true, id = conv.Id, internal_note = conv.InternalNote });
        });

        app.MapPost("/conversations/{id}/messages", async (
            string id,
            SendSocialMessageRequest request,
            BootstrapDbContext db,
            IHttpClientFactory httpFactory,
            ZaloOaClient zaloClient,
            RbacService rbac,
            HttpContext http,
            IConfiguration config,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var (allowed, forbidden, profile) = await CheckPermissionAsync(http, rbac, AppPermissions.InboxReply, cancellationToken);
            if (!allowed) return forbidden;

            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return Results.BadRequest(new { error = "Content is required" });
            }

            var conv = await db.SocialConversations.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
            if (conv is null)
            {
                return Results.NotFound(new { error = "Conversation not found" });
            }

            // ── Zalo Conversation Reply ──
            if (conv.Id.StartsWith("zalo_"))
            {
                var oaAccessToken = config["ZALO_OA_ACCESS_TOKEN"] ?? config["Zalo:OaAccessToken"];
                var zaloMode = config["ZALO_MODE"] ?? config["Zalo:Mode"] ?? "mock";
                var oaId = conv.PageId ?? config["ZALO_OA_ID"] ?? "zalo_oa";

                string? customerUserId = null;
                var parts = conv.Id.Split('_');
                if (parts.Length >= 3)
                {
                    customerUserId = parts[2];
                }
                if (string.IsNullOrWhiteSpace(customerUserId) && !string.IsNullOrWhiteSpace(conv.CustomerId))
                {
                    customerUserId = conv.CustomerId.Replace("zalo_user_", "");
                }

                if (!string.IsNullOrWhiteSpace(oaAccessToken) && !string.IsNullOrWhiteSpace(customerUserId) && !string.Equals(zaloMode, "mock", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var sendResult = await zaloClient.SendMessageAsync(customerUserId, request.Content, oaAccessToken, cancellationToken);
                        if (!sendResult.Success)
                        {
                            var logger = loggerFactory.CreateLogger("DXOS.ZaloSend");
                            logger.LogWarning("Zalo Send API warning: {Error} - {Message}", sendResult.ErrorCode, sendResult.Message);
                        }
                    }
                    catch (Exception ex)
                    {
                        var logger = loggerFactory.CreateLogger("DXOS.ZaloSend");
                        logger.LogError(ex, "Failed to send Zalo message via OpenAPI");
                    }
                }

                var msgId = $"agent_msg_{Guid.NewGuid():N}";
                var now = DateTimeOffset.UtcNow;

                await IngestSocialMessageAsync(
                    db,
                    oaId,
                    conv.CustomerId ?? $"zalo_user_{customerUserId ?? "unknown"}",
                    conv.CustomerName ?? "Khách hàng",
                    conv.Id,
                    msgId,
                    oaId,
                    "Royce Shop",
                    "agent",
                    request.Content,
                    now,
                    cancellationToken);

                return Results.Ok(new
                {
                    success = true,
                    id = msgId,
                    conversation_id = conv.Id,
                    sender_type = "agent",
                    sender_name = "Royce Shop",
                    content = request.Content,
                    created_time = now
                });
            }

            // ── TikTok Lead Conversation Reply ──
            if (conv.Id.StartsWith("tiktok_"))
            {
                var ttMsgId = $"agent_msg_{Guid.NewGuid():N}";
                var ttNow = DateTimeOffset.UtcNow;
                var advId = conv.PageId ?? config["TIKTOK_ADVERTISER_ID"] ?? "tiktok_adv";

                await IngestSocialMessageAsync(
                    db,
                    advId,
                    conv.CustomerId ?? $"tiktok_user_unknown",
                    conv.CustomerName ?? "Khách hàng",
                    conv.Id,
                    ttMsgId,
                    advId,
                    "Royce Shop",
                    "agent",
                    request.Content,
                    ttNow,
                    cancellationToken);

                return Results.Ok(new
                {
                    success = true,
                    id = ttMsgId,
                    conversation_id = conv.Id,
                    sender_type = "agent",
                    sender_name = "Royce Shop",
                    content = request.Content,
                    created_time = ttNow,
                    note = "TikTok Lead Gen does not support outbound direct chat reply; message stored as local CRM agent response."
                });
            }

            var pageToken = config["FACEBOOK_PAGE_ACCESS_TOKEN"] ?? config["Facebook:PageAccessToken"];
            var fbMode = config["FACEBOOK_MODE"] ?? config["Facebook:Mode"] ?? "mock";
            var pageId = conv.PageId ?? "988656934325292";

            // Extract customer PSID
            string? customerPsid = null;
            if (conv.Id.StartsWith("fb_") && !conv.Id.Contains("lead"))
            {
                var parts = conv.Id.Split('_');
                if (parts.Length >= 3)
                {
                    customerPsid = parts[2];
                }
            }
            if (string.IsNullOrWhiteSpace(customerPsid) && !string.IsNullOrWhiteSpace(conv.CustomerId))
            {
                customerPsid = conv.CustomerId.Replace("fb_user_", "");
            }

            // Call Facebook Send API if live
            if (!string.IsNullOrWhiteSpace(pageToken) && !string.IsNullOrWhiteSpace(customerPsid) && !string.Equals(fbMode, "mock", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var httpClient = httpFactory.CreateClient();
                    var sendUrl = $"https://graph.facebook.com/v21.0/me/messages?access_token={Uri.EscapeDataString(pageToken)}";
                    var payload = new
                    {
                        recipient = new { id = customerPsid },
                        messaging_type = "RESPONSE",
                        message = new { text = request.Content }
                    };
                    var bodyContent = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
                    using var response = await httpClient.PostAsync(sendUrl, bodyContent, cancellationToken);
                    if (!response.IsSuccessStatusCode)
                    {
                        var errStr = await response.Content.ReadAsStringAsync(cancellationToken);
                        var logger = loggerFactory.CreateLogger("DXOS.FacebookSend");
                        logger.LogWarning("Facebook Send API warning: {Error}", errStr);
                    }
                }
                catch (Exception ex)
                {
                    var logger = loggerFactory.CreateLogger("DXOS.FacebookSend");
                    logger.LogError(ex, "Failed to send Facebook Messenger message via Graph API");
                }
            }

            var fbMsgId = $"agent_msg_{Guid.NewGuid():N}";
            var fbNow = DateTimeOffset.UtcNow;

            await IngestSocialMessageAsync(
                db,
                pageId,
                conv.CustomerId ?? $"fb_user_{customerPsid ?? "unknown"}",
                conv.CustomerName ?? "Khách hàng",
                conv.Id,
                fbMsgId,
                pageId,
                "Royce Shop",
                "agent",
                request.Content,
                fbNow,
                cancellationToken);

            await rbac.LogAuditAsync(profile.ActorId, AppPermissions.InboxReply, "send_message", conv.Id, $"Sent message to {conv.CustomerName}", cancellationToken);

            return Results.Ok(new
            {
                success = true,
                id = fbMsgId,
                conversation_id = conv.Id,
                sender_type = "agent",
                sender_name = "Royce Shop",
                content = request.Content,
                created_time = fbNow
            });
        });

        // ══════════════════════════════════════════════════════════════════════
        // ── RBAC Authorization Endpoints (Part A) ───────────────────────────
        // ══════════════════════════════════════════════════════════════════════
        app.MapGet("/auth/me", async (RbacService rbac, HttpContext http, CancellationToken ct) =>
        {
            var actorId = http.Request.Headers["X-DXOS-Actor"].ToString();
            var profile = await rbac.ResolveActorProfileAsync(actorId, ct);
            return Results.Ok(profile);
        });

        app.MapGet("/settings/roles", async (RbacService rbac, HttpContext http, CancellationToken ct) =>
        {
            var (allowed, forbidden, _) = await CheckPermissionAsync(http, rbac, AppPermissions.SettingsRoles, ct);
            if (!allowed) return forbidden;

            var roles = await rbac.ListRolesAsync(ct);
            return Results.Ok(roles);
        });

        app.MapPost("/settings/roles", async (CreateRoleRequest request, RbacService rbac, HttpContext http, CancellationToken ct) =>
        {
            var (allowed, forbidden, profile) = await CheckPermissionAsync(http, rbac, AppPermissions.SettingsRoles, ct);
            if (!allowed) return forbidden;

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest(new { error = "Tên vai trò không được để trống.", code = "InvalidRoleName" });
            }

            try
            {
                var role = await rbac.CreateRoleAsync(request.Name, request.Description ?? string.Empty, request.Permissions ?? [], ct);
                await rbac.LogAuditAsync(profile.ActorId, AppPermissions.SettingsRoles, "create_role", role.Id.ToString(), $"Created role {role.Name}", ct);
                return Results.Ok(new { success = true, role = role });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message, code = "RoleCreateFailed" });
            }
        });

        app.MapPut("/settings/roles/{id:guid}/permissions", async (Guid id, UpdateRolePermissionsRequest request, RbacService rbac, HttpContext http, CancellationToken ct) =>
        {
            var (allowed, forbidden, profile) = await CheckPermissionAsync(http, rbac, AppPermissions.SettingsRoles, ct);
            if (!allowed) return forbidden;

            try
            {
                await rbac.UpdateRolePermissionsAsync(id, request.Permissions ?? [], ct);
                await rbac.LogAuditAsync(profile.ActorId, AppPermissions.SettingsRoles, "update_role_permissions", id.ToString(), "Updated permissions", ct);
                return Results.Ok(new { success = true, role_id = id });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message, code = "NotFound" });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message, code = "RoleUpdateFailed" });
            }
        });

        app.MapGet("/settings/users", async (RbacService rbac, HttpContext http, CancellationToken ct) =>
        {
            var (allowed, forbidden, _) = await CheckPermissionAsync(http, rbac, AppPermissions.SettingsRoles, ct);
            if (!allowed) return forbidden;

            var users = await rbac.ListUsersAsync(ct);
            return Results.Ok(users);
        });

        app.MapPost("/settings/users", async (AssignUserRoleRequest request, RbacService rbac, HttpContext http, CancellationToken ct) =>
        {
            var (allowed, forbidden, profile) = await CheckPermissionAsync(http, rbac, AppPermissions.SettingsRoles, ct);
            if (!allowed) return forbidden;

            if (string.IsNullOrWhiteSpace(request.ActorId))
            {
                return Results.BadRequest(new { error = "ActorId không được để trống.", code = "InvalidActorId" });
            }

            try
            {
                var user = await rbac.AssignUserRolesAsync(request.ActorId, request.DisplayName, request.RoleNames ?? [], ct);
                await rbac.LogAuditAsync(profile.ActorId, AppPermissions.SettingsRoles, "assign_user_roles", user.Id.ToString(), $"Assigned roles [{string.Join(", ", request.RoleNames ?? [])}] to {user.ActorId}", ct);
                return Results.Ok(new { success = true, user = user });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message, code = "UserAssignFailed" });
            }
        });

        // ══════════════════════════════════════════════════════════════════════
        // ── Facebook Page Content & Graph API Endpoints (Part C) ───────────
        // ══════════════════════════════════════════════════════════════════════
        app.MapPost("/facebook/page/sync-posts", async (
            FacebookPageClient fbClient,
            BootstrapDbContext db,
            RbacService rbac,
            IConfiguration config,
            HttpContext http,
            CancellationToken ct) =>
        {
            var (allowed, forbidden, profile) = await CheckPermissionAsync(http, rbac, AppPermissions.PagePostsRead, ct);
            if (!allowed) return forbidden;

            var pageToken = config["FACEBOOK_PAGE_ACCESS_TOKEN"] ?? config["Facebook:PageAccessToken"];
            var pageId = config["FACEBOOK_PAGE_ID"] ?? config["Facebook:PageId"] ?? "988656934325292";

            if (string.IsNullOrWhiteSpace(pageToken))
            {
                return Results.BadRequest(new { error = "FACEBOOK_PAGE_ACCESS_TOKEN is not configured.", code = "MissingToken" });
            }

            var posts = await fbClient.GetPagePostsAsync(pageId, pageToken, ct);
            int syncedCount = 0;

            var allExisting = await db.SocialPosts.ToListAsync(ct);

            foreach (var p in posts)
            {
                var msgPrefix = !string.IsNullOrWhiteSpace(p.Message) && p.Message.Length >= 20 ? p.Message[..20] : p.Message;
                var existingPost = allExisting.FirstOrDefault(sp =>
                    sp.PostId == p.Id ||
                    sp.PostId.EndsWith(p.Id) ||
                    (!string.IsNullOrWhiteSpace(msgPrefix) && !string.IsNullOrWhiteSpace(sp.Message) && sp.Message.Contains(msgPrefix, StringComparison.OrdinalIgnoreCase)));

                DateTimeOffset? createdTime = null;
                if (!string.IsNullOrWhiteSpace(p.CreatedTime) && DateTimeOffset.TryParse(p.CreatedTime, out var parsedCt))
                {
                    createdTime = parsedCt;
                }

                if (existingPost is null)
                {
                    existingPost = new SocialPostRecord
                    {
                        Id = $"post_{p.Id}",
                        PostId = p.Id,
                        PageId = pageId,
                        Message = p.Message,
                        PermalinkUrl = p.PermalinkUrl,
                        FullPicture = p.FullPicture,
                        MediaType = p.MediaType,
                        MediaUrl = p.MediaUrl,
                        ThumbnailUrl = p.ThumbnailUrl,
                        Status = "published",
                        ReactionCount = p.ReactionCount ?? 0,
                        CommentCount = p.CommentCount ?? 0,
                        ShareCount = p.ShareCount ?? 0,
                        CreatedTimeUtc = createdTime,
                        CreatedAtUtc = DateTimeOffset.UtcNow
                    };
                    db.SocialPosts.Add(existingPost);
                }
                else
                {
                    existingPost.Message = p.Message;
                    existingPost.PermalinkUrl = p.PermalinkUrl;
                    existingPost.FullPicture = p.FullPicture;
                    existingPost.MediaType = p.MediaType;
                    existingPost.MediaUrl = p.MediaUrl;
                    existingPost.ThumbnailUrl = p.ThumbnailUrl;
                    existingPost.ReactionCount = p.ReactionCount ?? 0;
                    existingPost.CommentCount = p.CommentCount ?? 0;
                    existingPost.ShareCount = p.ShareCount ?? 0;
                    if (createdTime.HasValue) existingPost.CreatedTimeUtc = createdTime;
                }

                // Fetch insights for this post
                var insights = await fbClient.GetPostInsightsAsync(p.Id, pageToken, ct);
                var metric = await db.SocialPostMetrics.FirstOrDefaultAsync(m => m.PostId == p.Id, ct);
                if (metric is null)
                {
                    metric = new SocialPostMetricRecord
                    {
                        Id = $"metric_{p.Id}",
                        PostId = p.Id,
                        Impressions = insights.Impressions,
                        EngagedUsers = insights.EngagedUsers,
                        Clicks = insights.Clicks,
                        Source = "graph",
                        DataFreshness = insights.DataFreshness,
                        FetchedAtUtc = DateTimeOffset.UtcNow
                    };
                    db.SocialPostMetrics.Add(metric);
                }
                else
                {
                    metric.Impressions = insights.Impressions;
                    metric.EngagedUsers = insights.EngagedUsers;
                    metric.Clicks = insights.Clicks;
                    metric.DataFreshness = insights.DataFreshness;
                    metric.FetchedAtUtc = DateTimeOffset.UtcNow;
                }

                syncedCount++;
            }

            await db.SaveChangesAsync(ct);
            await rbac.LogAuditAsync(profile.ActorId, AppPermissions.PagePostsRead, "sync_posts", pageId, $"Synced {syncedCount} posts from Facebook Page", ct);

            return Results.Ok(new { success = true, synced_count = syncedCount, page_id = pageId });
        });

        app.MapPost("/facebook/page/sync-inbox", async (
            FacebookPageClient fbClient,
            BootstrapDbContext db,
            RbacService rbac,
            IConfiguration config,
            LeadService leads,
            HttpContext http,
            CancellationToken ct) =>
        {
            var (allowed, forbidden, profile) = await CheckPermissionAsync(http, rbac, AppPermissions.InboxRead, ct);
            if (!allowed) return forbidden;

            var pageToken = config["FACEBOOK_PAGE_ACCESS_TOKEN"] ?? config["Facebook:PageAccessToken"];
            var pageId = config["FACEBOOK_PAGE_ID"] ?? config["Facebook:PageId"] ?? "988656934325292";

            if (string.IsNullOrWhiteSpace(pageToken))
            {
                return Results.BadRequest(new { error = "FACEBOOK_PAGE_ACCESS_TOKEN is not configured.", code = "MissingToken" });
            }

            var pageRecord = await db.SocialPages.FindAsync(new object[] { pageId }, ct);
            if (pageRecord is null)
            {
                pageRecord = new SocialPageRecord
                {
                    Id = pageId,
                    Name = "Facebook Fanpage",
                    Type = "facebook",
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                db.SocialPages.Add(pageRecord);
                await db.SaveChangesAsync(ct);
            }

            var convs = await fbClient.GetPageConversationsAsync(pageId, pageToken, ct);
            int syncedConvs = 0;
            int syncedMsgs = 0;

            foreach (var c in convs)
            {
                var sender = c.Senders.FirstOrDefault(s => s.Id != pageId) ?? c.Senders.FirstOrDefault();
                var senderId = sender?.Id ?? "unknown";
                var senderName = sender?.Name ?? "Khách hàng Facebook";
                var custId = $"fb_user_{senderId}";

                DateTimeOffset lastSeen = DateTimeOffset.UtcNow;
                if (!string.IsNullOrWhiteSpace(c.UpdatedTime) && DateTimeOffset.TryParse(c.UpdatedTime, out var parsedUt))
                {
                    lastSeen = parsedUt;
                }

                // Upsert customer
                var existingCust = await db.SocialCustomers.FindAsync(new object[] { custId }, ct);
                if (existingCust is null)
                {
                    existingCust = new SocialCustomerRecord
                    {
                        Id = custId,
                        Name = senderName,
                        PageId = pageId,
                        FirstSeenAt = lastSeen,
                        LastSeenAt = lastSeen,
                        CreatedAt = lastSeen,
                        UpdatedAt = lastSeen
                    };
                    db.SocialCustomers.Add(existingCust);
                }
                else
                {
                    existingCust.Name = senderName;
                    if (lastSeen > (existingCust.LastSeenAt ?? DateTimeOffset.MinValue))
                    {
                        existingCust.LastSeenAt = lastSeen;
                    }
                }
                await db.SaveChangesAsync(ct);

                // Upsert conversation
                var convId = c.Id.StartsWith("fb_") ? c.Id : $"fb_{c.Id}";
                var latestMsg = c.Messages.OrderByDescending(m => m.CreatedTime).FirstOrDefault();
                var snippet = latestMsg?.Message ?? "Tin nhắn Facebook";

                var existingConv = await db.SocialConversations.FindAsync(new object[] { convId }, ct);
                if (existingConv is null)
                {
                    existingConv = new SocialConversationRecord
                    {
                        Id = convId,
                        PageId = pageId,
                        CustomerId = custId,
                        CustomerName = senderName,
                        Snippet = snippet,
                        MessageCount = c.MessageCount ?? c.Messages.Count,
                        Status = "open",
                        InsertedAt = lastSeen,
                        UpdatedAt = lastSeen,
                        SyncedAt = DateTimeOffset.UtcNow
                    };
                    db.SocialConversations.Add(existingConv);
                }
                else
                {
                    existingConv.CustomerName = senderName;
                    existingConv.Snippet = snippet;
                    existingConv.UpdatedAt = lastSeen;
                    existingConv.MessageCount = Math.Max(existingConv.MessageCount, c.MessageCount ?? c.Messages.Count);
                }
                await db.SaveChangesAsync(ct);

                // Upsert messages
                foreach (var m in c.Messages)
                {
                    if (string.IsNullOrWhiteSpace(m.Id)) continue;
                    var msgId = m.Id.StartsWith("fb_msg_") ? m.Id : $"fb_msg_{m.Id}";
                    var existingMsg = await db.SocialMessages.FindAsync(new object[] { msgId }, ct);

                    DateTimeOffset msgTime = lastSeen;
                    if (!string.IsNullOrWhiteSpace(m.CreatedTime) && DateTimeOffset.TryParse(m.CreatedTime, out var parsedMt))
                    {
                        msgTime = parsedMt;
                    }

                    var isAgent = m.From?.Id == pageId;
                    var senderType = isAgent ? "agent" : "customer";

                    if (existingMsg is null)
                    {
                        var newMsg = new SocialMessageRecord
                        {
                            Id = msgId,
                            ConversationId = convId,
                            PageId = pageId,
                            SenderId = m.From?.Id ?? custId,
                            SenderName = m.From?.Name ?? (isAgent ? "Fanpage" : senderName),
                            SenderType = senderType,
                            Content = m.Message ?? "",
                            MessageType = !string.IsNullOrWhiteSpace(m.AttachmentUrl) ? (m.AttachmentType ?? "image") : "text",
                            AttachmentsJson = !string.IsNullOrWhiteSpace(m.AttachmentUrl) ? JsonSerializer.Serialize(new[] { m.AttachmentUrl }) : "[]",
                            CreatedTime = msgTime,
                            CreatedAt = msgTime,
                            SyncedAt = DateTimeOffset.UtcNow
                        };
                        db.SocialMessages.Add(newMsg);
                        syncedMsgs++;

                        // Phone extraction from customer messages
                        if (!isAgent && !string.IsNullOrWhiteSpace(m.Message))
                        {
                            var phones = PhoneExtractor.ExtractAllPhoneNumbers(m.Message);
                            if (phones.Count > 0)
                            {
                                existingConv.HasPhone = true;
                                existingConv.CustomerPhone = phones[0];
                                existingCust.PhoneNumbersJson = JsonSerializer.Serialize(phones);
                            }
                        }
                    }
                }

                syncedConvs++;
            }

            // Update page stats
            pageRecord = await db.SocialPages.FindAsync(new object[] { pageId }, ct);
            if (pageRecord is not null)
            {
                pageRecord.TotalConversations = await db.SocialConversations.CountAsync(c => c.PageId == pageId, ct);
                pageRecord.TotalMessages = await db.SocialMessages.CountAsync(m => m.PageId == pageId, ct);
                pageRecord.LastSyncAt = DateTimeOffset.UtcNow;
            }

            await db.SaveChangesAsync(ct);
            await rbac.LogAuditAsync(profile.ActorId, AppPermissions.InboxRead, "sync_inbox", pageId, $"Synced {syncedConvs} conversations and {syncedMsgs} messages from Facebook Page", ct);

            return Results.Ok(new { success = true, synced_conversations = syncedConvs, synced_messages = syncedMsgs, page_id = pageId });
        });

        app.MapPost("/facebook/page/sync-all", async (
            FacebookPageSyncAllRequest? req,
            FacebookPageClient fbClient,
            BootstrapDbContext db,
            RbacService rbac,
            IConfiguration config,
            HttpContext http,
            CancellationToken ct) =>
        {
            var (allowed, forbidden, profile) = await CheckPermissionAsync(http, rbac, AppPermissions.PagePostsRead, ct);
            if (!allowed) return forbidden;

            var pageToken = !string.IsNullOrWhiteSpace(req?.PageAccessToken)
                ? req.PageAccessToken
                : (config["FACEBOOK_PAGE_ACCESS_TOKEN"] ?? config["Facebook:PageAccessToken"]);
            var pageId = !string.IsNullOrWhiteSpace(req?.PageId)
                ? req.PageId
                : (config["FACEBOOK_PAGE_ID"] ?? config["Facebook:PageId"] ?? "988656934325292");

            if (string.IsNullOrWhiteSpace(pageToken))
            {
                return Results.BadRequest(new
                {
                    error = "Chưa có Facebook Page Access Token. Vui lòng nhập token của Fanpage để đồng bộ dữ liệu thật.",
                    code = "MissingToken"
                });
            }

            // 1. Check page info
            var pageInfo = await fbClient.GetPageAsync(pageId, pageToken, ct);
            if (pageInfo is null)
            {
                return Results.BadRequest(new
                {
                    error = "Không thể kết nối Facebook Graph API. Token đã hết hạn hoặc không có quyền truy cập Page ID này.",
                    code = "InvalidOrExpiredToken",
                    page_id = pageId
                });
            }

            // Update or create SocialPageRecord
            var pageRecord = await db.SocialPages.FindAsync(new object[] { pageId }, ct);
            if (pageRecord is null)
            {
                pageRecord = new SocialPageRecord
                {
                    Id = pageId,
                    Name = pageInfo.Name ?? "SEO Trùm Fanpage",
                    Type = "facebook",
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                db.SocialPages.Add(pageRecord);
            }
            else
            {
                pageRecord.Name = pageInfo.Name ?? pageRecord.Name;
                pageRecord.UpdatedAt = DateTimeOffset.UtcNow;
            }
            await db.SaveChangesAsync(ct);

            // 2. Sync Posts & Metrics
            var posts = await fbClient.GetPagePostsAsync(pageId, pageToken, ct);
            int syncedPosts = 0;
            var allExistingPosts = await db.SocialPosts.Where(p => p.PageId == pageId).ToListAsync(ct);

            foreach (var p in posts)
            {
                var msgPrefix = !string.IsNullOrWhiteSpace(p.Message) && p.Message.Length >= 20 ? p.Message[..20] : p.Message;
                var existingPost = allExistingPosts.FirstOrDefault(sp =>
                    sp.PostId == p.Id ||
                    sp.PostId.EndsWith(p.Id) ||
                    (!string.IsNullOrWhiteSpace(msgPrefix) && !string.IsNullOrWhiteSpace(sp.Message) && sp.Message.Contains(msgPrefix, StringComparison.OrdinalIgnoreCase)));

                DateTimeOffset? createdTime = null;
                if (!string.IsNullOrWhiteSpace(p.CreatedTime) && DateTimeOffset.TryParse(p.CreatedTime, out var parsedCt))
                {
                    createdTime = parsedCt;
                }

                if (existingPost is null)
                {
                    existingPost = new SocialPostRecord
                    {
                        Id = $"post_{p.Id}",
                        PostId = p.Id,
                        PageId = pageId,
                        Message = p.Message,
                        PermalinkUrl = p.PermalinkUrl,
                        FullPicture = p.FullPicture,
                        MediaType = p.MediaType,
                        MediaUrl = p.MediaUrl,
                        ThumbnailUrl = p.ThumbnailUrl,
                        Status = "published",
                        ReactionCount = p.ReactionCount ?? 0,
                        CommentCount = p.CommentCount ?? 0,
                        ShareCount = p.ShareCount ?? 0,
                        CreatedTimeUtc = createdTime,
                        CreatedAtUtc = DateTimeOffset.UtcNow
                    };
                    db.SocialPosts.Add(existingPost);
                }
                else
                {
                    existingPost.Message = p.Message;
                    existingPost.PermalinkUrl = p.PermalinkUrl;
                    existingPost.FullPicture = p.FullPicture;
                    existingPost.MediaType = p.MediaType;
                    existingPost.MediaUrl = p.MediaUrl;
                    existingPost.ThumbnailUrl = p.ThumbnailUrl;
                    existingPost.ReactionCount = p.ReactionCount ?? 0;
                    existingPost.CommentCount = p.CommentCount ?? 0;
                    existingPost.ShareCount = p.ShareCount ?? 0;
                    if (createdTime.HasValue) existingPost.CreatedTimeUtc = createdTime;
                }

                // Insights
                var insights = await fbClient.GetPostInsightsAsync(p.Id, pageToken, ct);
                var metric = await db.SocialPostMetrics.FirstOrDefaultAsync(m => m.PostId == p.Id, ct);
                if (metric is null)
                {
                    metric = new SocialPostMetricRecord
                    {
                        Id = $"metric_{p.Id}",
                        PostId = p.Id,
                        Impressions = insights.Impressions,
                        EngagedUsers = insights.EngagedUsers,
                        Clicks = insights.Clicks,
                        Source = "graph",
                        DataFreshness = insights.DataFreshness,
                        FetchedAtUtc = DateTimeOffset.UtcNow
                    };
                    db.SocialPostMetrics.Add(metric);
                }
                else
                {
                    metric.Impressions = insights.Impressions;
                    metric.EngagedUsers = insights.EngagedUsers;
                    metric.Clicks = insights.Clicks;
                    metric.DataFreshness = insights.DataFreshness;
                    metric.FetchedAtUtc = DateTimeOffset.UtcNow;
                }

                syncedPosts++;
            }

            // 3. Sync Conversations & Messages
            var convs = await fbClient.GetPageConversationsAsync(pageId, pageToken, ct);
            int syncedConvs = 0;
            int syncedMsgs = 0;

            foreach (var c in convs)
            {
                var sender = c.Senders.FirstOrDefault(s => s.Id != pageId) ?? c.Senders.FirstOrDefault();
                var senderId = sender?.Id ?? "unknown";
                var senderName = sender?.Name ?? "Khách hàng Facebook";
                var custId = $"fb_user_{senderId}";

                DateTimeOffset lastSeen = DateTimeOffset.UtcNow;
                if (!string.IsNullOrWhiteSpace(c.UpdatedTime) && DateTimeOffset.TryParse(c.UpdatedTime, out var parsedUt))
                {
                    lastSeen = parsedUt;
                }

                // Upsert customer
                var existingCust = await db.SocialCustomers.FindAsync(new object[] { custId }, ct);
                if (existingCust is null)
                {
                    existingCust = new SocialCustomerRecord
                    {
                        Id = custId,
                        Name = senderName,
                        PageId = pageId,
                        FirstSeenAt = lastSeen,
                        LastSeenAt = lastSeen,
                        CreatedAt = lastSeen,
                        UpdatedAt = lastSeen
                    };
                    db.SocialCustomers.Add(existingCust);
                }
                else
                {
                    existingCust.Name = senderName;
                    if (lastSeen > (existingCust.LastSeenAt ?? DateTimeOffset.MinValue))
                    {
                        existingCust.LastSeenAt = lastSeen;
                    }
                }
                await db.SaveChangesAsync(ct);

                var convId = c.Id.StartsWith("fb_") ? c.Id : $"fb_{c.Id}";
                var latestMsg = c.Messages.OrderByDescending(m => m.CreatedTime).FirstOrDefault();
                var snippet = latestMsg?.Message ?? "Tin nhắn Facebook";

                var existingConv = await db.SocialConversations.FindAsync(new object[] { convId }, ct);
                if (existingConv is null)
                {
                    existingConv = new SocialConversationRecord
                    {
                        Id = convId,
                        PageId = pageId,
                        CustomerId = custId,
                        CustomerName = senderName,
                        Snippet = snippet,
                        MessageCount = c.MessageCount ?? c.Messages.Count,
                        Status = "open",
                        InsertedAt = lastSeen,
                        UpdatedAt = lastSeen,
                        SyncedAt = DateTimeOffset.UtcNow
                    };
                    db.SocialConversations.Add(existingConv);
                }
                else
                {
                    existingConv.CustomerName = senderName;
                    existingConv.Snippet = snippet;
                    existingConv.UpdatedAt = lastSeen;
                    existingConv.MessageCount = Math.Max(existingConv.MessageCount, c.MessageCount ?? c.Messages.Count);
                }
                await db.SaveChangesAsync(ct);

                foreach (var m in c.Messages)
                {
                    if (string.IsNullOrWhiteSpace(m.Id)) continue;
                    var msgId = m.Id.StartsWith("fb_msg_") ? m.Id : $"fb_msg_{m.Id}";
                    var existingMsg = await db.SocialMessages.FindAsync(new object[] { msgId }, ct);

                    DateTimeOffset msgTime = lastSeen;
                    if (!string.IsNullOrWhiteSpace(m.CreatedTime) && DateTimeOffset.TryParse(m.CreatedTime, out var parsedMt))
                    {
                        msgTime = parsedMt;
                    }

                    var isAgent = m.From?.Id == pageId;
                    var senderType = isAgent ? "agent" : "customer";

                    if (existingMsg is null)
                    {
                        var newMsg = new SocialMessageRecord
                        {
                            Id = msgId,
                            ConversationId = convId,
                            PageId = pageId,
                            SenderId = m.From?.Id ?? custId,
                            SenderName = m.From?.Name ?? (isAgent ? "Fanpage" : senderName),
                            SenderType = senderType,
                            Content = m.Message ?? "",
                            MessageType = !string.IsNullOrWhiteSpace(m.AttachmentUrl) ? (m.AttachmentType ?? "image") : "text",
                            AttachmentsJson = !string.IsNullOrWhiteSpace(m.AttachmentUrl) ? JsonSerializer.Serialize(new[] { m.AttachmentUrl }) : "[]",
                            CreatedTime = msgTime,
                            CreatedAt = msgTime,
                            SyncedAt = DateTimeOffset.UtcNow
                        };
                        db.SocialMessages.Add(newMsg);
                        syncedMsgs++;

                        if (!isAgent && !string.IsNullOrWhiteSpace(m.Message))
                        {
                            var phones = PhoneExtractor.ExtractAllPhoneNumbers(m.Message);
                            if (phones.Count > 0)
                            {
                                existingConv.HasPhone = true;
                                existingConv.CustomerPhone = phones[0];
                                existingCust.PhoneNumbersJson = JsonSerializer.Serialize(phones);
                            }
                        }
                    }
                }

                syncedConvs++;
            }

            pageRecord.TotalConversations = await db.SocialConversations.CountAsync(c => c.PageId == pageId, ct);
            pageRecord.TotalMessages = await db.SocialMessages.CountAsync(m => m.PageId == pageId, ct);
            pageRecord.LastSyncAt = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync(ct);
            await rbac.LogAuditAsync(profile.ActorId, AppPermissions.PagePostsRead, "sync_all", pageId, $"Synced {syncedPosts} posts, {syncedConvs} convs, {syncedMsgs} msgs from Facebook Page", ct);

            return Results.Ok(new
            {
                success = true,
                page_id = pageId,
                page_name = pageInfo.Name,
                fan_count = pageInfo.FanCount,
                followers_count = pageInfo.FollowersCount,
                posts_synced = syncedPosts,
                conversations_synced = syncedConvs,
                messages_synced = syncedMsgs,
                message = $"Đã đồng bộ thành công {syncedPosts} bài viết, {syncedConvs} hội thoại và {syncedMsgs} tin nhắn thật từ Fanpage {pageInfo.Name}!"
            });
        });

        app.MapGet("/facebook/posts", async (
            string? status,
            BootstrapDbContext db,
            RbacService rbac,
            HttpContext http,
            CancellationToken ct) =>
        {
            var (allowed, forbidden, _) = await CheckPermissionAsync(http, rbac, AppPermissions.PagePostsRead, ct);
            if (!allowed) return forbidden;

            var query = db.SocialPosts.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => p.Status == status);
            }

            var posts = await query.OrderByDescending(p => p.CreatedTimeUtc ?? p.ScheduledAtUtc ?? p.CreatedAtUtc).Take(50).ToListAsync(ct);
            var postIds = posts.Select(p => p.PostId).ToList();

            var metrics = await db.SocialPostMetrics.AsNoTracking()
                .Where(m => postIds.Contains(m.PostId))
                .ToDictionaryAsync(m => m.PostId, ct);

            var commentCounts = await db.SocialComments.AsNoTracking()
                .Where(c => postIds.Contains(c.PostId))
                .GroupBy(c => c.PostId)
                .Select(g => new { PostId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.PostId, g => g.Count, ct);

            return Results.Ok(posts.Select(p =>
            {
                metrics.TryGetValue(p.PostId, out var m);
                commentCounts.TryGetValue(p.PostId, out var cCount);

                long? totalComments = p.CommentCount;
                if (!totalComments.HasValue && cCount > 0)
                {
                    totalComments = cCount;
                }

                return new
                {
                    id = p.Id,
                    post_id = p.PostId,
                    page_id = p.PageId,
                    message = p.Message,
                    permalink_url = p.PermalinkUrl,
                    full_picture = p.FullPicture,
                    media_type = p.MediaType,
                    media_url = p.MediaUrl,
                    thumbnail_url = p.ThumbnailUrl,
                    status = p.Status,
                    scheduled_at = p.ScheduledAtUtc,
                    graph_scheduled = p.GraphScheduled,
                    created_time = p.CreatedTimeUtc,
                    created_at = p.CreatedAtUtc,
                    impressions = m?.Impressions ?? 0,
                    engaged_users = m?.EngagedUsers ?? 0,
                    clicks = m?.Clicks ?? 0,
                    reaction_count = p.ReactionCount,
                    comment_count = totalComments,
                    share_count = p.ShareCount,
                    data_freshness = m?.DataFreshness ?? "none"
                };
            }));
        });

        app.MapGet("/facebook/posts/{id}/comments", async (
            string id,
            FacebookPageClient fbClient,
            BootstrapDbContext db,
            RbacService rbac,
            IConfiguration config,
            HttpContext http,
            CancellationToken ct) =>
        {
            var (allowed, forbidden, _) = await CheckPermissionAsync(http, rbac, AppPermissions.PageCommentsRead, ct);
            if (!allowed) return forbidden;

            var pageToken = config["FACEBOOK_PAGE_ACCESS_TOKEN"] ?? config["Facebook:PageAccessToken"];
            var cleanPostId = id.StartsWith("post_") ? id.Substring("post_".Length) : id;

            if (!string.IsNullOrWhiteSpace(pageToken))
            {
                var fbCommentsRes = await fbClient.GetPostCommentsAsync(cleanPostId, pageToken, ct);
                foreach (var c in fbCommentsRes.Comments)
                {
                    var exists = await db.SocialComments.AnyAsync(sc => sc.CommentId == c.Id, ct);
                    if (!exists)
                    {
                        DateTimeOffset? cTime = null;
                        if (!string.IsNullOrWhiteSpace(c.CreatedTime) && DateTimeOffset.TryParse(c.CreatedTime, out var parsed))
                        {
                            cTime = parsed;
                        }

                        db.SocialComments.Add(new SocialCommentRecord
                        {
                            Id = $"comment_{c.Id}",
                            CommentId = c.Id,
                            PostId = cleanPostId,
                            FromId = c.From?.Id,
                            FromName = c.From?.Name,
                            Message = c.Message,
                            CreatedTimeUtc = cTime,
                            CreatedAtUtc = DateTimeOffset.UtcNow
                        });
                    }
                }
                await db.SaveChangesAsync(ct);
            }

            var list = await db.SocialComments.AsNoTracking()
                .Where(c => c.PostId == cleanPostId)
                .OrderBy(c => c.CreatedTimeUtc ?? c.CreatedAtUtc)
                .ToListAsync(ct);

            return Results.Ok(list.Select(c => new
            {
                id = c.Id,
                comment_id = c.CommentId,
                post_id = c.PostId,
                from_id = c.FromId,
                from_name = c.FromName,
                message = c.Message,
                created_time = c.CreatedTimeUtc,
                created_at = c.CreatedAtUtc
            }));
        });

        app.MapPost("/facebook/posts/{id}/comments", async (
            string id,
            SendSocialMessageRequest request,
            FacebookPageClient fbClient,
            BootstrapDbContext db,
            RbacService rbac,
            IConfiguration config,
            HttpContext http,
            CancellationToken ct) =>
        {
            var (allowed, forbidden, profile) = await CheckPermissionAsync(http, rbac, AppPermissions.PageCommentsReply, ct);
            if (!allowed) return forbidden;

            var message = request.Content ?? request.Message;
            if (string.IsNullOrWhiteSpace(message))
            {
                return Results.BadRequest(new { error = "Nội dung bình luận là bắt buộc." });
            }

            var pageToken = config["FACEBOOK_PAGE_ACCESS_TOKEN"] ?? config["Facebook:PageAccessToken"];
            var postId = id.StartsWith("post_") ? id.Substring("post_".Length) : id;
            var targetId = !string.IsNullOrWhiteSpace(request.ParentCommentId) ? request.ParentCommentId : postId;

            string? createdCommentId = null;
            if (!string.IsNullOrWhiteSpace(pageToken))
            {
                createdCommentId = await fbClient.ReplyCommentAsync(targetId, message, pageToken, ct);
            }
            else
            {
                createdCommentId = $"mock_reply_{Guid.NewGuid():N}";
            }

            var record = new SocialCommentRecord
            {
                Id = $"comment_{createdCommentId ?? Guid.NewGuid().ToString("N")}",
                CommentId = createdCommentId ?? Guid.NewGuid().ToString("N"),
                PostId = postId,
                FromId = "page_admin",
                FromName = "Royce Shop",
                Message = message,
                ParentCommentId = request.ParentCommentId,
                CreatedTimeUtc = DateTimeOffset.UtcNow,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            db.SocialComments.Add(record);
            await db.SaveChangesAsync(ct);

            await rbac.LogAuditAsync(profile.ActorId, AppPermissions.PageCommentsReply, "reply_comment", postId, $"Replied to {targetId}: {message}", ct);

            return Results.Ok(new { success = true, id = record.Id, comment_id = record.CommentId, message = record.Message });
        });

        app.MapPost("/facebook/posts", async (
            PublishFacebookPostRequest request,
            FacebookPageClient fbClient,
            CampaignService campaigns,
            BootstrapDbContext db,
            RbacService rbac,
            IConfiguration config,
            HttpContext http,
            CancellationToken ct) =>
        {
            var (allowed, forbidden, profile) = await CheckPermissionAsync(http, rbac, AppPermissions.PagePublish, ct);
            if (!allowed) return forbidden;

            var message = request.Message ?? request.Content;
            if (string.IsNullOrWhiteSpace(message))
            {
                return Results.BadRequest(new { error = "Nội dung bài viết là bắt buộc." });
            }

            // If a campaign ID is attached, require that the campaign is approved or published
            if (request.CampaignId.HasValue && request.CampaignId.Value != Guid.Empty)
            {
                var cmp = await campaigns.GetAsync(request.CampaignId.Value, ct);
                if (cmp is null)
                {
                    return Results.BadRequest(new { error = $"Chiến dịch '{request.CampaignId}' không tồn tại.", code = "CampaignNotFound" });
                }
                if (cmp.Status != CampaignStatus.Published)
                {
                    return Results.BadRequest(new
                    {
                        error = $"Chiến dịch '{cmp.Topic}' đang ở trạng thái '{cmp.Status}'. Bài đăng yêu cầu chiến dịch phải được Chủ Doanh Nghiệp phê duyệt (Published) trước khi đăng.",
                        code = "CampaignNotApproved"
                    });
                }
            }

            DateTimeOffset? scheduledUtc = null;
            bool isScheduled = false;
            if (!string.IsNullOrWhiteSpace(request.ScheduledAt))
            {
                if (!DateTimeOffset.TryParse(request.ScheduledAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedSched))
                {
                    return Results.BadRequest(new { error = "Định dạng thời gian hẹn giờ không hợp lệ.", code = "InvalidScheduleTime" });
                }

                scheduledUtc = parsedSched.ToUniversalTime();
                var now = DateTimeOffset.UtcNow;

                // Graph schedule window: now + 10 minutes to now + 6 months (approx 180 days)
                if (scheduledUtc < now.AddMinutes(9).AddSeconds(30) || scheduledUtc > now.AddDays(180))
                {
                    return Results.BadRequest(new
                    {
                        error = "Thời gian hẹn giờ đăng bài phải từ 10 phút đến 6 tháng kể từ thời điểm hiện tại.",
                        code = "InvalidScheduleWindow"
                    });
                }

                isScheduled = true;
            }

            var pageToken = config["FACEBOOK_PAGE_ACCESS_TOKEN"] ?? config["Facebook:PageAccessToken"];
            var pageId = config["FACEBOOK_PAGE_ID"] ?? config["Facebook:PageId"] ?? "988656934325292";

            if (string.IsNullOrWhiteSpace(pageToken))
            {
                return Results.BadRequest(new
                {
                    error = "Chưa cấu hình FACEBOOK_PAGE_ACCESS_TOKEN trong hệ thống.",
                    code = "MissingToken"
                });
            }

            var publishRes = await fbClient.PublishPostAsync(pageId, message, pageToken, mediaUrl: request.MediaUrl, mediaType: request.MediaType, scheduledPublishTime: isScheduled ? scheduledUtc : null, ct);
            if (!publishRes.Ok)
            {
                return Results.Json(new
                {
                    error = publishRes.ErrorMessage ?? "Đăng bài lên Facebook thất bại.",
                    code = publishRes.ErrorCode ?? "GraphPublishFailed"
                }, statusCode: StatusCodes.Status502BadGateway);
            }

            var publishedFbId = publishRes.GraphPostId;
            var graphScheduled = isScheduled;

            var postRecord = new SocialPostRecord
            {
                Id = $"post_{publishedFbId}",
                PostId = publishedFbId ?? Guid.NewGuid().ToString("N"),
                PageId = pageId,
                Message = message,
                MediaUrl = request.MediaUrl,
                MediaType = request.MediaType,
                FullPicture = request.MediaUrl,
                ThumbnailUrl = request.MediaUrl,
                Status = isScheduled ? "scheduled" : "published",
                ScheduledAtUtc = isScheduled ? scheduledUtc : null,
                GraphScheduled = graphScheduled,
                ReactionCount = 0,
                CommentCount = 0,
                ShareCount = 0,
                CreatedTimeUtc = isScheduled ? null : DateTimeOffset.UtcNow,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            db.SocialPosts.Add(postRecord);
            await db.SaveChangesAsync(ct);

            var auditAction = isScheduled ? "schedule_post" : "publish_post";
            var auditDetails = isScheduled
                ? $"Scheduled post for {scheduledUtc:yyyy-MM-dd HH:mm:ss} UTC: {message.Substring(0, Math.Min(50, message.Length))}"
                : $"Published post: {message.Substring(0, Math.Min(50, message.Length))}";

            await rbac.LogAuditAsync(profile.ActorId, AppPermissions.PagePublish, auditAction, postRecord.PostId, auditDetails, ct);

            return Results.Ok(new
            {
                success = true,
                post_id = postRecord.PostId,
                message = postRecord.Message,
                status = postRecord.Status,
                scheduled_at = postRecord.ScheduledAtUtc,
                graph_scheduled = postRecord.GraphScheduled,
                created_time = postRecord.CreatedTimeUtc
            });
        });

        app.MapPost("/facebook/posts/{id}/cancel-schedule", async (
            string id,
            FacebookPageClient fbClient,
            BootstrapDbContext db,
            RbacService rbac,
            IConfiguration config,
            HttpContext http,
            CancellationToken ct) =>
        {
            var (allowed, forbidden, profile) = await CheckPermissionAsync(http, rbac, AppPermissions.PagePublish, ct);
            if (!allowed) return forbidden;

            var cleanId = id.StartsWith("post_") ? id.Substring("post_".Length) : id;
            var post = await db.SocialPosts.FirstOrDefaultAsync(p => p.Id == id || p.PostId == cleanId || p.PostId == id, ct);
            if (post is null)
            {
                return Results.NotFound(new { error = "Không tìm thấy bài viết.", code = "PostNotFound" });
            }

            if (post.Status != "scheduled")
            {
                return Results.BadRequest(new { error = "Bài viết không ở trạng thái đã lên lịch.", code = "PostNotScheduled" });
            }

            var pageToken = config["FACEBOOK_PAGE_ACCESS_TOKEN"] ?? config["Facebook:PageAccessToken"];
            if (post.GraphScheduled && !string.IsNullOrWhiteSpace(pageToken))
            {
                await fbClient.CancelScheduledPostAsync(post.PostId, pageToken, ct);
            }

            post.Status = "cancelled";
            await db.SaveChangesAsync(ct);

            await rbac.LogAuditAsync(profile.ActorId, AppPermissions.PagePublish, "cancel_schedule", post.PostId, $"Cancelled scheduled post {post.PostId}", ct);

            return Results.Ok(new { success = true, post_id = post.PostId, status = post.Status });
        });

        app.MapPost("/facebook/posts/{id}/sync-insights", async (
            string id,
            FacebookPageClient fbClient,
            BootstrapDbContext db,
            RbacService rbac,
            IConfiguration config,
            HttpContext http,
            CancellationToken ct) =>
        {
            var (allowed, forbidden, profile) = await CheckPermissionAsync(http, rbac, AppPermissions.PageInsightsRead, ct);
            if (!allowed) return forbidden;

            var pageToken = config["FACEBOOK_PAGE_ACCESS_TOKEN"] ?? config["Facebook:PageAccessToken"];
            var cleanPostId = id.StartsWith("post_") ? id.Substring("post_".Length) : id;

            if (string.IsNullOrWhiteSpace(pageToken))
            {
                return Results.BadRequest(new { error = "FACEBOOK_PAGE_ACCESS_TOKEN is not configured." });
            }

            var insights = await fbClient.GetPostInsightsAsync(cleanPostId, pageToken, ct);
            var metric = await db.SocialPostMetrics.FirstOrDefaultAsync(m => m.PostId == cleanPostId, ct);
            if (metric is null)
            {
                metric = new SocialPostMetricRecord
                {
                    Id = $"metric_{cleanPostId}",
                    PostId = cleanPostId,
                    Impressions = insights.Impressions,
                    EngagedUsers = insights.EngagedUsers,
                    Clicks = insights.Clicks,
                    Source = "graph",
                    DataFreshness = insights.DataFreshness,
                    FetchedAtUtc = DateTimeOffset.UtcNow
                };
                db.SocialPostMetrics.Add(metric);
            }
            else
            {
                metric.Impressions = insights.Impressions;
                metric.EngagedUsers = insights.EngagedUsers;
                metric.Clicks = insights.Clicks;
                metric.DataFreshness = insights.DataFreshness;
                metric.FetchedAtUtc = DateTimeOffset.UtcNow;
            }
            await db.SaveChangesAsync(ct);

            return Results.Ok(new
            {
                success = true,
                post_id = cleanPostId,
                impressions = metric.Impressions,
                engaged_users = metric.EngagedUsers,
                clicks = metric.Clicks,
                data_freshness = metric.DataFreshness,
                fetched_at = metric.FetchedAtUtc
            });
        });

        app.MapGet("/facebook/page/health", async (
            string? page_id,
            PageHealthService healthService,
            RbacService rbac,
            IConfiguration config,
            HttpContext http,
            CancellationToken ct) =>
        {
            var (allowed, forbidden, profile) = await CheckPermissionAsync(http, rbac, AppPermissions.PagePostsRead, ct);
            if (!allowed) return forbidden;

            var targetPageId = page_id ?? config["FACEBOOK_PAGE_ID"] ?? "988656934325292";
            var (evaluation, commentsStatus) = await healthService.GetHealthEvaluationWithStatusAsync(targetPageId, ct);
            return Results.Ok(new
            {
                overallScore = evaluation.OverallScore,
                label = evaluation.Label,
                axes = evaluation.Axes,
                reasons = evaluation.Reasons,
                modelId = evaluation.ModelId,
                version = evaluation.Version,
                commentsStatus
            });
        });

        app.MapGet("/facebook/page/inbox-actions", async (
            string? page_id,
            PageHealthService healthService,
            DXOS.Application.Abstractions.IChatClient chatClient,
            RbacService rbac,
            IConfiguration config,
            HttpContext http,
            CancellationToken ct) =>
        {
            var (allowed, forbidden, profile) = await CheckPermissionAsync(http, rbac, AppPermissions.InboxRead, ct);
            if (!allowed) return forbidden;

            var targetPageId = page_id ?? config["FACEBOOK_PAGE_ID"] ?? "988656934325292";
            var items = await healthService.GetInboxActionsAsync(targetPageId, chatClient, 10, ct);
            return Results.Ok(items.Select(i => new
            {
                id = i.Id,
                customer_name = i.CustomerName,
                snippet = i.Snippet,
                customer_phone = i.CustomerPhone,
                assigned_to_actor = i.AssignedToActor,
                suggestedReply = i.SuggestedReply
            }));
        });

        app.MapPost("/facebook/page/advise", async (
            PageAdviceRequest? request,
            PageHealthService healthService,
            DXOS.Application.Abstractions.IChatClient chatClient,
            RbacService rbac,
            IConfiguration config,
            HttpContext http,
            CancellationToken ct) =>
        {
            var (allowed, forbidden, profile) = await CheckPermissionAsync(http, rbac, AppPermissions.PagePostsRead, ct);
            if (!allowed) return forbidden;

            var targetPageId = request?.PageId ?? config["FACEBOOK_PAGE_ID"] ?? "988656934325292";
            var advice = await healthService.GetPageAdviceAsync(targetPageId, chatClient, ct);
            return Results.Ok(advice);
        });

        app.MapPost("/facebook/page/agent/run", async (
            PageAgentRunRequest? request,
            PageAgentService agentService,
            DXOS.Application.Abstractions.IChatClient chatClient,
            RbacService rbac,
            IConfiguration config,
            HttpContext http,
            CancellationToken ct) =>
        {
            var (allowed, forbidden, profile) = await CheckPermissionAsync(http, rbac, AppPermissions.PagePostsRead, ct);
            if (!allowed) return forbidden;

            var targetPageId = request?.PageId ?? config["FACEBOOK_PAGE_ID"] ?? "988656934325292";
            var result = await agentService.RunAsync(targetPageId, chatClient, ct);
            return Results.Ok(result);
        });

        app.MapGet("/messages", async (BootstrapDbContext db, RbacService rbac, HttpContext http, CancellationToken ct) =>
        {
            var (allowed, forbidden, _) = await CheckPermissionAsync(http, rbac, AppPermissions.InboxRead, ct);
            if (!allowed) return forbidden;

            var list = await db.SocialMessages.AsNoTracking().OrderByDescending(m => m.CreatedTime).Take(1000).ToListAsync(ct);
            return Results.Ok(list.Select(m => new
            {
                id = m.Id,
                conversation_id = m.ConversationId,
                page_id = m.PageId,
                sender_id = m.SenderId,
                sender_name = m.SenderName,
                sender_type = m.SenderType,
                content = m.Content,
                message_type = m.MessageType ?? "text",
                attachments_json = m.AttachmentsJson,
                created_time = m.CreatedTime
            }));
        });

        app.MapGet("/analytics/leads-by-platform", async (LeadService leads, HttpContext http, CancellationToken cancellationToken) =>
        {
            try
            {
                ReadActor(http);
                var rows = await leads.SummarizeByPlatformAsync(cancellationToken);
                return Results.Ok(new
                {
                    fetchedAt = DateTimeOffset.UtcNow,
                    source = "unified-data-layer",
                    dataFreshness = "demo",
                    adsLive = false,
                    platforms = rows.Select(r => new
                    {
                        provider = r.Provider,
                        leadCount = r.LeadCount,
                        hotCount = r.HotCount,
                        warmCount = r.WarmCount,
                        coldCount = r.ColdCount,
                        convertedCount = r.ConvertedCount,
                        revenueVnd = r.RevenueVnd
                    }).ToList()
                });
            }
            catch (DomainRuleException ex)
            {
                return MapDomainException(ex);
            }
        });

        app.MapPost("/leads/webhook", async (FormLeadRequest request, LeadService leads, HttpContext http, CancellationToken cancellationToken) =>
        {
            try
            {
                ReadActor(http);
                var lead = await leads.IntakeFormAsync(request.Name ?? string.Empty, request.Phone, request.Email, request.CampaignId, cancellationToken);
                return Results.Ok(ToLeadResponse(lead, DateTimeOffset.UtcNow));
            }
            catch (DomainRuleException ex)
            {
                return MapDomainException(ex);
            }
        });

        app.MapPost("/leads/message", async (FormLeadRequest request, LeadService leads, HttpContext http, CancellationToken cancellationToken) =>
        {
            try
            {
                ReadActor(http);
                var lead = await leads.RecordMessageOrCallAsync(request.Name ?? string.Empty, request.Phone, request.Email, LeadSource.Message, request.CampaignId, cancellationToken);
                return Results.Ok(ToLeadResponse(lead, DateTimeOffset.UtcNow));
            }
            catch (DomainRuleException ex)
            {
                return MapDomainException(ex);
            }
        });

        app.MapPost("/leads/call", async (FormLeadRequest request, LeadService leads, HttpContext http, CancellationToken cancellationToken) =>
        {
            try
            {
                ReadActor(http);
                var lead = await leads.RecordMessageOrCallAsync(request.Name ?? string.Empty, request.Phone, request.Email, LeadSource.Call, request.CampaignId, cancellationToken);
                return Results.Ok(ToLeadResponse(lead, DateTimeOffset.UtcNow));
            }
            catch (DomainRuleException ex)
            {
                return MapDomainException(ex);
            }
        });

        app.MapPost("/api/seed", async (SocialSeedService socialSeed, DemoSeedService demoSeed, HttpContext http, CancellationToken cancellationToken) =>
        {
            var socialCount = await socialSeed.SeedAsync(cancellationToken);
            try { await demoSeed.SeedAsync(cancellationToken); } catch { }
            return Results.Ok(new
            {
                success = true,
                seededCustomers = socialCount,
                message = "Đã nạp dữ liệu mẫu SEO Trùm Social CRM thành công!"
            });
        });

        app.MapPost("/demo/seed", async (DemoSeedService seed, SocialSeedService socialSeed, HttpContext http, CancellationToken cancellationToken) =>
        {
            try
            {
                ReadActor(http);
                var result = await seed.SeedAsync(cancellationToken);
                await socialSeed.SeedAsync(cancellationToken);
                return Results.Ok(new
                {
                    campaign = ToCampaignResponse(result.Campaign),
                    leads = result.Leads.Select(l => ToLeadResponse(l, DateTimeOffset.UtcNow)).ToList()
                });
            }
            catch (DomainRuleException ex)
            {
                return MapDomainException(ex);
            }
        });

        app.MapGet("/leads", async (LeadService leads, RbacService rbac, HttpContext http, CancellationToken cancellationToken) =>
        {
            var (allowed, forbidden, _) = await CheckPermissionAsync(http, rbac, AppPermissions.LeadsRead, cancellationToken);
            if (!allowed) return forbidden;

            try
            {
                ReadActor(http);
                var items = await leads.ListAsync(cancellationToken);
                var now = DateTimeOffset.UtcNow;
                return Results.Ok(items.Select(l => ToLeadResponse(l, now)).ToList());
            }
            catch (DomainRuleException ex)
            {
                return MapDomainException(ex);
            }
        });

        app.MapPost("/leads/{id:guid}/claim", async (Guid id, LeadService leads, HttpContext http, CancellationToken cancellationToken) =>
        {
            return await ExecuteAsync(http, actor => leads.ClaimAsync(actor, id, cancellationToken));
        });

        app.MapPost("/leads/{id:guid}/reject", async (Guid id, RejectRequest request, LeadService leads, HttpContext http, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request?.Reason))
            {
                return Results.BadRequest(new { error = "Lý do từ chối lead là bắt buộc.", code = "InvalidReason" });
            }

            return await ExecuteAsync(http, actor => leads.RejectAsync(actor, id, request.Reason, cancellationToken));
        });

        app.MapPost("/leads/{id:guid}/convert", async (Guid id, ConvertLeadRequest request, LeadService leads, RbacService rbac, HttpContext http, CancellationToken cancellationToken) =>
        {
            var (allowed, forbidden, _) = await CheckPermissionAsync(http, rbac, AppPermissions.LeadsConvert, cancellationToken);
            if (!allowed) return forbidden;

            return await ExecuteAsync(http, actor => leads.ConvertAsync(actor, id, request?.RevenueVnd, cancellationToken));
        });

        app.MapPost("/dashboard/spend-proposal", async (CreateSpendProposalRequest request, SpendProposalService proposals, HttpContext http, CancellationToken cancellationToken) =>
        {
            return await ExecuteAsync(http, actor => proposals.ProposeAsync(
                actor,
                request.FromNote ?? string.Empty,
                request.ToNote ?? string.Empty,
                request.Percent,
                request.Rationale ?? string.Empty,
                cancellationToken), ToSpendProposalResponse);
        });

        app.MapGet("/dashboard/spend-proposals", async (SpendProposalService proposals, HttpContext http, CancellationToken cancellationToken) =>
        {
            try
            {
                ReadActor(http);
                var items = await proposals.ListAsync(cancellationToken);
                return Results.Ok(items.Select(ToSpendProposalResponse).ToList());
            }
            catch (DomainRuleException ex)
            {
                return MapDomainException(ex);
            }
        });

        app.MapPost("/dashboard/spend-proposal/{id:guid}/approve", async (Guid id, SpendProposalService proposals, HttpContext http, CancellationToken cancellationToken) =>
        {
            return await ExecuteAsync(http, actor => proposals.ApproveAsync(actor, id, cancellationToken), ToSpendProposalResponse);
        });

        app.MapPost("/dashboard/spend-proposal/{id:guid}/reject", async (Guid id, RejectRequest? request, SpendProposalService proposals, HttpContext http, CancellationToken cancellationToken) =>
        {
            return await ExecuteAsync(http, actor => proposals.RejectAsync(actor, id, request?.Reason, cancellationToken), ToSpendProposalResponse);
        });

        app.MapGet("/dashboard/cpl", async (
            decimal? spend,
            decimal? dailySpend,
            decimal? budget,
            LeadService leads,
            TrafficService traffic,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            try
            {
                ReadActor(http);
                var storedSpend = await traffic.GetTotalStoredSpendVndAsync(cancellationToken);
                var dashboard = await leads.GetCplAsync(spend, dailySpend, budget, storedSpend, cancellationToken);
                return Results.Ok(new
                {
                    spend = dashboard.Spend,
                    leadCount = dashboard.LeadCount,
                    cpl = dashboard.Cpl,
                    currency = dashboard.Currency,
                    adsLive = false,
                    dailySpend = dashboard.DailySpend,
                    budget = dashboard.Budget,
                    daysUntilEmpty = dashboard.DaysUntilEmpty,
                    projectedLeads = dashboard.ProjectedLeads,
                    status = "NOT_READY"
                });
            }
            catch (DomainRuleException ex)
            {
                return MapDomainException(ex);
            }
        });

        // MCP Server Endpoints (Pure Application layer facade, zero token leaks, zero PostgREST)
        app.MapGet("/mcp/tools", (McpService mcp, HttpContext http) =>
        {
            try
            {
                ReadActor(http);
                return Results.Ok(new
                {
                    server = "DXOS.Mcp",
                    version = "1.0.0",
                    tools = McpService.GetToolDefinitions().Select(t => new
                    {
                        name = t.Name,
                        description = t.Description,
                        inputSchema = t.InputSchema
                    }).ToList()
                });
            }
            catch (DomainRuleException ex)
            {
                return MapDomainException(ex);
            }
        });

        app.MapPost("/mcp/tools/{name}", async (
            string name,
            JsonElement? body,
            McpService mcp,
            HttpContext http,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var actor = ReadActor(http);
                var logger = loggerFactory.CreateLogger("DXOS.Mcp");
                logger.LogInformation("[MCP Audit] Tool: {ToolName}, Actor: {ActorId}, Role: {ActorRole}", name, actor.ActorId, actor.Role);

                var result = await mcp.ExecuteToolAsync(actor, name, body, cancellationToken);
                if (result.IsError)
                {
                    return Results.BadRequest(new { error = result.Error, code = "McpToolError" });
                }

                return Results.Ok(result.Content);
            }
            catch (DomainRuleException ex)
            {
                return MapDomainException(ex);
            }
        });

        app.MapPost("/mcp", async (
            JsonElement body,
            McpService mcp,
            HttpContext http,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var actor = ReadActor(http);
                var logger = loggerFactory.CreateLogger("DXOS.Mcp");
                if (body.TryGetProperty("method", out var methodProp) && methodProp.GetString() == "tools/call")
                {
                    string toolName = "unknown";
                    if (body.TryGetProperty("params", out var p) && p.TryGetProperty("name", out var n))
                    {
                        toolName = n.GetString() ?? "unknown";
                    }
                    logger.LogInformation("[MCP Audit] Tool: {ToolName}, Actor: {ActorId}, Role: {ActorRole}", toolName, actor.ActorId, actor.Role);
                }

                var response = await mcp.HandleJsonRpcAsync(actor, body, cancellationToken);
                return Results.Ok(response);
            }
            catch (DomainRuleException ex)
            {
                return MapDomainException(ex);
            }
        });
    }

    private static async Task<IResult> ExecuteAsync<T>(
        HttpContext http,
        Func<ActorContext, Task<T>> action,
        Func<T, object>? projector = null)
    {
        try
        {
            var actor = ReadActor(http);
            var result = await action(actor);
            if (result is Campaign campaign)
            {
                return Results.Ok(ToCampaignResponse(campaign));
            }

            if (result is Lead lead)
            {
                return Results.Ok(ToLeadResponse(lead, DateTimeOffset.UtcNow));
            }

            if (result is SpendProposal proposal)
            {
                return Results.Ok(ToSpendProposalResponse(proposal));
            }

            return Results.Ok(projector is null ? result : projector(result));
        }
        catch (DomainRuleException ex)
        {
            return MapDomainException(ex);
        }
    }

    private static ActorContext ReadActor(HttpContext http)
    {
        var roleRaw = http.Request.Headers["X-DXOS-Role"].ToString();
        var actorRaw = http.Request.Headers["X-DXOS-Actor"].ToString();
        if (!Enum.TryParse<ActorRole>(roleRaw, ignoreCase: true, out var role))
        {
            throw new DomainRuleException("InvalidActor", "Header X-DXOS-Role must be Owner, Marketer, Content, Sales, or System.");
        }

        if (string.IsNullOrWhiteSpace(actorRaw))
        {
            throw new DomainRuleException("InvalidActor", "Header X-DXOS-Actor is required.");
        }

        return new ActorContext(role, actorRaw.Trim());
    }

    private static IResult MapDomainException(DomainRuleException ex)
    {
        return ex.Code switch
        {
            "NotFound" => Results.NotFound(new { error = ex.Message, code = ex.Code }),
            "ForbiddenRole" => Results.Json(new { error = ex.Message, code = ex.Code }, statusCode: StatusCodes.Status403Forbidden),
            "AlreadyClaimed" or "AlreadyConverted" or "InvalidTransition" or "TerminalState" or "BrandBlocked" or "UndoWindowExpired" =>
                Results.Conflict(new { error = ex.Message, code = ex.Code }),
            _ => Results.BadRequest(new { error = ex.Message, code = ex.Code })
        };
    }

    private static object ToCampaignResponse(Campaign campaign)
    {
        IReadOnlyList<string> platforms;
        try
        {
            platforms = !string.IsNullOrWhiteSpace(campaign.PlatformsJson)
                ? JsonSerializer.Deserialize<List<string>>(campaign.PlatformsJson) ?? new List<string> { "facebook" }
                : new List<string> { "facebook" };
        }
        catch
        {
            platforms = new List<string> { "facebook" };
        }

        IReadOnlyList<string> imageUrls;
        try
        {
            imageUrls = !string.IsNullOrWhiteSpace(campaign.ImageUrlsJson)
                ? JsonSerializer.Deserialize<List<string>>(campaign.ImageUrlsJson) ?? new List<string>()
                : new List<string>();
        }
        catch
        {
            imageUrls = new List<string>();
        }

        object? product = !string.IsNullOrWhiteSpace(campaign.ProductName) || campaign.ProductPriceVnd.HasValue
            ? new
            {
                name = campaign.ProductName,
                priceVnd = campaign.ProductPriceVnd,
                sku = campaign.ProductSku,
                imageUrl = campaign.ProductImageUrl
            }
            : null;

        return new
        {
            id = campaign.Id,
            title = campaign.Topic,
            topic = campaign.Topic,
            kind = campaign.Kind,
            description = campaign.Description,
            copy = campaign.Copy,
            copySnapshot = campaign.CopySnapshot,
            status = campaign.Status.ToString(),
            platforms = platforms,
            platformsJson = campaign.PlatformsJson,
            eventStartUtc = campaign.EventStartUtc,
            eventEndUtc = campaign.EventEndUtc,
            location = campaign.Location,
            imageUrls = imageUrls,
            imageUrlsJson = campaign.ImageUrlsJson,
            landingUrl = campaign.LandingUrl,
            product = product,
            productName = campaign.ProductName,
            productPriceVnd = campaign.ProductPriceVnd,
            productSku = campaign.ProductSku,
            productImageUrl = campaign.ProductImageUrl,
            rejectionReason = campaign.RejectionReason,
            approvedAtUtc = campaign.ApprovedAtUtc,
            createdByActor = campaign.CreatedByActor,
            createdAtUtc = campaign.CreatedAtUtc,
            updatedAtUtc = campaign.UpdatedAtUtc,
            adsPushed = false
        };
    }

    private static object ToLeadResponse(Lead lead, DateTimeOffset nowUtc)
    {
        return new
        {
            id = lead.Id,
            name = lead.Name,
            phone = lead.Phone,
            email = lead.Email,
            source = lead.Source.ToString(),
            sources = lead.Sources.Select(s => s.ToString()).ToList(),
            score = lead.Score,
            label = lead.Label.ToString(),
            scoreBreakdown = new
            {
                behavior = lead.Breakdown.Behavior,
                channel = lead.Breakdown.Channel,
                campaign = lead.Breakdown.Campaign,
                time = lead.Breakdown.Time,
                intent = lead.Breakdown.Intent,
                total = lead.Breakdown.Total
            },
            reasons = lead.Reasons,
            scoreModel = lead.ScoreModel,
            scoreVersion = lead.ScoreVersion,
            scoredAtUtc = lead.ScoredAtUtc,
            campaignId = lead.CampaignId,
            assignedToActor = lead.AssignedToActor,
            assignedAtUtc = lead.AssignedAtUtc,
            claimedByActor = lead.ClaimedByActor,
            claimedAtUtc = lead.ClaimedAtUtc,
            rejectedByActors = lead.RejectedByActors,
            lastRejectionReason = lead.LastRejectionReason,
            convertedAtUtc = lead.ConvertedAtUtc,
            conversionRevenueVnd = lead.ConversionRevenueVnd,
            isConverted = lead.IsConverted,
            slaRemainingSeconds = lead.SlaRemainingSeconds(nowUtc),
            welcomeQueued = true,
            welcomeChannel = "hang-doi-noi-bo",
            createdAtUtc = lead.CreatedAtUtc,
            updatedAtUtc = lead.UpdatedAtUtc
        };
    }

    private static object ToSpendProposalResponse(SpendProposal proposal)
    {
        return new
        {
            id = proposal.Id,
            fromNote = proposal.FromNote,
            toNote = proposal.ToNote,
            percent = proposal.Percent,
            rationale = proposal.Rationale,
            proposedByRole = proposal.ProposedByRole.ToString(),
            proposedByActor = proposal.ProposedByActor,
            status = proposal.Status,
            rejectionReason = proposal.RejectionReason,
            decidedByActor = proposal.DecidedByActor,
            adsLive = false,
            createdAtUtc = proposal.CreatedAtUtc,
            decidedAtUtc = proposal.DecidedAtUtc
        };
    }

    private static object ToTrafficSnapshotResponse(TrafficSnapshot snapshot)
    {
        return new
        {
            id = snapshot.Id,
            campaignId = snapshot.CampaignId,
            periodDate = snapshot.PeriodDate.ToString("yyyy-MM-dd"),
            impressions = snapshot.Impressions,
            clicks = snapshot.Clicks,
            visits = snapshot.Visits,
            spendVnd = snapshot.SpendVnd,
            source = snapshot.Source.ToString(),
            recordedByActor = snapshot.RecordedByActor,
            createdAtUtc = snapshot.CreatedAtUtc
        };
    }

    private static object ToCampaignTrafficTotalsResponse(CampaignTrafficTotals totals)
    {
        return new
        {
            impressions = totals.Impressions,
            clicks = totals.Clicks,
            visits = totals.Visits,
            spendVnd = totals.SpendVnd,
            ctr = totals.Ctr
        };
    }

    private static object ToTrafficIngestResponse(TrafficIngestResult result)
    {
        return new
        {
            snapshot = ToTrafficSnapshotResponse(result.Snapshot),
            totals = ToCampaignTrafficTotalsResponse(result.Totals)
        };
    }

    private static async Task IngestSocialMessageAsync(
        BootstrapDbContext db,
        string pageId,
        string customerId,
        string customerName,
        string conversationId,
        string messageId,
        string senderId,
        string senderName,
        string senderType,
        string content,
        DateTimeOffset createdTime,
        CancellationToken cancellationToken,
        LeadService? leads = null,
        ILogger? logger = null,
        string messageType = "text",
        string attachmentsJson = "[]")
    {
        try
        {
            // Phone extraction
            var extractedPhone = PhoneExtractor.ExtractFirstPhoneNumber(content);

            // Blocker 4: Intake customer message with phone into CRM pipeline
            if (string.Equals(senderType, "customer", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(extractedPhone) && leads is not null)
            {
                try
                {
                    var provider = conversationId.StartsWith("zalo_") ? "zalo" : (conversationId.StartsWith("tiktok_") ? "tiktok" : "facebook");
                    await leads.IntakePlatformWebhookAsync(
                        provider,
                        $"phone_{conversationId}_{extractedPhone}",
                        customerName,
                        extractedPhone,
                        email: null,
                        campaignId: null,
                        rawPayload: content,
                        cancellationToken: cancellationToken);
                }
                catch (DomainRuleException dex)
                {
                    logger?.LogWarning(dex, "DomainRuleException while intaking phone lead {Phone} from conv {ConvId}", extractedPhone, conversationId);
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Failed to intake phone lead {Phone} from conv {ConvId}", extractedPhone, conversationId);
                }
            }

            // 1. Page
            var pageType = conversationId.StartsWith("zalo_") ? "zalo_oa" : (conversationId.StartsWith("tiktok_") ? "tiktok" : "facebook");
            var page = await db.SocialPages.FirstOrDefaultAsync(p => p.Id == pageId, cancellationToken);
            if (page is null)
            {
                page = new SocialPageRecord
                {
                    Id = pageId,
                    Name = "Royce Shop",
                    Type = pageType,
                    IsActive = true,
                    TotalConversations = 1,
                    TotalMessages = 1,
                    LastSyncAt = createdTime,
                    CreatedAt = createdTime,
                    UpdatedAt = createdTime
                };
                db.SocialPages.Add(page);
            }
            else
            {
                if (conversationId.StartsWith("zalo_") && page.Type == "facebook")
                {
                    page.Type = "zalo_oa";
                }
                else if (conversationId.StartsWith("tiktok_") && page.Type == "facebook")
                {
                    page.Type = "tiktok";
                }
                page.TotalMessages += 1;
                page.LastSyncAt = createdTime;
                page.UpdatedAt = DateTimeOffset.UtcNow;
            }
            await db.SaveChangesAsync(cancellationToken);

            // 2. Customer
            var customer = await db.SocialCustomers.FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);
            if (customer is null)
            {
                var phones = !string.IsNullOrWhiteSpace(extractedPhone) ? JsonSerializer.Serialize(new[] { extractedPhone }) : "[]";
                customer = new SocialCustomerRecord
                {
                    Id = customerId,
                    Name = customerName,
                    PageId = pageId,
                    PhoneNumbersJson = phones,
                    FirstSeenAt = createdTime,
                    LastSeenAt = createdTime,
                    CreatedAt = createdTime,
                    UpdatedAt = createdTime
                };
                db.SocialCustomers.Add(customer);
            }
            else
            {
                if (!string.Equals(senderType, "agent", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(customerName) && customerName != "Khách hàng")
                {
                    customer.Name = customerName;
                }
                if (!string.IsNullOrWhiteSpace(extractedPhone))
                {
                    try
                    {
                        var currentPhones = JsonSerializer.Deserialize<List<string>>(customer.PhoneNumbersJson ?? "[]") ?? [];
                        if (!currentPhones.Contains(extractedPhone))
                        {
                            currentPhones.Add(extractedPhone);
                            customer.PhoneNumbersJson = JsonSerializer.Serialize(currentPhones);
                        }
                    }
                    catch { }
                }
                customer.LastSeenAt = createdTime;
                customer.UpdatedAt = DateTimeOffset.UtcNow;
            }
            await db.SaveChangesAsync(cancellationToken);

            // 3. Conversation
            var snippet = string.Equals(senderType, "agent", StringComparison.OrdinalIgnoreCase) ? $"Bạn: {content}" : content;
            var isReplied = string.Equals(senderType, "agent", StringComparison.OrdinalIgnoreCase);

            var conv = await db.SocialConversations.FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);
            if (conv is null)
            {
                conv = new SocialConversationRecord
                {
                    Id = conversationId,
                    PageId = pageId,
                    CustomerId = customerId,
                    CustomerName = customerName,
                    CustomerPhone = extractedPhone,
                    HasPhone = !string.IsNullOrWhiteSpace(extractedPhone),
                    Snippet = snippet,
                    IsReplied = isReplied,
                    Status = "open",
                    MessageCount = 1,
                    InsertedAt = createdTime,
                    UpdatedAt = createdTime,
                    SyncedAt = DateTimeOffset.UtcNow
                };
                db.SocialConversations.Add(conv);
            }
            else
            {
                if (!string.Equals(senderType, "agent", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(customerName) && customerName != "Khách hàng")
                {
                    conv.CustomerName = customerName;
                }
                if (!string.IsNullOrWhiteSpace(extractedPhone))
                {
                    conv.CustomerPhone = extractedPhone;
                    conv.HasPhone = true;
                }
                conv.Snippet = snippet;
                conv.IsReplied = isReplied;
                conv.MessageCount += 1;
                conv.UpdatedAt = createdTime;
                conv.SyncedAt = DateTimeOffset.UtcNow;
            }
            await db.SaveChangesAsync(cancellationToken);

            // 4. Message
            var exists = await db.SocialMessages.AnyAsync(m => m.Id == messageId, cancellationToken);
            if (!exists)
            {
                var msg = new SocialMessageRecord
                {
                    Id = messageId,
                    ConversationId = conversationId,
                    PageId = pageId,
                    SenderId = senderId,
                    SenderName = senderName,
                    SenderType = senderType,
                    Content = content,
                    MessageType = messageType,
                    AttachmentsJson = attachmentsJson,
                    CreatedTime = createdTime,
                    CreatedAt = DateTimeOffset.UtcNow,
                    SyncedAt = DateTimeOffset.UtcNow
                };
                db.SocialMessages.Add(msg);
                await db.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            if (logger is not null)
            {
                logger.LogError(ex, "IngestSocialMessageAsync failed for conv {ConvId} msg {MsgId}", conversationId, messageId);
            }
            else
            {
                Console.Error.WriteLine($"IngestSocialMessageAsync error for conv {conversationId} msg {messageId}: {ex}");
            }
        }
    }

    private static async Task<(bool Allowed, IResult? ForbiddenResult, ActorAuthProfile Profile)> CheckPermissionAsync(
        HttpContext http,
        RbacService rbac,
        string permission,
        CancellationToken ct)
    {
        var actorId = http.Request.Headers["X-DXOS-Actor"].ToString();
        var profile = await rbac.ResolveActorProfileAsync(actorId, ct);

        if (!profile.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase))
        {
            return (false, Results.Json(new
            {
                error = $"Bạn không có quyền '{permission}'.",
                code = "ForbiddenPermission",
                required_permission = permission,
                current_actor = profile.ActorId,
                current_roles = profile.Roles
            }, statusCode: StatusCodes.Status403Forbidden), profile);
        }

        return (true, null, profile);
    }
}

internal sealed record SendSocialMessageRequest(string? Content, string? Message = null, string? ParentCommentId = null);

internal sealed record UpdateConversationStatusRequest(string? Status);

internal sealed record AssignConversationRequest(string? ActorId);

internal sealed record UpdateConversationNoteRequest(string? Note);

internal sealed record CreateRoleRequest(string? Name, string? Description, string[]? Permissions);

internal sealed record UpdateRolePermissionsRequest(string[]? Permissions);

internal sealed record AssignUserRoleRequest(string? ActorId, string? DisplayName, string[]? RoleNames);

internal sealed record PublishFacebookPostRequest(string? Message, string? Content = null, Guid? CampaignId = null, string? ScheduledAt = null, string? MediaUrl = null, string? MediaType = null);

internal sealed record CreateCampaignRequest(
    string? Title = null,
    string? Topic = null,
    string? Kind = null,
    string? Description = null,
    string[]? Platforms = null,
    DateTimeOffset? EventStart = null,
    DateTimeOffset? EventEnd = null,
    string? Location = null,
    string[]? ImageUrls = null,
    string? LandingUrl = null,
    CampaignProductDto? Product = null);

internal sealed record UpdateCampaignRequest(
    string? Title = null,
    string? Topic = null,
    string? Copy = null,
    string? Kind = null,
    string? Description = null,
    string[]? Platforms = null,
    DateTimeOffset? EventStart = null,
    DateTimeOffset? EventEnd = null,
    string? Location = null,
    string[]? ImageUrls = null,
    string? LandingUrl = null,
    CampaignProductDto? Product = null);

internal sealed record ApplyCampaignDraftRequest(string? Caption);

internal sealed record RejectRequest(string? Reason);

internal sealed record ConvertLeadRequest(decimal? RevenueVnd);

internal sealed record CreateSpendProposalRequest(
    string? FromNote,
    string? ToNote,
    decimal Percent,
    string? Rationale);

internal sealed record FormLeadRequest(string? Name, string? Phone, string? Email, Guid? CampaignId);

internal sealed record PlatformLeadWebhookRequest(
    string? ExternalEventId,
    string? Name,
    string? Phone,
    string? Email,
    Guid? CampaignId);

internal sealed record RecordTrafficRequest(
    DateOnly? PeriodDate,
    long Impressions,
    long Clicks,
    long Visits,
    decimal SpendVnd);

internal sealed record PageAdviceRequest(string? PageId);
internal sealed record PageAgentRunRequest(string? PageId);
internal sealed record FacebookPageSyncAllRequest(string? PageId = null, string? PageAccessToken = null);
