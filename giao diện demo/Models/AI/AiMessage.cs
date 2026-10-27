using System;

namespace giao_dien_demo.Models.AI;

public sealed class AiMessage
{
    public int Id { get; set; }
    public string ConversationId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Route { get; set; }
    public string? Intent { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? SourceMetadataJson { get; set; }
    public string? WarningCode { get; set; }
    public AiConversation? Conversation { get; set; }
}
