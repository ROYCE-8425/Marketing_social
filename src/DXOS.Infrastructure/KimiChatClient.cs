using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DXOS.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DXOS.Infrastructure;

/// <summary>
/// Moonshot / Kimi OpenAI-compatible chat client. Never logs API keys.
/// On transport or provider failure, falls back to <see cref="MockChatClient"/> so advise/drafts still work.
/// Does not call Graph, publish, or send messages.
/// </summary>
public sealed class KimiChatClient : IChatClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<KimiChatClient> _logger;
    private readonly MockChatClient _fallback;

    public KimiChatClient(
        HttpClient httpClient,
        IConfiguration config,
        ILogger<KimiChatClient> logger,
        MockChatClient fallback)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
        _fallback = fallback;
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        var apiKey = _config["KIMI_API_KEY"] ?? _config["Kimi:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return await _fallback.CompleteAsync(systemPrompt, userPrompt, cancellationToken);
        }

        var isDraft = IsDraftPrompt(systemPrompt, userPrompt);
        var effectiveSystem = isDraft
            ? systemPrompt + "\nChỉ trả về nội dung tin nhắn tiếng Việt. Không JSON, không markdown, không giải thích."
            : systemPrompt + "\nTrả về JSON thuần (không markdown) đúng schema: {\"advisor\":\"DX-OS Marketing AI Expert\",\"disclaimer\":\"Đề xuất chỉ mang tính tham khảo. Hệ thống không tự động đăng bài hay can thiệp ngân sách.\",\"recommendations\":[{\"title\":\"\",\"category\":\"Inbox|Content|Technical\",\"actionText\":\"\",\"suggestedPostContent\":\"\"}]}. Đúng 3 mục. Không được bảo người dùng rằng bạn sẽ tự đăng bài, chi tiền, hoặc xóa dữ liệu.";

        var model = _config["KIMI_MODEL"] ?? _config["Kimi:Model"] ?? "kimi-k2.5";
        var payload = new
        {
            model,
            temperature = 0.4,
            messages = new[]
            {
                new { role = "system", content = effectiveSystem },
                new { role = "user", content = userPrompt }
            }
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Kimi chat completions failed with status {Status}", (int)response.StatusCode);
                return await _fallback.CompleteAsync(systemPrompt, userPrompt, cancellationToken);
            }

            using var doc = JsonDocument.Parse(body);
            var content = ExtractAssistantContent(doc);
            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("Kimi chat completions returned empty assistant content");
                return await _fallback.CompleteAsync(systemPrompt, userPrompt, cancellationToken);
            }

            return StripMarkdownFence(content.Trim());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Kimi chat completions threw; using mock advisor");
            return await _fallback.CompleteAsync(systemPrompt, userPrompt, cancellationToken);
        }
    }

    public static bool IsDraftPrompt(string systemPrompt, string userPrompt)
    {
        return systemPrompt.Contains("Khách đã để lại số điện thoại", StringComparison.OrdinalIgnoreCase)
            || userPrompt.Contains("hẹn gọi/nhắn qua số", StringComparison.OrdinalIgnoreCase)
            || userPrompt.Contains("soạn 1 tin trả lời ngắn", StringComparison.OrdinalIgnoreCase)
            || userPrompt.Contains("soạn 1 tin xác nhận", StringComparison.OrdinalIgnoreCase);
    }

    internal static string? ExtractAssistantContent(JsonDocument doc)
    {
        if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
        {
            return null;
        }

        var first = choices[0];
        if (first.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var contentEl))
        {
            return contentEl.GetString();
        }

        return null;
    }

    public static string StripMarkdownFence(string content)
    {
        var trimmed = content.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var firstNewline = trimmed.IndexOf('\n');
        if (firstNewline < 0)
        {
            return trimmed;
        }

        var rest = trimmed[(firstNewline + 1)..];
        var fence = rest.LastIndexOf("```", StringComparison.Ordinal);
        if (fence >= 0)
        {
            rest = rest[..fence];
        }

        return rest.Trim();
    }
}
