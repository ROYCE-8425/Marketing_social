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
    private readonly IClock _clock;

    public McpService(
        LeadService leadService,
        IClock clock,
        TrafficService? trafficService = null,
        IWebhookEventStore? webhookStore = null)
    {
        _leadService = leadService;
        _clock = clock;
        _trafficService = trafficService;
        _webhookStore = webhookStore;
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
                    leadId = new { type = "string", description = "Định danh Lead cần nhận xử lý (GUID)." },
                    actor = new { type = "string", description = "Mã định danh Sales nhận Lead (tùy chọn; mặc định lấy theo ActorContext)." }
                }
            }),
        new(
            "analytics_summary",
            "Lấy báo cáo tổng hợp hiệu quả Marketing & Sales: số lượng Lead theo kênh, tỷ lệ Hot/Warm/Cold, số chốt đơn, doanh thu và chỉ số CPL.",
            new
            {
                type = "object",
                properties = new
                {
                    spendOverride = new { type = "number", description = "Chi phí tùy chỉnh mô phỏng (VND)." },
                    dailySpend = new { type = "number", description = "Tốc độ chi tiêu mỗi ngày (VND)." },
                    budget = new { type = "number", description = "Tổng ngân sách khả dụng (VND)." }
                }
            }),
        new(
            "platform_connections",
            "Danh sách các kênh quảng cáo / mạng xã hội đang kết nối (Facebook, TikTok, Zalo). Tuyệt đối không chứa secret hay token.",
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
                _ => new McpToolResult(true, null, $"Unknown tool '{toolName}'.")
            };
        }
        catch (DomainRuleException ex)
        {
            return new McpToolResult(true, null, $"[{ex.Code}] {ex.Message}");
        }
        catch (Exception ex)
        {
            return new McpToolResult(true, null, ex.Message);
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

        var effectiveActor = actor;
        if (arguments.Value.TryGetProperty("actor", out var actProp) && actProp.ValueKind == JsonValueKind.String)
        {
            var customActorId = actProp.GetString()?.Trim();
            if (!string.IsNullOrWhiteSpace(customActorId))
            {
                effectiveActor = new ActorContext(actor.Role, customActorId);
            }
        }

        var claimed = await _leadService.ClaimAsync(effectiveActor, leadId, cancellationToken);
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
