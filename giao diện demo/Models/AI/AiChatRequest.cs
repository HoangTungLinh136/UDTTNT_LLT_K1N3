using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace giao_dien_demo.Models.AI;

public sealed class AiChatRequest
{
    [JsonPropertyName("requestId")]
    public string? RequestId { get; init; }

    [JsonPropertyName("traceId")]
    public string? TraceId { get; init; }

    [JsonPropertyName("conversationId")]
    public string? ConversationId { get; init; }

    [JsonPropertyName("text")]
    public string Text { get; init; } = string.Empty;

    [JsonPropertyName("modality")]
    public string Modality { get; init; } = "TEXT";

    [JsonPropertyName("userContext")]
    public AvaUserContext UserContext { get; init; } = new();

    [JsonPropertyName("history")]
    public IReadOnlyList<AiHistoryMessage> History { get; init; } = [];
}

public sealed class AiBrowserChatRequest
{
    [JsonPropertyName("conversationId")]
    public string? ConversationId { get; init; }

    [JsonPropertyName("text")]
    public string Text { get; init; } = string.Empty;

    [JsonPropertyName("attachments")]
    public IReadOnlyList<AiBrowserAttachment> Attachments { get; init; } = [];
}

public sealed class AiBrowserAttachment
{
    [JsonPropertyName("fileName")]
    public string FileName { get; init; } = string.Empty;

    [JsonPropertyName("contentType")]
    public string ContentType { get; init; } = string.Empty;

    [JsonPropertyName("contentBase64")]
    public string ContentBase64 { get; init; } = string.Empty;
}

public sealed class AiBrowserTranscribeRequest
{
    [JsonPropertyName("fileName")]
    public string FileName { get; init; } = string.Empty;
    [JsonPropertyName("contentType")]
    public string ContentType { get; init; } = string.Empty;
    [JsonPropertyName("contentBase64")]
    public string ContentBase64 { get; init; } = string.Empty;
}

public sealed class AiHistoryMessage
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;

    [JsonPropertyName("attachmentContext")]
    public IReadOnlyDictionary<string, JsonElement> AttachmentContext { get; init; }
        = new Dictionary<string, JsonElement>();
}
