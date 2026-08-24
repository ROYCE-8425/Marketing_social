using System.Net;
using DXOS.Infrastructure.Integrations;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DXOS.Unit.Tests;

public sealed class FacebookPageClientTests
{
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    [Fact]
    public async Task GetPageAsync_ReturnsPageMetadata()
    {
        var json = """
        {
            "id": "988656934325292",
            "name": "Royce Shop",
            "category": "E-commerce",
            "fan_count": 120,
            "followers_count": 150
        }
        """;

        var handler = new MockHttpMessageHandler(req =>
        {
            Assert.Contains("/988656934325292", req.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var client = new FacebookPageClient(new HttpClient(handler), NullLogger<FacebookPageClient>.Instance);
        var page = await client.GetPageAsync("988656934325292", "test_token", TestContext.Current.CancellationToken);

        Assert.NotNull(page);
        Assert.Equal("988656934325292", page.Id);
        Assert.Equal("Royce Shop", page.Name);
        Assert.Equal(120, page.FanCount);
        Assert.Equal(150, page.FollowersCount);
    }

    [Fact]
    public async Task GetPagePostsAsync_ParsesValidPostsWithSummaries()
    {
        var json = """
        {
            "data": [
                {
                    "id": "988656934325292_101",
                    "message": "Flash Sale Royce Shop áo thun 99k!",
                    "created_time": "2026-08-24T00:00:00+0000",
                    "permalink_url": "https://facebook.com/royceshop/posts/101",
                    "reactions": { "summary": { "total_count": 15 } },
                    "comments": { "summary": { "total_count": 3 } },
                    "shares": { "count": 2 }
                }
            ]
        }
        """;

        var handler = new MockHttpMessageHandler(req =>
        {
            var uri = req.RequestUri?.ToString() ?? string.Empty;
            if (uri.Contains("/posts"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":[]}""", System.Text.Encoding.UTF8, "application/json")
            };
        });

        var client = new FacebookPageClient(new HttpClient(handler), NullLogger<FacebookPageClient>.Instance);
        var posts = await client.GetPagePostsAsync("988656934325292", "test_token", TestContext.Current.CancellationToken);

        Assert.Single(posts);
        Assert.Equal("988656934325292_101", posts[0].Id);
        Assert.Equal("Flash Sale Royce Shop áo thun 99k!", posts[0].Message);
        Assert.Equal(15, posts[0].ReactionCount);
        Assert.Equal(3, posts[0].CommentCount);
        Assert.Equal(2, posts[0].ShareCount);
    }

    [Fact]
    public async Task GetPostCommentsAsync_ParsesCommentsWithSender()
    {
        var json = """
        {
            "data": [
                {
                    "id": "comm_123",
                    "from": {
                        "id": "user_456",
                        "name": "Nguyễn Văn A"
                    },
                    "message": "Shop còn size L màu đen không ạ?",
                    "created_time": "2026-08-24T01:00:00+0000"
                }
            ]
        }
        """;

        var handler = new MockHttpMessageHandler(req =>
        {
            Assert.Contains("/post_101/comments", req.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var client = new FacebookPageClient(new HttpClient(handler), NullLogger<FacebookPageClient>.Instance);
        var res = await client.GetPostCommentsAsync("post_101", "test_token", TestContext.Current.CancellationToken);

        Assert.False(res.HasPermissionError);
        Assert.True(res.HttpSuccess);
        Assert.Single(res.Comments);
        Assert.Equal("comm_123", res.Comments[0].Id);
        Assert.Equal("Nguyễn Văn A", res.Comments[0].From?.Name);
        Assert.Equal("Shop còn size L màu đen không ạ?", res.Comments[0].Message);
    }

    [Fact]
    public async Task GetPostCommentsAsync_WhenEmptyCommentsArray200_SetsHttpSuccessTrue()
    {
        var json = """{ "data": [] }""";
        var handler = new MockHttpMessageHandler(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var client = new FacebookPageClient(new HttpClient(handler), NullLogger<FacebookPageClient>.Instance);
        var res = await client.GetPostCommentsAsync("post_101", "test_token", TestContext.Current.CancellationToken);

        Assert.False(res.HasPermissionError);
        Assert.True(res.HttpSuccess);
        Assert.Empty(res.Comments);
    }

    [Fact]
    public async Task GetPostCommentsAsync_WhenCode10PermissionDenied_SetsPermissionDeniedFlag()
    {
        var errorJson = """
        {
            "error": {
                "message": "(#10) To read comments, pages_read_user_content is required",
                "type": "OAuthException",
                "code": 10,
                "fbtrace_id": "Abc123Trace"
            }
        }
        """;

        var handler = new MockHttpMessageHandler(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(errorJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var client = new FacebookPageClient(new HttpClient(handler), NullLogger<FacebookPageClient>.Instance);
        var res = await client.GetPostCommentsAsync("post_101", "test_token", TestContext.Current.CancellationToken);

        Assert.True(res.HasPermissionError);
        Assert.False(res.HttpSuccess);
        Assert.Empty(res.Comments);
        Assert.Equal("10", res.ErrorCode);
    }

    [Fact]
    public async Task GetPostCommentsAsync_WhenCode200PermissionDenied_SetsPermissionDeniedFlag()
    {
        var errorJson = """
        {
            "error": {
                "message": "(#200) Provide valid app permissions",
                "type": "OAuthException",
                "code": 200,
                "fbtrace_id": "Abc200Trace"
            }
        }
        """;

        var handler = new MockHttpMessageHandler(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent(errorJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var client = new FacebookPageClient(new HttpClient(handler), NullLogger<FacebookPageClient>.Instance);
        var res = await client.GetPostCommentsAsync("post_101", "test_token", TestContext.Current.CancellationToken);

        Assert.True(res.HasPermissionError);
        Assert.False(res.HttpSuccess);
        Assert.Empty(res.Comments);
        Assert.Equal("200", res.ErrorCode);
    }

    [Fact]
    public async Task GetPostCommentsAsync_WhenGenericExpiredTokenWithoutCode10_DoesNotSetPermissionError()
    {
        var errorJson = """
        {
            "error": {
                "message": "Error validating access token: Session has expired on Monday, 24-Aug-26.",
                "type": "OAuthException",
                "code": 190,
                "error_subcode": 463,
                "fbtrace_id": "Abc190Trace"
            }
        }
        """;

        var handler = new MockHttpMessageHandler(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(errorJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var client = new FacebookPageClient(new HttpClient(handler), NullLogger<FacebookPageClient>.Instance);
        var res = await client.GetPostCommentsAsync("post_101", "test_token", TestContext.Current.CancellationToken);

        Assert.False(res.HasPermissionError);
        Assert.False(res.HttpSuccess);
        Assert.Empty(res.Comments);
        Assert.Equal("190", res.ErrorCode);
    }

    [Fact]
    public async Task ReplyCommentAsync_ReturnsCreatedCommentId()
    {
        var json = """{ "id": "reply_999" }""";

        var handler = new MockHttpMessageHandler(req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Contains("/comm_123/comments", req.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var client = new FacebookPageClient(new HttpClient(handler), NullLogger<FacebookPageClient>.Instance);
        var replyId = await client.ReplyCommentAsync("comm_123", "Dạ shop còn size L bạn nhé!", "test_token", TestContext.Current.CancellationToken);

        Assert.Equal("reply_999", replyId);
    }

    [Fact]
    public async Task PublishPostAsync_ReturnsPublishedPostId()
    {
        var json = """{ "id": "988656934325292_555" }""";

        var handler = new MockHttpMessageHandler(req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Contains("/988656934325292/feed", req.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var client = new FacebookPageClient(new HttpClient(handler), NullLogger<FacebookPageClient>.Instance);
        var result = await client.PublishPostAsync("988656934325292", "Bộ sưu tập thu đông mới nhất!", "test_token", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Ok);
        Assert.Equal("988656934325292_555", result.GraphPostId);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public async Task PublishPostAsync_WhenGraphError190_ReturnsFailWithCode190()
    {
        var json = """
        {
            "error": {
                "message": "Error validating access token: Session has expired on Monday, 24-Aug-26 00:00:00 PDT.",
                "type": "OAuthException",
                "code": 190,
                "error_subcode": 463
            }
        }
        """;

        var handler = new MockHttpMessageHandler(req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            return new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var client = new FacebookPageClient(new HttpClient(handler), NullLogger<FacebookPageClient>.Instance);
        var result = await client.PublishPostAsync("988656934325292", "Test post with expired token", "expired_token", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Ok);
        Assert.Null(result.GraphPostId);
        Assert.Equal("190", result.ErrorCode);
        Assert.Contains("Error validating access token", result.ErrorMessage);
    }

    [Fact]
    public async Task PublishPostAsync_WithSchedule_SendsPublishedFalseAndUnixSeconds()
    {
        var json = """{ "id": "988656934325292_sched_777" }""";
        var scheduledUtc = DateTimeOffset.UtcNow.AddHours(2);
        long expectedUnix = scheduledUtc.ToUnixTimeSeconds();

        var handler = new MockHttpMessageHandler(req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            var contentTask = req.Content!.ReadAsStringAsync();
            contentTask.Wait();
            var body = contentTask.Result;

            Assert.Contains("published=false", body);
            Assert.Contains($"scheduled_publish_time={expectedUnix}", body);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var client = new FacebookPageClient(new HttpClient(handler), NullLogger<FacebookPageClient>.Instance);
        var result = await client.PublishPostAsync("988656934325292", "Bài đăng hẹn giờ sáng mai", "test_token", scheduledPublishTime: scheduledUtc, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Ok);
        Assert.Equal("988656934325292_sched_777", result.GraphPostId);
    }

    [Fact]
    public void ScheduleTimeConversion_AsiaHoChiMinhToUtc_ConvertsAccurately()
    {
        // 07:00 AM on 2026-08-25 in Asia/Ho_Chi_Minh (UTC+7) -> 00:00:00 UTC on 2026-08-25
        var vnTimeString = "2026-08-25T07:00:00+07:00";
        var parsed = DateTimeOffset.Parse(vnTimeString);
        var utcTime = parsed.ToUniversalTime();

        Assert.Equal(0, utcTime.Hour);
        Assert.Equal(2026, utcTime.Year);
        Assert.Equal(8, utcTime.Month);
        Assert.Equal(25, utcTime.Day);

        long unixSeconds = utcTime.ToUnixTimeSeconds();
        var fromUnix = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        Assert.Equal(utcTime, fromUnix);
    }

    [Fact]
    public async Task CancelScheduledPostAsync_SendsDeleteRequest()
    {
        var handler = new MockHttpMessageHandler(req =>
        {
            Assert.Equal(HttpMethod.Delete, req.Method);
            Assert.Contains("/988656934325292_sched_777", req.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"success":true}""", System.Text.Encoding.UTF8, "application/json")
            };
        });

        var client = new FacebookPageClient(new HttpClient(handler), NullLogger<FacebookPageClient>.Instance);
        var cancelled = await client.CancelScheduledPostAsync("988656934325292_sched_777", "test_token", TestContext.Current.CancellationToken);

        Assert.True(cancelled);
    }

    [Fact]
    public async Task GetPagePostsAsync_ParsesFullPictureAndAttachmentsMedia()
    {
        var json = """
        {
            "data": [
                {
                    "id": "988656934325292_media_101",
                    "message": "BST mới kèm ảnh mẫu cực đẹp!",
                    "created_time": "2026-08-24T02:00:00+0000",
                    "permalink_url": "https://facebook.com/royceshop/posts/101",
                    "full_picture": "https://scontent.xx.fbcdn.net/v/t39.30808-6/sample.jpg",
                    "attachments": {
                        "data": [
                            {
                                "media_type": "photo",
                                "media": {
                                    "image": {
                                        "src": "https://scontent.xx.fbcdn.net/v/t39.30808-6/hd_sample.jpg"
                                    }
                                }
                            }
                        ]
                    },
                    "reactions": { "summary": { "total_count": 88 } },
                    "comments": { "summary": { "total_count": 12 } },
                    "shares": { "count": 5 }
                }
            ]
        }
        """;

        var handler = new MockHttpMessageHandler(req =>
        {
            var uri = req.RequestUri?.ToString() ?? string.Empty;
            if (uri.Contains("/posts"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":[]}""", System.Text.Encoding.UTF8, "application/json")
            };
        });

        var client = new FacebookPageClient(new HttpClient(handler), NullLogger<FacebookPageClient>.Instance);
        var posts = await client.GetPagePostsAsync("988656934325292", "test_token", TestContext.Current.CancellationToken);

        Assert.Single(posts);
        var post = posts[0];
        Assert.Equal("988656934325292_media_101", post.Id);
        Assert.Equal("https://scontent.xx.fbcdn.net/v/t39.30808-6/sample.jpg", post.FullPicture);
        Assert.Equal("photo", post.MediaType);
        Assert.Equal("https://scontent.xx.fbcdn.net/v/t39.30808-6/hd_sample.jpg", post.MediaUrl);
        Assert.Equal("https://scontent.xx.fbcdn.net/v/t39.30808-6/sample.jpg", post.ThumbnailUrl);
        Assert.Equal(88, post.ReactionCount);
        Assert.Equal(12, post.CommentCount);
        Assert.Equal(5, post.ShareCount);
    }

    [Fact]
    public async Task GetPostInsightsAsync_ParsesMetricsAndHandlesGraphPartial()
    {
        var json = """
        {
            "data": [
                {
                    "name": "post_impressions",
                    "values": [ { "value": 1520 } ]
                },
                {
                    "name": "post_engaged_users",
                    "values": [ { "value": 230 } ]
                },
                {
                    "name": "post_clicks",
                    "values": [ { "value": 85 } ]
                }
            ]
        }
        """;

        var handler = new MockHttpMessageHandler(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var client = new FacebookPageClient(new HttpClient(handler), NullLogger<FacebookPageClient>.Instance);
        var insights = await client.GetPostInsightsAsync("post_101", "test_token", TestContext.Current.CancellationToken);

        Assert.Equal(1520, insights.Impressions);
        Assert.Equal(230, insights.EngagedUsers);
        Assert.Equal(85, insights.Clicks);
        Assert.Equal("fresh", insights.DataFreshness);
    }

    [Fact]
    public async Task GetPostInsightsAsync_WhenCombinedUrlReturns400_RetriesPerMetricAndSetsPartialFreshness()
    {
        var handler = new MockHttpMessageHandler(req =>
        {
            var uri = req.RequestUri!.ToString();
            if (uri.Contains("metric=post_impressions%2Cpost_engaged_users%2Cpost_clicks") || uri.Contains("post_impressions,post_engaged_users,post_clicks"))
            {
                // Combined call fails with 400
                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("""{"error":{"message":"Unsupported metric combination","type":"OAuthException","code":100}}""", System.Text.Encoding.UTF8, "application/json")
                };
            }
            if (uri.Contains("metric=post_impressions"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"data":[{"name":"post_impressions","values":[{"value":1500}]}]}""", System.Text.Encoding.UTF8, "application/json")
                };
            }
            if (uri.Contains("metric=post_engaged_users"))
            {
                // Unsupported metric fails with 400
                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("""{"error":{"message":"Metric not supported for this post type","code":100}}""", System.Text.Encoding.UTF8, "application/json")
                };
            }
            if (uri.Contains("metric=post_clicks"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"data":[{"name":"post_clicks","values":[{"value":75}]}]}""", System.Text.Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var client = new FacebookPageClient(new HttpClient(handler), NullLogger<FacebookPageClient>.Instance);
        var insights = await client.GetPostInsightsAsync("post_101", "test_token", TestContext.Current.CancellationToken);

        Assert.Equal(1500, insights.Impressions);
        Assert.Equal(0, insights.EngagedUsers); // Skipped/unsupported
        Assert.Equal(75, insights.Clicks);
        Assert.Equal("partial", insights.DataFreshness);
    }
}
