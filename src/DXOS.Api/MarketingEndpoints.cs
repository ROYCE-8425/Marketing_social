using System.Text.Json;
using DXOS.Application;
using DXOS.Domain;
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
            return await ExecuteAsync(http, actor => campaigns.CreateDraftAsync(actor, request.Topic ?? string.Empty, cancellationToken));
        });

        app.MapPost("/campaigns/{id:guid}/submit-review", async (Guid id, CampaignService campaigns, HttpContext http, CancellationToken cancellationToken) =>
        {
            return await ExecuteAsync(http, actor => campaigns.SubmitReviewAsync(actor, id, cancellationToken));
        });

        app.MapPost("/campaigns/{id:guid}/send-to-owner", async (Guid id, CampaignService campaigns, HttpContext http, CancellationToken cancellationToken) =>
        {
            return await ExecuteAsync(http, actor => campaigns.SendToOwnerAsync(actor, id, cancellationToken));
        });

        app.MapPost("/campaigns/{id:guid}/approve", async (Guid id, CampaignService campaigns, HttpContext http, CancellationToken cancellationToken) =>
        {
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
            ReadActor(http);
            var fbToken = config["FACEBOOK_PAGE_ACCESS_TOKEN"] ?? config["Facebook:PageAccessToken"];
            var fbMode = config["FACEBOOK_MODE"] ?? config["Facebook:Mode"];
            var isFbLive = !string.IsNullOrWhiteSpace(fbToken) || string.Equals(fbMode, "live", StringComparison.OrdinalIgnoreCase);

            return Results.Ok(PlatformCatalog.MockConnections.Select(c => new
            {
                provider = c.Provider,
                displayName = c.DisplayName,
                capabilities = (c.Provider == "facebook" && isFbLive)
                    ? ["READ_LEADS", "WEBHOOK", "GRAPH_API_LEAD_ADS"]
                    : c.Capabilities,
                mode = (c.Provider == "facebook" && isFbLive) ? "development-live" : c.Mode,
                adsLive = false,
                token = (string?)null
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
                                        messageType,
                                        attachmentsJson);

                                    if (!isEcho)
                                    {
                                        await leads.IntakePlatformWebhookAsync(
                                            "facebook",
                                            mid ?? Guid.NewGuid().ToString("N"),
                                            senderName,
                                            null,
                                            null,
                                            null,
                                            rawBody,
                                            cancellationToken);
                                    }

                                    logger.LogInformation("Processed Messenger message ({Type}) from {Sender}: {Text}", senderType, senderName, text);
                                }
                            }
                        }

                        // 2. Lead Ads Changes Events
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
                                        cancellationToken);

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

        // ── Social CRM REST Endpoints for Admin UI ──
        app.MapGet("/pages", async (BootstrapDbContext db, CancellationToken ct) =>
        {
            var list = await db.SocialPages.AsNoTracking().ToListAsync(ct);
            if (list.Count == 0)
            {
                return Results.Ok(new[]
                {
                    new { id = "988656934325292", name = "Royce Shop", type = "facebook", is_active = true, total_conversations = 0, total_messages = 0 }
                });
            }
            return Results.Ok(list);
        });

        app.MapGet("/customers", async (BootstrapDbContext db, CancellationToken ct) =>
        {
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

        app.MapGet("/conversations", async (BootstrapDbContext db, CancellationToken ct) =>
        {
            var list = await db.SocialConversations.AsNoTracking().OrderByDescending(c => c.UpdatedAt).ToListAsync(ct);
            return Results.Ok(list.Select(c =>
            {
                var platform = c.Id.StartsWith("fb_") ? (c.Id.Contains("lead") ? "lead" : "facebook") : (c.Id.StartsWith("zalo_") ? "zalo" : "facebook");
                string stage = "active";
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
                    snippet = c.Snippet,
                    message_count = c.MessageCount,
                    has_phone = c.HasPhone,
                    is_replied = c.IsReplied,
                    stage = stage,
                    status = stage.ToUpperInvariant(),
                    platform = platform,
                    tags = c.TagsJson,
                    updated_at = c.UpdatedAt
                };
            }));
        });

        app.MapPost("/conversations/{id}/messages", async (
            string id,
            SendSocialMessageRequest request,
            BootstrapDbContext db,
            IHttpClientFactory httpFactory,
            IConfiguration config,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return Results.BadRequest(new { error = "Content is required" });
            }

            var conv = await db.SocialConversations.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
            if (conv is null)
            {
                return Results.NotFound(new { error = "Conversation not found" });
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

            var msgId = $"agent_msg_{Guid.NewGuid():N}";
            var now = DateTimeOffset.UtcNow;

            await IngestSocialMessageAsync(
                db,
                pageId,
                conv.CustomerId ?? $"fb_user_{customerPsid ?? "unknown"}",
                conv.CustomerName ?? "Khách hàng",
                conv.Id,
                msgId,
                pageId,
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
        });

        app.MapPost("/conversations/{id}/status", async (
            string id,
            UpdateConversationStatusRequest request,
            BootstrapDbContext db,
            CancellationToken cancellationToken) =>
        {
            var conv = await db.SocialConversations.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
            if (conv is null)
            {
                return Results.NotFound(new { error = "Conversation not found" });
            }

            var status = request.Status?.Trim().ToLowerInvariant() ?? "active";
            conv.TagsJson = JsonSerializer.Serialize(new[] { $"status:{status}" });
            conv.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(new
            {
                success = true,
                id = conv.Id,
                status = status.ToUpperInvariant()
            });
        });

        app.MapGet("/messages", async (BootstrapDbContext db, CancellationToken ct) =>
        {
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

        app.MapPost("/demo/seed", async (DemoSeedService seed, HttpContext http, CancellationToken cancellationToken) =>
        {
            try
            {
                ReadActor(http);
                var result = await seed.SeedAsync(cancellationToken);
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

        app.MapGet("/leads", async (LeadService leads, HttpContext http, CancellationToken cancellationToken) =>
        {
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

        app.MapPost("/leads/{id:guid}/convert", async (Guid id, ConvertLeadRequest request, LeadService leads, HttpContext http, CancellationToken cancellationToken) =>
        {
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
        return new
        {
            id = campaign.Id,
            topic = campaign.Topic,
            copy = campaign.Copy,
            copySnapshot = campaign.CopySnapshot,
            status = campaign.Status.ToString(),
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
        string messageType = "text",
        string attachmentsJson = "[]")
    {
        try
        {
            // 1. Page
            var page = await db.SocialPages.FirstOrDefaultAsync(p => p.Id == pageId, cancellationToken);
            if (page is null)
            {
                page = new SocialPageRecord
                {
                    Id = pageId,
                    Name = "Royce Shop",
                    Type = "facebook",
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
                page.TotalMessages += 1;
                page.LastSyncAt = createdTime;
                page.UpdatedAt = DateTimeOffset.UtcNow;
            }
            await db.SaveChangesAsync(cancellationToken);

            // 2. Customer
            var customer = await db.SocialCustomers.FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);
            if (customer is null)
            {
                customer = new SocialCustomerRecord
                {
                    Id = customerId,
                    Name = customerName,
                    PageId = pageId,
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
                    Snippet = snippet,
                    IsReplied = isReplied,
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
        catch
        {
            // Fail open for background ingest logging
        }
    }
}

internal sealed record SendSocialMessageRequest(string? Content);

internal sealed record UpdateConversationStatusRequest(string? Status);

internal sealed record CreateCampaignRequest(string? Topic);

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
