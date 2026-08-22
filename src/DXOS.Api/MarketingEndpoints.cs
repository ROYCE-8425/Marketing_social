using DXOS.Application;
using DXOS.Domain;
using Elsa.Workflows;

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
            "AlreadyClaimed" or "InvalidTransition" or "TerminalState" or "BrandBlocked" or "UndoWindowExpired" =>
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
            campaignId = lead.CampaignId,
            assignedToActor = lead.AssignedToActor,
            assignedAtUtc = lead.AssignedAtUtc,
            claimedByActor = lead.ClaimedByActor,
            claimedAtUtc = lead.ClaimedAtUtc,
            rejectedByActors = lead.RejectedByActors,
            lastRejectionReason = lead.LastRejectionReason,
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
}

internal sealed record CreateCampaignRequest(string? Topic);

internal sealed record RejectRequest(string? Reason);

internal sealed record CreateSpendProposalRequest(
    string? FromNote,
    string? ToNote,
    decimal Percent,
    string? Rationale);

internal sealed record FormLeadRequest(string? Name, string? Phone, string? Email, Guid? CampaignId);

internal sealed record RecordTrafficRequest(
    DateOnly? PeriodDate,
    long Impressions,
    long Clicks,
    long Visits,
    decimal SpendVnd);
