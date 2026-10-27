using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using giao_dien_demo.Data;
using giao_dien_demo.Models.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace giao_dien_demo.Services.AI;

public sealed class AvaConversationStore : IAvaConversationStore
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<AvaConversationStore> _logger;

    public AvaConversationStore(ApplicationDbContext db, ILogger<AvaConversationStore> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AiHistoryMessage>> LoadHistoryAsync(int userId, string? conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId)) return [];
        try
        {
            var ownsConversation = await _db.AiConversations.AsNoTracking()
                .AnyAsync(x => x.Id == conversationId && x.UserId == userId);
            if (!ownsConversation) return [];

            var rows = await _db.AiMessages.AsNoTracking()
                .Where(x => x.ConversationId == conversationId)
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .Take(8)
                .OrderBy(x => x.CreatedAt)
                .ThenBy(x => x.Id)
                .ToListAsync();
            return rows.Select(x => new AiHistoryMessage
            {
                Role = x.Role,
                Content = x.Content,
                AttachmentContext = DeserializeAttachmentContext(x.SourceMetadataJson)
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AVA conversation history is unavailable.");
            return [];
        }
    }

    public async Task<string> SaveExchangeAsync(int userId, string? conversationId, string question, AiChatResponse response)
    {
        var id = conversationId;
        try
        {
            var conversation = string.IsNullOrWhiteSpace(id)
                ? null
                : await _db.AiConversations.SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId);

            if (conversation == null)
            {
                id = Guid.NewGuid().ToString("N");
                conversation = new AiConversation
                {
                    Id = id,
                    UserId = userId,
                    Title = question.Trim().Length > 200 ? question.Trim()[..200] : question.Trim(),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.AiConversations.Add(conversation);
            }

            var now = DateTime.UtcNow;
            _db.AiMessages.Add(new AiMessage
            {
                ConversationId = id!,
                Role = "user",
                Content = question.Trim(),
                SourceMetadataJson = response.AttachmentContext.Count == 0
                    ? null
                    : JsonSerializer.Serialize(response.AttachmentContext),
                CreatedAt = now
            });
            _db.AiMessages.Add(new AiMessage
            {
                ConversationId = id!,
                Role = "assistant",
                Content = response.Answer,
                Route = response.Route,
                Intent = response.Intent,
                WarningCode = response.Warning,
                SourceMetadataJson = response.Sources.Count == 0 ? null : JsonSerializer.Serialize(response.Sources),
                CreatedAt = now
            });
            conversation!.UpdatedAt = now;
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // AI answers remain usable if the optional conversation migration
            // has not yet been applied; never log message content or PII.
            _logger.LogWarning(ex, "AVA conversation could not be persisted.");
        }

        return id ?? Guid.NewGuid().ToString("N");
    }

    private static IReadOnlyDictionary<string, JsonElement> DeserializeAttachmentContext(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, JsonElement>();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
                ?? new Dictionary<string, JsonElement>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, JsonElement>();
        }
    }
}
