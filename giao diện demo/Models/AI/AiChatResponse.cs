using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace giao_dien_demo.Models.AI;

public sealed class AiChatResponse
{
    [JsonPropertyName("requestId")]
    public string RequestId { get; init; } = string.Empty;

    [JsonPropertyName("traceId")]
    public string TraceId { get; init; } = string.Empty;

    [JsonPropertyName("conversationId")]
    public string? ConversationId { get; set; }

    [JsonPropertyName("answer")]
    public string Answer { get; init; } = string.Empty;

    [JsonPropertyName("route")]
    public string? Route { get; init; }

    [JsonPropertyName("intent")]
    public string Intent { get; init; } = string.Empty;

    [JsonPropertyName("sources")]
    public IReadOnlyList<SourceRefDto> Sources { get; init; } = [];

    [JsonPropertyName("citations")]
    public IReadOnlyList<SourceRefDto> Citations { get; init; } = [];

    [JsonPropertyName("warning")]
    public string? Warning { get; init; }

    [JsonPropertyName("canSpeak")]
    public bool CanSpeak { get; init; }

    [JsonPropertyName("verification")]
    public AiVerificationDto Verification { get; init; } = new();

    [JsonPropertyName("safety")]
    public AiSafetyDto Safety { get; init; } = new();

    [JsonPropertyName("attachmentContext")]
    public IReadOnlyDictionary<string, JsonElement> AttachmentContext { get; init; }
        = new Dictionary<string, JsonElement>();
}

public sealed class SourceRefDto
{
    [JsonPropertyName("source_type")]
    public string? SourceType { get; init; }

    [JsonPropertyName("source_name")]
    public string? SourceName { get; init; }

    [JsonPropertyName("file_name")]
    public string? FileName { get; init; }
}

public sealed class AiVerificationDto
{
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("checks")]
    public IReadOnlyList<string> Checks { get; init; } = [];

    [JsonPropertyName("warning")]
    public string? Warning { get; init; }
}

public sealed class AiSafetyDto
{
    [JsonPropertyName("decision")]
    public string? Decision { get; init; }

    [JsonPropertyName("reason_code")]
    public string? ReasonCode { get; init; }
}
