namespace DXOS.Infrastructure.Persistence.Entities;

public sealed class SocialPageRecord
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Type { get; set; } = "facebook";
    public bool IsActive { get; set; } = true;
    public int TotalConversations { get; set; }
    public int TotalMessages { get; set; }
    public DateTimeOffset? LastSyncAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SocialCustomerRecord
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? PageId { get; set; }
    public string PhoneNumbersJson { get; set; } = "[]";
    public string EmailsJson { get; set; } = "[]";
    public string TagsJson { get; set; } = "[]";
    public int OrderCount { get; set; }
    public decimal PurchasedAmount { get; set; }
    public DateTimeOffset? FirstSeenAt { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SocialConversationRecord
{
    public string Id { get; set; } = string.Empty;
    public string? PageId { get; set; }
    public string? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? Snippet { get; set; }
    public int MessageCount { get; set; }
    public bool HasPhone { get; set; }
    public bool IsReplied { get; set; }
    public string Status { get; set; } = "open"; // "open", "pending", "done", "spam"
    public string? AssignedToActor { get; set; }
    public string? InternalNote { get; set; }
    public string TagsJson { get; set; } = "[]";
    public DateTimeOffset InsertedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset SyncedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SocialMessageRecord
{
    public string Id { get; set; } = string.Empty;
    public string? ConversationId { get; set; }
    public string? PageId { get; set; }
    public string? SenderId { get; set; }
    public string? SenderName { get; set; }
    public string? SenderType { get; set; } // "customer", "agent", "system"
    public string? Content { get; set; }
    public string? ContentHtml { get; set; }
    public string? MessageType { get; set; } = "text";
    public string AttachmentsJson { get; set; } = "[]";
    public string ReactionsJson { get; set; } = "[]";
    public bool IsUnsent { get; set; }
    public DateTimeOffset? CreatedTime { get; set; }
    public DateTimeOffset SyncedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
