using System;
using System.Collections.Generic;

namespace giao_dien_demo.Models.AI;

public sealed class AiConversation
{
    public string Id { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string? Title { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public User? User { get; set; }
    public ICollection<AiMessage> Messages { get; set; } = new List<AiMessage>();
}
