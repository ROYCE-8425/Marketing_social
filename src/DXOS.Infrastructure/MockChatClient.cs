using System.Text.Json;
using DXOS.Application;
using DXOS.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace DXOS.Infrastructure;

public sealed record PageAdviceItem(
    string Title,
    string Category,
    string ActionText,
    string SuggestedPostContent);

public sealed class MockChatClient : IChatClient
{
    private readonly ILogger<MockChatClient> _logger;

    public MockChatClient(ILogger<MockChatClient> logger)
    {
        _logger = logger;
    }

    public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MockChatClient generating advisory or draft response based on prompt context");

        // If this is a Campaign AI draft request
        if (systemPrompt.Contains("Campaign AI Content Strategist", StringComparison.OrdinalIgnoreCase) ||
            userPrompt.Contains("chiến dịch", StringComparison.OrdinalIgnoreCase) ||
            userPrompt.Contains("ai-draft", StringComparison.OrdinalIgnoreCase))
        {
            var campaignDraftsJson = """
            {
              "drafts": [
                {
                  "caption": "🔥 [SIÊU ƯU ĐÃI KHAI TRƯƠNG]\nNhận ngay quà tặng đặc biệt và ưu đãi giảm giá lên đến 30% khi liên hệ tư vấn hôm nay!\n👉 Inbox ngay để shop gửi bảng giá chi tiết và tư vấn 1-1 nhé!",
                  "suggestedMediaUrl": "/logos/royce_avatar.jpg",
                  "scheduleHintLocal": "20:00 hôm nay"
                },
                {
                  "caption": "⚡ [ĐỪNG BỎ LỠ CƠ HỘI]\nTrải nghiệm giải pháp Marketing và CRM thế hệ mới. Tăng tốc tương tác, bứt phá doanh thu bán hàng!\n👉 Đăng ký nhận ưu đãi tại Fanpage ngay hôm nay!",
                  "suggestedMediaUrl": "/logos/royce_avatar.jpg",
                  "scheduleHintLocal": "11:30 ngày mai"
                },
                {
                  "caption": "🌟 [BỘ SƯU TẬP & SỰ KIỆN ĐẶC BIỆT]\nChào đón mùa mua sắm mới với ngập tràn ưu đãi độc quyền dành riêng cho khách hàng tương tác trên Fanpage.\n👉 Để lại bình luận hoặc nhắn tin để nhận mã giảm giá!",
                  "suggestedMediaUrl": "/logos/royce_avatar.jpg",
                  "scheduleHintLocal": "09:00 cuối tuần"
                }
              ],
              "disclaimer": "AI không tự đăng bài, không tự gửi tin, không chi tiền."
            }
            """;
            return Task.FromResult(campaignDraftsJson);
        }

        // If this is an Operator Agent run request
        if (systemPrompt.Contains("Facebook Page Operator Agent", StringComparison.OrdinalIgnoreCase) ||
            systemPrompt.Contains("Page Operator Agent", StringComparison.OrdinalIgnoreCase) ||
            userPrompt.Contains("JSON contract", StringComparison.OrdinalIgnoreCase))
        {
            if (!userPrompt.Contains("[Kết quả công cụ", StringComparison.OrdinalIgnoreCase) &&
                !userPrompt.Contains("Đã hết số lượt", StringComparison.OrdinalIgnoreCase))
            {
                // First round: request tool call
                return Task.FromResult("""{"tool":"page_health","args":{}}""");
            }

            // Second round (or with tool results): return final agent JSON
            var agentJson = """
            {
              "summary": "Fanpage Royce Shop có tin nhắn mới cần phản hồi và cần đăng bài tăng tương tác.",
              "focus": "inbox",
              "actions": [
                {
                  "id": "a1",
                  "type": "reply_inbox",
                  "title": "Phản hồi tin nhắn chờ tư vấn",
                  "rationale": "Khách hàng cần tư vấn size và báo giá",
                  "payload": {
                    "conversationId": "c1",
                    "suggestedReply": "Dạ Royce Shop chào bạn! Để shop tư vấn chuẩn size và gửi ưu đãi tốt nhất, bạn cho shop xin SĐT/Zalo để chuyên viên liên hệ hỗ trợ ngay nhé ạ!"
                  },
                  "requiresPermission": "inbox.reply",
                  "autoExecute": false
                },
                {
                  "id": "a2",
                  "type": "compose_post",
                  "title": "Soạn bài viết ưu đãi mới",
                  "rationale": "Kêu gọi hành động và thu thập số điện thoại quan tâm",
                  "payload": {
                    "suggestedPost": "🔥 [BỘ SƯU TẬP MỚI ROYCE SHOP]\nInbox ngay để nhận tư vấn size và ưu đãi độc quyền hôm nay!"
                  },
                  "requiresPermission": "page.publish",
                  "autoExecute": false
                }
              ],
              "disclaimer": "AI không tự đăng bài, không tự gửi tin, không chi tiền."
            }
            """;
            return Task.FromResult(agentJson);
        }

        // If this is a draft request for a conversation that already has a phone number
        if (systemPrompt.Contains("Khách đã để lại số điện thoại", StringComparison.OrdinalIgnoreCase) ||
            userPrompt.Contains("hẹn gọi/nhắn qua số", StringComparison.OrdinalIgnoreCase))
        {
            var phone = PhoneExtractor.ExtractFirstPhoneNumber(userPrompt) ?? "SĐT của bạn";
            return Task.FromResult($"Dạ Royce Shop đã nhận thông tin và sẽ liên hệ tư vấn trực tiếp qua số {phone} cho bạn ngay nhé ạ!");
        }

