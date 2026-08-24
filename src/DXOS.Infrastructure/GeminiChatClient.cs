using System.Text;
using System.Text.Json;
using DXOS.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DXOS.Infrastructure;

/// <summary>
/// Google Gemini generateContent client (AI Studio API key). Never logs API keys.
/// Uses a free-tier Flash/Flash-Lite model by default. Falls back to <see cref="MockChatClient"/> on failure.
/// Does not publish, send messages, or call Graph.
/// </summary>
public sealed class GeminiChatClient : IChatClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<GeminiChatClient> _logger;
    private readonly MockChatClient _fallback;

    public GeminiChatClient(
        HttpClient httpClient,
        IConfiguration config,
        ILogger<GeminiChatClient> logger,
        MockChatClient fallback)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
        _fallback = fallback;
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        var apiKey = _config["GEMINI_API_KEY"] ?? _config["Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return await _fallback.CompleteAsync(systemPrompt, userPrompt, cancellationToken);
        }

        var isDraft = KimiChatClient.IsDraftPrompt(systemPrompt, userPrompt);
        var effectiveSystem = isDraft
            ? systemPrompt + "\nChỉ trả về nội dung tin nhắn tiếng Việt. Không JSON, không markdown, không giải thích."
            : systemPrompt + "\nTrả về JSON thuần (không markdown) đúng schema: {\"advisor\":\"DX-OS Marketing AI Expert\",\"disclaimer\":\"Đề xuất chỉ mang tính tham khảo. Hệ thống không tự động đăng bài hay can thiệp ngân sách.\",\"recommendations\":[{\"title\":\"\",\"category\":\"Inbox|Content|Technical\",\"actionText\":\"\",\"suggestedPostContent\":\"\"}]}. Đúng 3 mục. Không được bảo người dùng rằng bạn sẽ tự đăng bài, chi tiền, hoặc xóa dữ liệu.";

        var model = _config["GEMINI_MODEL"] ?? _config["Gemini:Model"] ?? "gemini-2.5-flash-lite";
        var payload = new
        {
            system_instruction = new
            {
                parts = new[] { new { text = effectiveSystem } }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = userPrompt } }
                }
            }
        };

        try
        {
            var path = $"v1beta/models/{Uri.EscapeDataString(model)}:generateContent";
            using var request = new HttpRequestMessage(HttpMethod.Post, path);
            request.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini generateContent failed with status {Status} for model {Model}", (int)response.StatusCode, model);
                return await _fallback.CompleteAsync(systemPrompt, userPrompt, cancellationToken);
            }

            using var doc = JsonDocument.Parse(body);
            var content = ExtractText(doc);
            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("Gemini generateContent returned empty text");
                return await _fallback.CompleteAsync(systemPrompt, userPrompt, cancellationToken);
            }

            return KimiChatClient.StripMarkdownFence(content.Trim());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini generateContent threw; using mock advisor");
            return await _fallback.CompleteAsync(systemPrompt, userPrompt, cancellationToken);
        }
    }

    public static string? ExtractText(JsonDocument doc)
    {
        if (!doc.RootElement.TryGetProperty("candidates", out var candidates)
            || candidates.ValueKind != JsonValueKind.Array
            || candidates.GetArrayLength() == 0)
        {
            return null;
        }

        var first = candidates[0];
        if (!first.TryGetProperty("content", out var content)
            || !content.TryGetProperty("parts", out var parts)
            || parts.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var sb = new StringBuilder();
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var textEl))
            {
                sb.Append(textEl.GetString());
            }
        }

        var result = sb.ToString();
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }
}
