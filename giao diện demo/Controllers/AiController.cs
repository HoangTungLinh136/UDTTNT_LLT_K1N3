using System;
using System.IO;
using System.Net.Http;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using giao_dien_demo.Authorization;
using giao_dien_demo.Data;
using giao_dien_demo.Models.AI;
using giao_dien_demo.Services;
using giao_dien_demo.Services.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace giao_dien_demo.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize]
[AutoValidateAntiforgeryToken]
public sealed class AiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAvaGatewayService _gateway;
    private readonly IAvaConversationStore _conversationStore;
    private readonly IAuditLogService _auditLog;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<AiController> _logger;

    public AiController(
        ApplicationDbContext db,
        IAvaGatewayService gateway,
        IAvaConversationStore conversationStore,
        IAuditLogService auditLog,
        IHostEnvironment environment,
        ILogger<AiController> logger)
    {
        _db = db;
        _gateway = gateway;
        _conversationStore = conversationStore;
        _auditLog = auditLog;
        _environment = environment;
        _logger = logger;
    }

    [HttpPost("chat")]
    [RequestSizeLimit(30_000_000)]
    public async Task<ActionResult<AiChatResponse>> Chat(
        [FromBody] AiBrowserChatRequest request,
        CancellationToken cancellationToken)
    {
        var attachments = request?.Attachments ?? [];
        if (request == null || (string.IsNullOrWhiteSpace(request.Text) && attachments.Count == 0))
            return BadRequest(new { error = "Bạn chưa nhập câu hỏi." });

        if (attachments.Count > 5)
            return BadRequest(new { error = "AVA hỗ trợ tối đa 5 tệp mỗi lượt." });
        long attachmentBytes = 0;
        foreach (var attachment in attachments)
        {
            if (!IsSupportedAttachment(attachment, out var decodedLength))
                return BadRequest(new { error = "Tệp đính kèm không hợp lệ hoặc định dạng chưa hỗ trợ." });
            attachmentBytes += decodedLength;
            if (attachmentBytes > 20L * 1024 * 1024)
                return StatusCode(StatusCodes.Status413PayloadTooLarge, new { error = "Tổng tệp đính kèm tối đa 20 MiB." });
        }

        var actor = await CurrentActor.ResolveUserAsync(User, _db);
        if (actor == null)
            return Unauthorized(new { error = "Phiên đăng nhập không còn hợp lệ." });

        var history = await _conversationStore.LoadHistoryAsync(actor.Id, request.ConversationId);
        AiChatResponse response;
        try
        {
            response = await _gateway.ChatAsync(request.Text, request.ConversationId, history, cancellationToken, attachments);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return StatusCode(504, new { error = "AVA hết thời gian phản hồi. Vui lòng thử lại." });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "Không thể xác thực người dùng AVA." });
        }
        catch (InvalidDataException)
        {
            return StatusCode(503, new { error = "AVA trả về dữ liệu không hợp lệ. Vui lòng thử lại sau." });
        }
        catch (AvaUpstreamException ex)
        {
            var safeCode = ex.Code switch
            {
                "ATTACHMENT_INVALID" or "ATTACHMENT_TOO_LARGE" or "UNSUPPORTED_MIME" or
                "ATTACHMENT_BASE64_INVALID" or "ATTACHMENT_SIGNATURE_MISMATCH" or
                "ATTACHMENT_EXPANSION_LIMIT_EXCEEDED" or "IMAGE_INVALID" or
                "IMAGE_ANALYSIS_UNAVAILABLE" or "OCR_UNAVAILABLE" or
                "VISION_UNAVAILABLE" or "AUDIO_INVALID" or "AUDIO_DURATION_LIMIT_EXCEEDED" or
                "STT_UNAVAILABLE" or "STT_EMPTY_TRANSCRIPT" or "DOCUMENT_RAG_UNAVAILABLE" or
                "DOCUMENT_TEXT_EMPTY" or "DOCUMENT_PARSE_FAILED" or
                "DOCUMENT_PAGE_LIMIT_EXCEEDED" or "DOCUMENT_CELL_LIMIT_EXCEEDED" or
                "PROVIDER_TIMEOUT" or "PERMISSION_DENIED" or "DATA_NOT_FOUND" or
                "NO_INTERNAL_EVIDENCE" or "ATTACHMENT_PROMPT_INJECTION" => ex.Code,
                _ => "PROVIDER_UNAVAILABLE"
            };
            return StatusCode((int)ex.StatusCode, new
            {
                code = safeCode,
                error = safeCode switch
                {
                    "ATTACHMENT_TOO_LARGE" => "Tệp đính kèm vượt quá dung lượng cho phép.",
                    "UNSUPPORTED_MIME" => "Định dạng tệp chưa được hỗ trợ.",
                    "OCR_UNAVAILABLE" => "OCR local hiện chưa sẵn sàng.",
                    "IMAGE_ANALYSIS_UNAVAILABLE" => "Model phân tích ảnh local hiện chưa sẵn sàng.",
                    "VISION_UNAVAILABLE" => "Vision local hiện chưa sẵn sàng.",
                    "STT_UNAVAILABLE" => "STT local hiện chưa sẵn sàng.",
                    "STT_EMPTY_TRANSCRIPT" => "Không nhận dạng được lời nói trong tệp âm thanh.",
                    "AUDIO_DURATION_LIMIT_EXCEEDED" => "Âm thanh phải dài không quá 120 giây.",
                    "ATTACHMENT_SIGNATURE_MISMATCH" => "Nội dung tệp không khớp với định dạng đã khai báo.",
                    "DOCUMENT_TEXT_EMPTY" => "Tài liệu không chứa nội dung văn bản có thể đọc được.",
                    "DOCUMENT_PARSE_FAILED" => "Không thể đọc cấu trúc tài liệu. Tệp có thể bị hỏng hoặc được mã hóa.",
                    "DOCUMENT_PAGE_LIMIT_EXCEEDED" => "Tài liệu PDF vượt quá giới hạn 500 trang.",
                    "DOCUMENT_CELL_LIMIT_EXCEEDED" => "Bảng tính vượt quá giới hạn xử lý an toàn.",
                    "DOCUMENT_RAG_UNAVAILABLE" => "Bộ phân tích tài liệu local hiện chưa sẵn sàng.",
                    "PERMISSION_DENIED" => "Bạn không có quyền thực hiện yêu cầu này.",
                    _ => "AVA chưa thể xử lý yêu cầu này. Vui lòng thử lại."
                }
            });
        }
        catch (HttpRequestException)
        {
            return StatusCode(503, new { error = "Không thể kết nối đến AVA. Vui lòng thử lại sau." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AVA gateway unexpected failure.");
            return StatusCode(503, new { error = "AVA hiện chưa sẵn sàng. Vui lòng thử lại sau." });
        }

        var storedQuestion = request.Text.Trim();
        if (string.IsNullOrWhiteSpace(storedQuestion))
            storedQuestion = "[Tệp đính kèm: " + string.Join(", ", attachments.Select(x => Path.GetFileName(x.FileName))) + "]";
        response.ConversationId = await _conversationStore.SaveExchangeAsync(
            actor.Id,
            request.ConversationId,
            storedQuestion,
            response);
        if (_environment.IsDevelopment())
        {
            _logger.LogInformation(
                    "AVA controller gateway success, answerPresent={AnswerPresent}, answerLength={AnswerLength}, returnedHttp=200.",
                    !string.IsNullOrWhiteSpace(response.Answer),
                    response.Answer?.Length ?? 0);
        }
        try
        {
            await _auditLog.LogAsync("ChatAi", "AiController", "Conversation", response.ConversationId, "Info", "Success");
        }
        catch (Exception ex)
        {
            // Conversation persistence is already best-effort by contract. An
            // unavailable audit table/connection must not turn a valid AVA
            // answer into an HTTP 500 after the upstream request succeeded.
            _logger.LogWarning(ex, "AVA audit persistence failed after successful response.");
        }
        return Ok(response);
    }

    private static bool IsSupportedAttachment(AiBrowserAttachment attachment, out int decodedLength)
    {
        decodedLength = 0;
        if (attachment == null || string.IsNullOrWhiteSpace(attachment.FileName) ||
            attachment.FileName.Length > 180 || string.IsNullOrWhiteSpace(attachment.ContentBase64) ||
            attachment.ContentBase64.Length > 28_000_000)
            return false;
        var extension = Path.GetExtension(Path.GetFileName(attachment.FileName)).ToLowerInvariant();
        var accepted = extension switch
        {
            ".png" => new[] { "image/png" }, ".jpg" or ".jpeg" => new[] { "image/jpeg", "image/jpg" },
            ".webp" => new[] { "image/webp" }, ".bmp" => new[] { "image/bmp" },
            ".pdf" => new[] { "application/pdf" }, ".docx" => new[] { "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
            ".xlsx" => new[] { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" }, ".txt" => new[] { "text/plain" },
            ".csv" => new[] { "text/csv", "application/vnd.ms-excel" }, ".json" => new[] { "application/json", "text/json" },
            ".wav" => new[] { "audio/wav", "audio/x-wav", "audio/wave", "audio/vnd.wave" },
            ".mp3" => new[] { "audio/mpeg", "audio/mp3" }, ".m4a" => new[] { "audio/mp4", "audio/x-m4a" },
            ".ogg" => new[] { "audio/ogg", "application/ogg" }, ".webm" => new[] { "audio/webm", "video/webm" }, _ => null
        };
        var suppliedType = (attachment.ContentType ?? string.Empty).Split(';', 2)[0].Trim();
        if (accepted == null || !accepted.Contains(suppliedType, StringComparer.OrdinalIgnoreCase))
            return false;
        try
        {
            decodedLength = Convert.FromBase64String(attachment.ContentBase64).Length;
            return decodedLength > 0 && decodedLength <= 20 * 1024 * 1024;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    [HttpPost("transcribe")]
    [RequestSizeLimit(30_000_000)]
    public async Task<IActionResult> Transcribe([FromBody] AiBrowserTranscribeRequest request, [FromServices] IAiPermissionService permissions, CancellationToken cancellationToken)
    {
        var actor = await CurrentActor.ResolveUserAsync(User, _db);
        if (actor == null) return Unauthorized();
        var context = await permissions.GetContextAsync(actor, cancellationToken);
        if (!context.Permissions.Contains("ai.chat") || !context.Permissions.Contains("ai.audio.stt")) return Forbid();
        var file = new AiBrowserAttachment
        {
            FileName = request.FileName,
            ContentType = request.ContentType,
            ContentBase64 = request.ContentBase64
        };
        if (!IsSupportedAttachment(file, out var length))
            return BadRequest(new { error = "Tệp âm thanh không hợp lệ." });
        if (length > 20 * 1024 * 1024)
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new { error = "Âm thanh vượt quá 20 MiB." });
        try
        {
            var transcript = await _gateway.TranscribeAsync(file, cancellationToken);
            return Ok(new { transcript });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return StatusCode(504, new { error = "Nhận dạng giọng nói đã hết thời gian." });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "Phiên đăng nhập không còn hợp lệ." });
        }
        catch (AvaUpstreamException ex)
        {
            var code = ex.Code switch
            {
                "AUDIO_INVALID" or "AUDIO_DURATION_LIMIT_EXCEEDED" or "ATTACHMENT_SIGNATURE_MISMATCH" or
                "ATTACHMENT_BASE64_INVALID" or "ATTACHMENT_TOO_LARGE" or "UNSUPPORTED_MIME" or
                "STT_EMPTY_TRANSCRIPT" or "STT_UNAVAILABLE" => ex.Code,
                _ => "STT_UNAVAILABLE"
            };
            return StatusCode((int)ex.StatusCode, new
            {
                code,
                error = code switch
                {
                    "AUDIO_DURATION_LIMIT_EXCEEDED" => "Âm thanh phải dài không quá 120 giây.",
                    "STT_EMPTY_TRANSCRIPT" => "Không nhận dạng được lời nói trong bản ghi.",
                    "AUDIO_INVALID" or "ATTACHMENT_SIGNATURE_MISMATCH" => "Tệp âm thanh không hợp lệ.",
                    _ => "Dịch vụ STT local chưa sẵn sàng."
                }
            });
        }
        catch (HttpRequestException)
        {
            return StatusCode(503, new { error = "Dịch vụ STT local chưa sẵn sàng." });
        }
    }
}

