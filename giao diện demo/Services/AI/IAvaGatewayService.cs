using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using giao_dien_demo.Models.AI;

namespace giao_dien_demo.Services.AI;

public interface IAvaGatewayService
{
    Task<AiChatResponse> ChatAsync(
        string text,
        string? conversationId,
        IReadOnlyList<AiHistoryMessage> history,
        CancellationToken cancellationToken = default,
        IReadOnlyList<AiBrowserAttachment>? attachments = null);

    Task<string> TranscribeAsync(AiBrowserAttachment attachment, CancellationToken cancellationToken = default);
}
