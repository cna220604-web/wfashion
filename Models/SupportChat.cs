using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;

public static class SupportChatStatuses
{
    public const string Waiting = "Waiting";
    public const string Active = "Active";
    public const string Closed = "Closed";
}

public sealed class SupportConversation
{
    public int Id { get; set; }

    [Required, StringLength(450)]
    public string CustomerUserId { get; set; } = string.Empty;

    [Required, StringLength(36)]
    public string ClientSessionId { get; set; } = string.Empty;

    [StringLength(450)]
    public string? AssignedEmployeeUserId { get; set; }

    [Required, StringLength(20)]
    public string Status { get; set; } = SupportChatStatuses.Waiting;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<SupportMessage> Messages { get; set; } = new List<SupportMessage>();
}

public sealed class SupportMessage
{
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public SupportConversation Conversation { get; set; } = null!;

    [Required, StringLength(450)]
    public string SenderUserId { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string SenderName { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string SenderRole { get; set; } = string.Empty;

    [Required, StringLength(1200)]
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class SupportMessageRequest
{
    [Required, StringLength(1200, MinimumLength = 1)]
    public string Content { get; init; } = string.Empty;
}

public sealed class StartSupportChatRequest
{
    [Required, MinLength(1), MaxLength(12)]
    public List<SupportTranscriptMessage> Messages { get; init; } = [];
}

public sealed class SupportTranscriptMessage
{
    [Required, RegularExpression("^(user|assistant)$")]
    public string Role { get; init; } = string.Empty;

    [Required, StringLength(1200, MinimumLength = 1)]
    public string Content { get; init; } = string.Empty;
}

public sealed record SupportMessageView(int Id, string SenderName, string SenderRole, string Content, DateTime CreatedAtUtc);
public sealed record SupportConversationView(int Id, string CustomerName, string Status, string? AssignedEmployeeName, int MessageCount, DateTime UpdatedAtUtc);
