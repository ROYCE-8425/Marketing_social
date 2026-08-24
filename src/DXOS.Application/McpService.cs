using System.Text.Json;
using DXOS.Domain;

namespace DXOS.Application;

public sealed record McpToolDefinition(
    string Name,
    string Description,
    object InputSchema);

public sealed record McpToolResult(
    bool IsError,
    object? Content,
    string? Error = null);

public sealed class McpService
{
    private readonly LeadService _leadService;
    private readonly TrafficService? _trafficService;
    private readonly IWebhookEventStore? _webhookStore;
    private readonly PageHealthService? _pageHealthService;
    private readonly IClock _clock;

    public McpService(
        LeadService leadService,
        IClock clock,
        TrafficService? trafficService = null,
        IWebhookEventStore? webhookStore = null,
        PageHealthService? pageHealthService = null)
    {
        _leadService = leadService;
        _clock = clock;
        _trafficService = trafficService;
        _webhookStore = webhookStore;
        _pageHealthService = pageHealthService;
    }

    public static IReadOnlyList<McpToolDefinition> GetToolDefinitions() =>
    [
        new(
            "lead_search",
            "Tìm kiếm danh sách Lead theo từ khóa (tên/SĐT/email), phân loại (Hot/Warm/Cold/Junk), và giới hạn kết quả.",
            new
            {
                type = "object",
                properties = new
                {
                    query = new { type = "string", description = "Từ khóa tìm kiếm theo tên khách hàng, số điện thoại hoặc email." },
                    label = new { type = "string", description = "Phân loại Lead cần lọc: Hot, Warm, Cold, hoặc Junk.", @enum = new[] { "Hot", "Warm", "Cold", "Junk" } },
                    limit = new { type = "integer", description = "Số lượng bản ghi tối đa trả về (mặc định: 50).", @default = 50 }
                }
            }),
        new(
            "lead_get",
            "Lấy thông tin chi tiết một Lead theo định danh ID (GUID).",
            new
            {
                type = "object",
                required = new[] { "id" },
                properties = new
                {
                    id = new { type = "string", description = "Định danh duy nhất của Lead (GUID)." }
                }
            }),
        new(
            "lead_assign",
            "Tiếp nhận hoặc phân công Lead cho nhân sự Sales xử lý (yêu cầu vai trò Sales hoặc System).",
            new
            {
                type = "object",
                required = new[] { "leadId" },
                properties = new
                {
                    leadId = new { type = "string", description = "Định danh Lead cần nhận xử lý (GUID)." }
                }
            }),
        new(
            "analytics_summary",
            "Xem báo cáo tổng hợp chi phí chuyển đổi (CPL), số lượng Lead theo nguồn kênh (Facebook/TikTok/Form/Zalo) và ngân sách.",
            new
            {
                type = "object",
                properties = new
                {
                    spendOverride = new { type = "number", description = "Ngân sách chi tiêu thủ công nếu muốn ghi đè." },
                    dailySpend = new { type = "number", description = "Chi tiêu hàng ngày ước tính." },
                    budget = new { type = "number", description = "Tổng ngân sách chiến dịch." }
                }
            }),
        new(
            "platform_connections",
            "Liệt kê danh sách các kênh mạng xã hội/quảng cáo đã tích hợp (Facebook, TikTok, Zalo).",
            new
            {
                type = "object",
                properties = new { }
            }),
        new(
            "sync_status",
            "Kiểm tra trạng thái đồng bộ dữ liệu ingest từ các kênh và thời điểm nhận webhook gần nhất.",
            new
            {
                type = "object",
                properties = new { }
            }),
        new(
            "page_health",
            "Đánh giá sức khỏe và hiệu quả vận hành Fanpage Facebook (Nội dung, Hộp thư, Lead, Tương tác) bằng thuật toán chuẩn hóa.",
            new
            {
                type = "object",
                properties = new
                {
                    pageId = new { type = "string", description = "Mã Fanpage cần đánh giá (mặc định: trang chính của hệ thống)." }
                }
            })
    ];

