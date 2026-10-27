using System.Collections.Generic;
using System.Threading.Tasks;
using giao_dien_demo.Models.AI;

namespace giao_dien_demo.Services.AI;

public interface IAvaConversationStore
{
    Task<IReadOnlyList<AiHistoryMessage>> LoadHistoryAsync(int userId, string? conversationId);
    Task<string> SaveExchangeAsync(int userId, string? conversationId, string question, AiChatResponse response);
}