        // If this is a draft request for a single conversation asking for SĐT
        if (userPrompt.Contains("soạn 1 tin trả lời ngắn", StringComparison.OrdinalIgnoreCase) ||
            userPrompt.Contains("xin SĐT", StringComparison.OrdinalIgnoreCase) ||
            userPrompt.Contains("soạn tin", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult("Dạ Royce Shop chào bạn! Để shop tư vấn chuẩn size và gửi ưu đãi tốt nhất, bạn cho shop xin SĐT/Zalo để chuyên viên liên hệ hỗ trợ ngay nhé ạ!");
        }

        bool hasInboxNeed = userPrompt.Contains("chưa trả lời", StringComparison.OrdinalIgnoreCase) ||
                            userPrompt.Contains("Hộp thư", StringComparison.OrdinalIgnoreCase) ||
                            userPrompt.Contains("inbox", StringComparison.OrdinalIgnoreCase) ||
                            userPrompt.Contains("SDT", StringComparison.OrdinalIgnoreCase) ||
                            userPrompt.Contains("SĐT", StringComparison.OrdinalIgnoreCase);

        bool hasContentNeed = userPrompt.Contains("Nội dung", StringComparison.OrdinalIgnoreCase) ||
                              userPrompt.Contains("CTA", StringComparison.OrdinalIgnoreCase) ||
                              userPrompt.Contains("bài đăng", StringComparison.OrdinalIgnoreCase) ||
                              userPrompt.Contains("Tần suất", StringComparison.OrdinalIgnoreCase);

        bool hasTechNeed = userPrompt.Contains("Tương tác", StringComparison.OrdinalIgnoreCase) ||
                           userPrompt.Contains("Graph", StringComparison.OrdinalIgnoreCase) ||
                           userPrompt.Contains("quyền", StringComparison.OrdinalIgnoreCase) ||
                           userPrompt.Contains("flop", StringComparison.OrdinalIgnoreCase);

        var rec1 = hasInboxNeed
            ? new PageAdviceItem(
                Title: "Tăng tốc độ phản hồi tin nhắn & Thu thập SĐT",
                Category: "Inbox",
                ActionText: "Ưu tiên phản hồi các tin nhắn chưa đọc trong vòng 15 phút đầu tiên và chủ động xin số điện thoại để chuyển sang đội ngũ Sales.",
                SuggestedPostContent: "Dạ Royce Shop chào bạn! Bạn đang quan tâm mẫu nào ạ? Bạn cho shop xin SĐT/Zalo để shop tư vấn trực tiếp và gửi báo giá ưu đãi ngay nhé ạ!")
            : new PageAdviceItem(
                Title: "Tối ưu hóa quy trình tiếp nhận hộp thư",
                Category: "Inbox",
                ActionText: "Duy trì thời gian phản hồi tin nhắn dưới 30 phút và phân loại hội thoại tự động.",
                SuggestedPostContent: "Dạ em chào anh/chị! Anh/chị cho em xin số điện thoại để chuyên viên tư vấn bên em gọi điện hỗ trợ trực tiếp và gửi bảng giá chi tiết cho mình ngay ạ!");

        var rec2 = hasContentNeed
            ? new PageAdviceItem(
                Title: "Bổ sung Lời kêu gọi hành động (CTA) & SĐT tư vấn",
                Category: "Content",
                ActionText: "Thêm số hotline hoặc lời mời nhắn tin inbox trực tiếp vào cuối bài đăng để tăng tỉ lệ thu thập lead.",
                SuggestedPostContent: "🔥 [ƯU ĐÃI ĐẶC BIỆT DÀNH CHO BẠN]\nLiên hệ ngay Hotline/Zalo hoặc nhắn tin trực tiếp cho Royce Shop để nhận báo giá chi tiết và tư vấn miễn phí ngay hôm nay!")
            : new PageAdviceItem(
                Title: "Duy trì lịch đăng bài định kỳ theo tuần",
                Category: "Content",
                ActionText: "Lên lịch đăng bài đều đặn 3-5 bài/tuần vào các khung giờ vàng (11h30 trưa và 20h tối).",
                SuggestedPostContent: "🔥 [BỘ SƯU TẬP MỚI]\nKhám phá các thiết kế mới nhất vừa cập bến Royce Shop tuần này!");

        var rec3 = hasTechNeed
            ? new PageAdviceItem(
                Title: "Cấu hình quyền Graph API & Duy trì tần suất đăng bài",
                Category: "Technical",
                ActionText: "Đăng ký quyền pages_read_user_content qua Meta App Review để đo lường toàn diện bình luận và không xem thiếu dữ liệu là flop.",
                SuggestedPostContent: "Chia sẻ câu chuyện khách hàng thực tế hoặc bài học kinh nghiệm mới nhất trong tuần để gia tăng tương tác tự nhiên trên Fanpage.")
            : new PageAdviceItem(
                Title: "Kiểm tra kết nối và tính toàn vẹn dữ liệu Graph API",
                Category: "Technical",
                ActionText: "Định kỳ làm mới token và theo dõi các chỉ số tương tác bài viết để duy trì đo lường chính xác.",
                SuggestedPostContent: "Royce Shop luôn sẵn sàng phục vụ quý khách hàng với trải nghiệm mua sắm tốt nhất!");

        var recommendations = new List<PageAdviceItem> { rec1, rec2, rec3 };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(new
        {
            advisor = "DX-OS Marketing AI Expert",
            disclaimer = "Đề xuất chỉ mang tính tham khảo. Hệ thống không tự động đăng bài hay can thiệp ngân sách.",
            recommendations
        }, options);

        return Task.FromResult(json);
    }
}