    public async Task<McpToolResult> ExecuteToolAsync(
        ActorContext actor,
        string toolName,
        JsonElement? arguments,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(actor.ActorId))
        {
            throw new DomainRuleException("InvalidActor", "X-DXOS-Actor is required for MCP tool execution.");
        }

        try
        {
            return toolName.ToLowerInvariant() switch
            {
                "lead_search" => await HandleLeadSearchAsync(arguments, cancellationToken),
                "lead_get" => await HandleLeadGetAsync(arguments, cancellationToken),
                "lead_assign" => await HandleLeadAssignAsync(actor, arguments, cancellationToken),
                "analytics_summary" => await HandleAnalyticsSummaryAsync(arguments, cancellationToken),
                "platform_connections" => HandlePlatformConnections(),
                "sync_status" => await HandleSyncStatusAsync(cancellationToken),
                "page_health" => await HandlePageHealthAsync(arguments, cancellationToken),
                _ => new McpToolResult(true, null, $"Unknown tool '{toolName}'.")
            };
        }
        catch (DomainRuleException ex)
        {
            return new McpToolResult(true, null, $"[{ex.Code}] {ex.Message}");
        }
        catch (Exception)
        {
            return new McpToolResult(true, null, "An internal error occurred while executing the tool.");
        }
    }

    private async Task<McpToolResult> HandleLeadSearchAsync(
        JsonElement? arguments,
        CancellationToken cancellationToken)
    {
        string? query = null;
        LeadLabel? label = null;
        var limit = 50;

        if (arguments.HasValue && arguments.Value.ValueKind == JsonValueKind.Object)
        {
            var root = arguments.Value;
            if (root.TryGetProperty("query", out var qProp) && qProp.ValueKind == JsonValueKind.String)
            {
                query = qProp.GetString();
            }
            if (root.TryGetProperty("label", out var lProp) && lProp.ValueKind == JsonValueKind.String)
            {
                if (Enum.TryParse<LeadLabel>(lProp.GetString(), true, out var parsedLabel))
                {
                    label = parsedLabel;
                }
            }
            if (root.TryGetProperty("limit", out var limProp) && limProp.TryGetInt32(out var limVal))
            {
                limit = limVal;
            }
        }

        var results = await _leadService.SearchAsync(query, label, limit, cancellationToken);
        var mapped = results.Select(FormatLeadSummary).ToList();
        return new McpToolResult(false, mapped);
    }

    private async Task<McpToolResult> HandleLeadGetAsync(
        JsonElement? arguments,
        CancellationToken cancellationToken)
    {
        if (!arguments.HasValue || arguments.Value.ValueKind != JsonValueKind.Object)
        {
            return new McpToolResult(true, null, "Argument 'id' is required.");
        }

        if (!arguments.Value.TryGetProperty("id", out var idProp) || idProp.ValueKind != JsonValueKind.String || !Guid.TryParse(idProp.GetString(), out var leadId))
        {
            return new McpToolResult(true, null, "A valid GUID 'id' is required.");
        }

        var lead = await _leadService.GetAsync(leadId, cancellationToken);
        if (lead is null)
        {
            return new McpToolResult(true, null, $"Lead '{leadId}' was not found.");
        }

        return new McpToolResult(false, FormatLeadDetails(lead));
    }

    private async Task<McpToolResult> HandleLeadAssignAsync(
        ActorContext actor,
        JsonElement? arguments,
        CancellationToken cancellationToken)
    {
        if (!arguments.HasValue || arguments.Value.ValueKind != JsonValueKind.Object)
        {
            return new McpToolResult(true, null, "Argument 'leadId' is required.");
        }

        if (!arguments.Value.TryGetProperty("leadId", out var idProp) || idProp.ValueKind != JsonValueKind.String || !Guid.TryParse(idProp.GetString(), out var leadId))
        {
            return new McpToolResult(true, null, "A valid GUID 'leadId' is required.");
        }

        var claimed = await _leadService.ClaimAsync(actor, leadId, cancellationToken);
        return new McpToolResult(false, new
        {
            message = $"Lead '{leadId}' đã được nhận xử lý bởi '{claimed.ClaimedByActor}'.",
            lead = FormatLeadDetails(claimed)
        });
    }

    private async Task<McpToolResult> HandleAnalyticsSummaryAsync(
        JsonElement? arguments,
        CancellationToken cancellationToken)
    {
        decimal? spendOverride = null;
        decimal? dailySpend = null;
        decimal? budget = null;

        if (arguments.HasValue && arguments.Value.ValueKind == JsonValueKind.Object)
        {
            var root = arguments.Value;
            if (root.TryGetProperty("spendOverride", out var sProp) && sProp.TryGetDecimal(out var sVal))
            {
                spendOverride = sVal;
            }
            if (root.TryGetProperty("dailySpend", out var dProp) && dProp.TryGetDecimal(out var dVal))
            {
                dailySpend = dVal;
            }
            if (root.TryGetProperty("budget", out var bProp) && bProp.TryGetDecimal(out var bVal))
            {
                budget = bVal;
            }
        }

        decimal storedSpend = 0m;
        if (_trafficService is not null)
        {
            storedSpend = await _trafficService.GetTotalStoredSpendVndAsync(cancellationToken);
        }

        var cpl = await _leadService.GetCplAsync(spendOverride, dailySpend, budget, storedSpend, cancellationToken);
        var platforms = await _leadService.SummarizeByPlatformAsync(cancellationToken);

        return new McpToolResult(false, new
        {
            cpl,
            platforms,
            dataFreshness = "demo",
            adsLive = false,
            source = "unified-data-layer"
        });
    }

    private static McpToolResult HandlePlatformConnections()
    {
        var connections = PlatformCatalog.MockConnections.Select(c => new
        {
            provider = c.Provider,
            displayName = c.DisplayName,
            capabilities = c.Capabilities,
            mode = c.Mode,
            adsLive = false
        }).ToList();

        return new McpToolResult(false, connections);
    }

    private async Task<McpToolResult> HandleSyncStatusAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset? lastReceivedAt = null;
        if (_webhookStore is not null)
        {
            lastReceivedAt = await _webhookStore.GetLastReceivedAtUtcAsync(cancellationToken);
        }

        return new McpToolResult(false, new
        {
            server = "DXOS.Mcp",
            version = "1.0.0",
            mode = "demo",
            adsLive = false,
            dataFreshness = "demo",
            mockPlatforms = new[] { "facebook", "tiktok", "zalo" },
            lastEventReceivedAtUtc = lastReceivedAt
        });
    }

    private async Task<McpToolResult> HandlePageHealthAsync(
        JsonElement? arguments,
        CancellationToken cancellationToken)
    {
        if (_pageHealthService is null)
        {
            return new McpToolResult(true, null, "PageHealthService is not available.");
        }

        string? pageId = null;
        if (arguments.HasValue && arguments.Value.ValueKind == JsonValueKind.Object)
        {
            if (arguments.Value.TryGetProperty("pageId", out var pProp) && pProp.ValueKind == JsonValueKind.String)
            {
                pageId = pProp.GetString();
            }
        }

        var evaluation = await _pageHealthService.EvaluatePageHealthAsync(pageId ?? "988656934325292", cancellationToken);
        return new McpToolResult(false, evaluation);
    }

    public async Task<object> HandleJsonRpcAsync(
        ActorContext actor,
        JsonElement requestBody,
        CancellationToken cancellationToken)
    {
        string? id = null;
        if (requestBody.TryGetProperty("id", out var idProp))
        {
            id = idProp.ToString();
        }

        if (!requestBody.TryGetProperty("method", out var methodProp) || methodProp.ValueKind != JsonValueKind.String)
        {
            return CreateRpcError(id, -32600, "Invalid Request: 'method' is required.");
        }

        var method = methodProp.GetString();
        switch (method)
        {
            case "initialize":
                return new
                {
                    jsonrpc = "2.0",
                    id,
                    result = new
                    {
                        protocolVersion = "2024-11-05",
                        capabilities = new
                        {
                            tools = new { }
                        },
                        serverInfo = new
                        {
                            name = "DXOS.Mcp",
                            version = "1.0.0"
                        }
                    }
                };

            case "tools/list":
                return new
                {
                    jsonrpc = "2.0",
                    id,
                    result = new
                    {
                        tools = GetToolDefinitions().Select(t => new
                        {
                            name = t.Name,
                            description = t.Description,
                            inputSchema = t.InputSchema
                        }).ToList()
                    }
                };

            case "tools/call":
                if (!requestBody.TryGetProperty("params", out var paramsProp) || paramsProp.ValueKind != JsonValueKind.Object)
                {
                    return CreateRpcError(id, -32602, "Invalid params: object expected.");
                }

                if (!paramsProp.TryGetProperty("name", out var nameProp) || nameProp.ValueKind != JsonValueKind.String)
                {
                    return CreateRpcError(id, -32602, "Invalid params: 'name' is required.");
                }

                var toolName = nameProp.GetString()!;
                JsonElement? arguments = null;
                if (paramsProp.TryGetProperty("arguments", out var argsProp) && argsProp.ValueKind == JsonValueKind.Object)
                {
                    arguments = argsProp;
                }

                var toolResult = await ExecuteToolAsync(actor, toolName, arguments, cancellationToken);
                return new
                {
                    jsonrpc = "2.0",
                    id,
                    result = new
                    {
                        content = new[]
                        {
                            new
                            {
                                type = "text",
                                text = JsonSerializer.Serialize(toolResult.Content ?? toolResult.Error)
                            }
                        },
                        isError = toolResult.IsError
                    }
                };

            case "ping":
                return new
                {
                    jsonrpc = "2.0",
                    id,
                    result = new { }
                };

            default:
                return CreateRpcError(id, -32601, $"Method '{method}' not found.");
        }
    }

    private static object CreateRpcError(string? id, int code, string message) =>
        new
        {
            jsonrpc = "2.0",
            id,
            error = new
            {
                code,
                message
            }
        };

    private static object FormatLeadSummary(Lead lead) =>
        new
        {
            id = lead.Id,
            name = lead.Name,
            phone = lead.Phone,
            email = lead.Email,
            source = lead.Source.ToString(),
            sources = lead.Sources.Select(s => s.ToString()).ToList(),
            score = lead.Score,
            label = lead.Label.ToString(),
            isConverted = lead.IsConverted,
            conversionRevenueVnd = lead.ConversionRevenueVnd,
            assignedToActor = lead.AssignedToActor,
            claimedByActor = lead.ClaimedByActor,
            createdAtUtc = lead.CreatedAtUtc
        };

    private static object FormatLeadDetails(Lead lead) =>
        new
        {
            id = lead.Id,
            name = lead.Name,
            phone = lead.Phone,
            email = lead.Email,
            source = lead.Source.ToString(),
            sources = lead.Sources.Select(s => s.ToString()).ToList(),
            score = lead.Score,
            label = lead.Label.ToString(),
            scoreBreakdown = lead.Breakdown,
            reasons = lead.Reasons,
            isConverted = lead.IsConverted,
            convertedAtUtc = lead.ConvertedAtUtc,
            conversionRevenueVnd = lead.ConversionRevenueVnd,
            assignedToActor = lead.AssignedToActor,
            assignedAtUtc = lead.AssignedAtUtc,
            claimedByActor = lead.ClaimedByActor,
            claimedAtUtc = lead.ClaimedAtUtc,
            lastRejectionReason = lead.LastRejectionReason,
            createdAtUtc = lead.CreatedAtUtc,
            updatedAtUtc = lead.UpdatedAtUtc
        };
}
