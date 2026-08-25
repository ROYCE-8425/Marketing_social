using System.Text.Json;
using DXOS.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DXOS.Infrastructure.Persistence;

public sealed class SocialSeedService
{
    private readonly BootstrapDbContext _db;
    private readonly ILogger<SocialSeedService> _logger;

    public SocialSeedService(BootstrapDbContext db, ILogger<SocialSeedService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<int> EnsureSeedDataAsync(CancellationToken ct = default)
    {
        var hasCustomers = await _db.SocialCustomers.AnyAsync(ct);
        if (!hasCustomers)
        {
            _logger.LogInformation("SocialCustomers table is empty. Executing auto-seed for SEO Trùm Social CRM baseline...");
            return await SeedAsync(ct);
        }
        return 0;
    }

    public async Task<int> SeedAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        DateTimeOffset Ms(double days) => now.AddDays(-days);
        DateTimeOffset MsHour(double days, double hours) => now.AddDays(-days).AddHours(-hours);

        // 1. Pages / Channels
        var pages = new List<SocialPageRecord>
        {
            new()
            {
                Id = "988656934325292",
                Name = "SEO Trùm Fanpage (Royce Shop)",
                Type = "facebook",
                IsActive = true,
                TotalConversations = 6,
                TotalMessages = 142,
                LastSyncAt = now.AddMinutes(-5),
                CreatedAt = Ms(90),
                UpdatedAt = now
            },
            new()
            {
                Id = "demo_zl_001",
                Name = "SEO Trùm Zalo Care",
                Type = "zalo_oa",
                IsActive = true,
                TotalConversations = 4,
                TotalMessages = 87,
                LastSyncAt = now.AddMinutes(-10),
                CreatedAt = Ms(90),
                UpdatedAt = now
            },
            new()
            {
                Id = "demo_tt_001",
                Name = "SEO Trùm TikTok Shop",
                Type = "tiktok",
                IsActive = true,
                TotalConversations = 2,
                TotalMessages = 35,
                LastSyncAt = now.AddMinutes(-15),
                CreatedAt = Ms(60),
                UpdatedAt = now
            }
        };

        foreach (var page in pages)
        {
            var existing = await _db.SocialPages.FindAsync(new object[] { page.Id }, ct);
            if (existing is null)
            {
                _db.SocialPages.Add(page);
            }
            else
            {
                existing.Name = page.Name;
                existing.Type = page.Type;
                existing.TotalConversations = page.TotalConversations;
                existing.TotalMessages = page.TotalMessages;
            }
        }

        // 2. Customers
        var customerData = new (string Id, string Name, string PageId, string Phone, int Orders, decimal Amount, DateTimeOffset LastSeen, DateTimeOffset FirstSeen, string[] Tags)[]
        {
            ("fb_user_Shop_Alpha", "Shop Alpha (HCM)", "988656934325292", "0909123456", 12, 35000000m, MsHour(0, 2), Ms(90), new[] { "vip", "hcm", "si", "status:active" }),
            ("fb_user_Shop_Beta", "Shop Beta (Đà Nẵng)", "988656934325292", "0988765432", 8, 18500000m, MsHour(1, 5), Ms(60), new[] { "danang", "retail", "status:active" }),
            ("zalo_user_Mr_Quang", "Anh Quang (Kinh Doanh)", "demo_zl_001", "0912345678", 5, 12000000m, MsHour(2, 0), Ms(45), new[] { "wholesale", "zalo", "status:active" }),
            ("fb_user_Shop_Delta", "Shop Delta (Huế)", "988656934325292", "0933112233", 6, 14000000m, Ms(4.5), Ms(120), new[] { "hue", "regular", "status:sleeping" }),
            ("zalo_user_Ms_Lan", "Chị Lan (B2C)", "demo_zl_001", "0977889900", 2, 4500000m, Ms(6.0), Ms(30), new[] { "b2c", "zalo", "status:sleeping" }),
            ("fb_user_Shop_Gamma", "Shop Gamma (Hà Nội)", "988656934325292", "0908776655", 15, 48000000m, Ms(12), Ms(180), new[] { "hanoi", "priority", "si", "status:at_risk" }),
            ("zalo_user_Mr_Hung", "Anh Hùng (Đại Lý)", "demo_zl_001", "0944556677", 9, 26000000m, Ms(22), Ms(150), new[] { "daily", "zalo", "status:at_risk" }),
            ("fb_user_Shop_Epsilon", "Shop Epsilon (Hải Phòng)", "988656934325292", "0966332211", 4, 9200000m, Ms(45), Ms(200), new[] { "haiphong", "status:dormant" }),
            ("fb_user_Shop_Zeta", "Shop Zeta (Vũng Tàu)", "988656934325292", "0922446688", 3, 7000000m, Ms(70), Ms(220), new[] { "vungtau", "status:dormant" }),
            ("zalo_user_Old_Client", "Khách Cũ HCM (Sỉ)", "demo_zl_001", "0911223344", 7, 19000000m, Ms(120), Ms(300), new[] { "churned", "can_cham_soc", "status:churned" })
        };

        foreach (var c in customerData)
        {
            var existing = await _db.SocialCustomers.FindAsync(new object[] { c.Id }, ct);
            if (existing is null)
            {
                _db.SocialCustomers.Add(new SocialCustomerRecord
                {
                    Id = c.Id,
                    Name = c.Name,
                    PageId = c.PageId,
                    PhoneNumbersJson = JsonSerializer.Serialize(new[] { c.Phone }),
                    EmailsJson = JsonSerializer.Serialize(new[] { $"{c.Id}@example.com" }),
                    TagsJson = JsonSerializer.Serialize(c.Tags),
                    OrderCount = c.Orders,
                    PurchasedAmount = c.Amount,
                    LastSeenAt = c.LastSeen,
                    FirstSeenAt = c.FirstSeen,
                    CreatedAt = c.FirstSeen,
                    UpdatedAt = c.LastSeen
                });
            }
            else
            {
                existing.Name = c.Name;
                existing.PageId = c.PageId;
                existing.PhoneNumbersJson = JsonSerializer.Serialize(new[] { c.Phone });
                existing.TagsJson = JsonSerializer.Serialize(c.Tags);
                existing.OrderCount = c.Orders;
                existing.PurchasedAmount = c.Amount;
                existing.LastSeenAt = c.LastSeen;
            }
        }

        // 3. Conversations
        var convData = new (string ConvId, string CustId, string CustName, string PageId, string Snippet, string Status, string Assignee, string Note, DateTimeOffset LastSeen)[]
        {
            ("fb_988656934325292_Shop_Alpha", "fb_user_Shop_Alpha", "Shop Alpha (HCM)", "988656934325292", "Đã ghi nhận đơn sỉ 50 áo thun. Bên em gửi xế chuyến 17h nhé.", "open", "royce", "Khách VIP nhập đều đặn hàng tuần", MsHour(0, 2)),
            ("fb_988656934325292_Shop_Beta", "fb_user_Shop_Beta", "Shop Beta (Đà Nẵng)", "988656934325292", "Dạ còn đủ size M và L màu xanh navy anh nhé!", "open", "sales_alice", "Hỏi báo giá sỉ đợt 2", MsHour(1, 5)),
            ("zalo_demo_zl_001_Mr_Quang", "zalo_user_Mr_Quang", "Anh Quang (Kinh Doanh)", "demo_zl_001", "Bên em đã gửi bảng chiết khấu đại lý qua Zalo cho anh rồi ạ.", "open", "sales_alice", "Quan tâm chính sách bảo hành và đổi trả", MsHour(2, 0)),
            ("fb_988656934325292_Shop_Delta", "fb_user_Shop_Delta", "Shop Delta (Huế)", "988656934325292", "Hàng đợt trước bán rất chạy, đợt này lấy thêm 30 cái.", "pending", "sales_alice", "Cần nhắc gọi lại trước cuối tuần", Ms(4.5)),
            ("zalo_demo_zl_001_Ms_Lan", "zalo_user_Ms_Lan", "Chị Lan (B2C)", "demo_zl_001", "Chị nhận được hàng rồi, chất vải đẹp lắm cảm ơn shop!", "done", "marketer_bob", "Khách hài lòng, có thể upsell phụ kiện", Ms(6.0)),
            ("fb_988656934325292_Shop_Gamma", "fb_user_Shop_Gamma", "Shop Gamma (Hà Nội)", "988656934325292", "Shop kiểm tra giúp anh đơn gửi thứ 3 tuần trước đã tới kho chưa?", "open", "royce", "Khách lớn đang chờ đối soát công nợ", Ms(12)),
            ("zalo_demo_zl_001_Mr_Hung", "zalo_user_Mr_Hung", "Anh Hùng (Đại Lý)", "demo_zl_001", "Tháng này thị trường chậm, qua tháng sau mình chốt đơn mới nhé.", "pending", "sales_alice", "Hẹn liên hệ lại đầu tháng", Ms(22)),
            ("fb_988656934325292_Shop_Epsilon", "fb_user_Shop_Epsilon", "Shop Epsilon (Hải Phòng)", "988656934325292", "Cảm ơn em đã tư vấn nhiệt tình.", "done", "marketer_bob", "Lâu chưa đặt lại, cần gửi voucher kích cầu", Ms(45)),
            ("fb_988656934325292_Shop_Zeta", "fb_user_Shop_Zeta", "Shop Zeta (Vũng Tàu)", "988656934325292", "Đợt này bên anh đang sửa cửa hàng nên tạm ngưng nhập.", "pending", "sales_alice", "Theo dõi khi nào khai trương lại", Ms(70)),
            ("zalo_demo_zl_001_Old_Client", "zalo_user_Old_Client", "Khách Cũ HCM (Sỉ)", "demo_zl_001", "Cảm ơn em, hiện tại bên anh chưa có nhu cầu.", "done", "royce", "Khách ngưng lâu ngày, cần chương trình tri ân", Ms(120))
        };

        foreach (var c in convData)
        {
            var existing = await _db.SocialConversations.FindAsync(new object[] { c.ConvId }, ct);
            if (existing is null)
            {
                _db.SocialConversations.Add(new SocialConversationRecord
                {
                    Id = c.ConvId,
                    PageId = c.PageId,
                    CustomerId = c.CustId,
                    CustomerName = c.CustName,
                    CustomerPhone = "0909123456",
                    Snippet = c.Snippet,
                    MessageCount = 24,
                    HasPhone = true,
                    IsReplied = true,
                    Status = c.Status,
                    AssignedToActor = c.Assignee,
                    InternalNote = c.Note,
                    TagsJson = JsonSerializer.Serialize(new[] { $"status:{c.Status}", "seed" }),
                    InsertedAt = c.LastSeen.AddDays(-30),
                    UpdatedAt = c.LastSeen,
                    SyncedAt = now
                });
            }
            else
            {
                existing.CustomerName = c.CustName;
                existing.Snippet = c.Snippet;
                existing.Status = c.Status;
                existing.AssignedToActor = c.Assignee;
                existing.InternalNote = c.Note;
                existing.UpdatedAt = c.LastSeen;
            }
        }

        // 4. Generate ~240 Messages
        var sampleCustomerQuestions = new[]
        {
            "Còn size M không em?",
            "Cho mình hỏi giá sỉ số lượng 50 cái",
            "Bên em có chuyến giao hàng hỏa tốc trong ngày không?",
            "Lấy giúp anh 5 cái màu đen và 3 cái màu xanh nhé, gửi về địa chỉ cũ SĐT 0909123456",
            "Mua combo 10 cái có được chiết khấu thêm không em?",
            "Đợt tới mình sẽ nhập thêm 100 cái mẫu mới",
            "Đã nhận được hàng rồi, chất lượng rất tốt cảm ơn shop!",
            "Bao giờ có hàng mẫu đợt mới về vậy em?",
            "Có mẫu catalogue mới gửi qua Zalo giúp anh nhé",
            "Mã vận đơn giao hàng của mình là gì em kiểm tra giúp?",
            "Gửi giúp em hóa đơn VAT của đơn hôm qua nhé",
            "Bên shop có hỗ trợ đổi size nếu khách mặc không vừa không ạ?"
        };

        var sampleAgentAnswers = new[]
        {
            "Dạ chào anh/chị! Sản phẩm bên em hiện đang sẵn hàng tại kho ạ.",
            "Dạ với số lượng từ 50 cái bên em áp dụng mức chiết khấu sỉ 25% kèm freeship toàn quốc ạ.",
            "Dạ bên em có hỗ trợ giao hỏa tốc 2h trong nội thành HCM và Hà Nội anh nhé!",
            "Dạ em đã lên đơn thành công cho anh rồi ạ, lát nữa shipper sẽ gọi cho anh nhé.",
            "Dạ bên em đang có chương trình tặng kèm phụ kiện cao cấp khi mua combo ạ.",
            "Dạ mẫu mới dự kiến tuần sau sẽ cập bến, em sẽ gửi ảnh qua trước cho anh tham khảo ạ.",
            "Dạ cảm ơn anh/chị đã tin tưởng và ủng hộ SEO Trùm ạ! Chúc anh/chị buôn may bán đắt!",
            "Dạ mã vận đơn của anh là VNPOST988656, hàng đang trên đường trung chuyển ạ.",
            "Dạ bên em hỗ trợ đổi size miễn phí trong vòng 7 ngày nếu còn nguyên tem mác ạ."
        };

        int msgCount = 0;
        foreach (var conv in convData)
        {
            for (int i = 24; i >= 1; i--)
            {
                var msgTime = conv.LastSeen.AddHours(-i * 3.5);
                var isCustomer = (i % 2 == 1);
                var msgId = $"seed_msg_{conv.ConvId}_{i}";

                var existingMsg = await _db.SocialMessages.FindAsync(new object[] { msgId }, ct);
                if (existingMsg is null)
                {
                    _db.SocialMessages.Add(new SocialMessageRecord
                    {
                        Id = msgId,
                        ConversationId = conv.ConvId,
                        PageId = conv.PageId,
                        SenderId = isCustomer ? conv.CustId : conv.PageId,
                        SenderName = isCustomer ? conv.CustName : "SEO Trùm Support",
                        SenderType = isCustomer ? "customer" : "agent",
                        Content = isCustomer
                            ? sampleCustomerQuestions[(i + msgCount) % sampleCustomerQuestions.Length]
                            : sampleAgentAnswers[(i + msgCount) % sampleAgentAnswers.Length],
                        MessageType = "text",
                        CreatedTime = msgTime,
                        CreatedAt = msgTime,
                        SyncedAt = now
                    });
                    msgCount++;
                }
            }
        }

        // 5. Facebook Posts & Metrics
        var posts = new[]
        {
            new SocialPostRecord
            {
                Id = "seed_post_001",
                PostId = "988656934325292_101",
                PageId = "988656934325292",
                Message = "🎉 BÙNG NỔ ƯU ĐÃI THÁNG 8 CÙNG SEO TRÙM & ROYCE SHOP! Giảm ngay 20% cho đối tác nhập sỉ số lượng lớn từ hôm nay. Liên hệ ngay hotline 0909123456!",
                PermalinkUrl = "https://facebook.com/988656934325292/posts/101",
                MediaType = "photo",
                MediaUrl = "/logos/royce_web_banner.jpg",
                FullPicture = "/logos/royce_web_banner.jpg",
                Status = "published",
                ReactionCount = 148,
                CommentCount = 32,
                ShareCount = 19,
                CreatedTimeUtc = Ms(2),
                CreatedAtUtc = Ms(2)
            },
            new SocialPostRecord
            {
                Id = "seed_post_002",
                PostId = "988656934325292_102",
                PageId = "988656934325292",
                Message = "🚀 Hướng dẫn tối ưu hóa quy trình bán hàng đa kênh tự động với hệ thống SEO Trùm Social CRM. Giúp tăng 300% hiệu suất xử lý tin nhắn và chốt đơn!",
                PermalinkUrl = "https://facebook.com/988656934325292/posts/102",
                MediaType = "photo",
                MediaUrl = "/logos/royce_avatar.jpg",
                FullPicture = "/logos/royce_avatar.jpg",
                Status = "published",
                ReactionCount = 95,
                CommentCount = 18,
                ShareCount = 12,
                CreatedTimeUtc = Ms(5),
                CreatedAtUtc = Ms(5)
            },
            new SocialPostRecord
            {
                Id = "seed_post_003",
                PostId = "988656934325292_103",
                PageId = "988656934325292",
                Message = "🔥 TOP những chiến lược marketing và chăm sóc đối tác B2B hiệu quả nhất dành cho doanh nghiệp SME năm 2026.",
                PermalinkUrl = "https://facebook.com/988656934325292/posts/103",
                MediaType = "status",
                Status = "published",
                ReactionCount = 210,
                CommentCount = 45,
                ShareCount = 28,
                CreatedTimeUtc = Ms(10),
                CreatedAtUtc = Ms(10)
            }
        };

        foreach (var p in posts)
        {
            var existing = await _db.SocialPosts.FindAsync(new object[] { p.Id }, ct);
            if (existing is null)
            {
                _db.SocialPosts.Add(p);
            }
            else
            {
                existing.Message = p.Message;
                existing.ReactionCount = p.ReactionCount;
                existing.CommentCount = p.CommentCount;
                existing.ShareCount = p.ShareCount;
            }
        }

        // 6. Facebook Comments
        var comments = new[]
        {
            new SocialCommentRecord { Id = "cmt_1", CommentId = "c_1", PostId = "988656934325292_101", FromId = "fb_u1", FromName = "Trần Hoàng Anh", Message = "Shop ơi tư vấn bảng giá sỉ giúp mình với", CreatedTimeUtc = Ms(1.8) },
            new SocialCommentRecord { Id = "cmt_2", CommentId = "c_2", PostId = "988656934325292_101", FromId = "988656934325292", FromName = "SEO Trùm Fanpage (Royce Shop)", Message = "Dạ chào anh Hoàng Anh, bên em đã inbox chi tiết bảng giá cho anh rồi ạ!", CreatedTimeUtc = Ms(1.7) },
            new SocialCommentRecord { Id = "cmt_3", CommentId = "c_3", PostId = "988656934325292_102", FromId = "fb_u2", FromName = "Lê Minh Tuấn", Message = "Hệ thống CRM dùng mượt và chuyên nghiệp thật sự!", CreatedTimeUtc = Ms(4.5) }
        };

        foreach (var cmt in comments)
        {
            var existing = await _db.SocialComments.FindAsync(new object[] { cmt.Id }, ct);
            if (existing is null)
            {
                _db.SocialComments.Add(cmt);
            }
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Successfully seeded Social CRM baseline: {Pages} pages, {Cust} customers, {Conv} conversations, {Msgs} messages, {Posts} posts",
            pages.Count, customerData.Length, convData.Length, msgCount, posts.Length);

        return customerData.Length;
    }
}
