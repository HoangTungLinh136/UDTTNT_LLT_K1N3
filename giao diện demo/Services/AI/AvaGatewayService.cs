using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using giao_dien_demo.Authorization;
using giao_dien_demo.Data;
using giao_dien_demo.Models.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace giao_dien_demo.Services.AI;

public sealed class AvaUpstreamException : Exception
{
    public AvaUpstreamException(HttpStatusCode statusCode, string code)
        : base(code) { StatusCode = statusCode; Code = code; }
    public HttpStatusCode StatusCode { get; }
    public string Code { get; }
}

public sealed class AvaGatewayService : IAvaGatewayService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ApplicationDbContext _db;
    private readonly IManagerScopeService _managerScope;
    private readonly ILeadershipScopeService _leadershipScope;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AvaGatewayService> _logger;

    private static bool IsImage(AiBrowserAttachment item) =>
        (item.ContentType ?? string.Empty).Split(';', 2)[0].Trim()
            .StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    private static bool IsAudio(AiBrowserAttachment item)
    {
        var contentType = (item.ContentType ?? string.Empty).Split(';', 2)[0].Trim();
        return contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) ||
            contentType.Equals("application/ogg", StringComparison.OrdinalIgnoreCase) ||
            contentType.Equals("video/webm", StringComparison.OrdinalIgnoreCase);
    }
    private readonly IHostEnvironment _environment;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IUserMemoryContextProvider _memoryContextProvider;
    private readonly IAiPermissionService _permissions;

    public AvaGatewayService(
        IHttpClientFactory httpClientFactory,
        ApplicationDbContext db,
        IManagerScopeService managerScope,
        ILeadershipScopeService leadershipScope,
        IConfiguration configuration,
        ILogger<AvaGatewayService> logger,
        IHttpContextAccessor httpContextAccessor,
        IUserMemoryContextProvider memoryContextProvider,
        IHostEnvironment environment,
        IAiPermissionService permissions)
    {
        _httpClientFactory = httpClientFactory;
        _db = db;
        _managerScope = managerScope;
        _leadershipScope = leadershipScope;
        _configuration = configuration;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _memoryContextProvider = memoryContextProvider;
        _environment = environment;
        _permissions = permissions;
    }

    public async Task<AiChatResponse> ChatAsync(
        string text,
        string? conversationId,
        IReadOnlyList<AiHistoryMessage> history,
        CancellationToken cancellationToken = default,
        IReadOnlyList<AiBrowserAttachment>? attachments = null)
    {
        var principal = _httpContextUser();
        var username = CurrentActor.Username(principal);
        var user = string.IsNullOrWhiteSpace(username)
            ? null
            : await _db.Users.AsNoTracking().Include(x => x.Employee)
                .SingleOrDefaultAsync(x => x.Username == username && x.CanLogin);
        if (user == null)
            throw new UnauthorizedAccessException("Authenticated HRMS user could not be resolved.");

        var context = await _permissions.GetContextAsync(user, cancellationToken);
        if (!context.Permissions.Contains("ai.chat")) throw new UnauthorizedAccessException("AI_CHAT_PERMISSION_DENIED");

        // =====================================================
        // PHASE 06 - TRUSTED PERSONAL MEMORY
        // =====================================================
        //
        // UserId không lấy từ browser.
        // user.Id được resolve từ authenticated ASP.NET context.
        //
        // Gateway chỉ gửi memory của chính user hiện tại.
        // Authorization vẫn thuộc backend/code.
        // =====================================================

        var memoryContext =
            await _memoryContextProvider.GetForUserAsync(
                user.Id.ToString(),
                cancellationToken
            );

        var submittedText = (text ?? string.Empty).Trim();
        var attachmentList = attachments ?? [];
        var modality = attachmentList.Any(IsImage)
            ? "IMAGE"
            : attachmentList.Any(IsAudio)
                ? "VOICE"
                : attachmentList.Count > 0 ? "DOCUMENT" : "TEXT";
        var effectiveText = !string.IsNullOrWhiteSpace(submittedText)
            ? submittedText
            : modality switch
            {
                "IMAGE" when attachmentList.All(IsImage)
                    => "Mô tả và phân tích nội dung ảnh đính kèm.",
                "VOICE" when attachmentList.All(IsAudio)
                    => "Chuyển lời nói thành văn bản và xử lý nội dung.",
                "DOCUMENT" => "Đọc và tóm tắt tài liệu đính kèm.",
                _ => "Phân tích các tệp đính kèm theo nội dung của chúng."
            };

        // Dùng anonymous payload để contract JSON có thể mở rộng
        // mà không buộc browser/client được quyền khai báo memory.
        var request = new
        {
            requestId =
                Guid.NewGuid().ToString("N"),

            traceId =
                Guid.NewGuid().ToString("N"),

            conversationId =
                conversationId,

            text =
                effectiveText,

            userText =
                submittedText,

            modality,

            attachments = attachmentList.Select(item => new
            {
                fileName = item.FileName,
                mimeType = item.ContentType,
                contentBase64 = item.ContentBase64
            }).ToArray(),

            userContext =
                context,

            history =
                history
                    .TakeLast(8)
                    .ToArray(),

            // Summary hiện là optional contract.
            // Khi có summary persistence riêng sẽ cấp giá trị tại đây.
            conversationSummary =
                (string?)null,

            userMemories =
                memoryContext
                    .Take(20)
                    .Select(
                        memory => new
                        {
                            memoryType =
                                memory.MemoryType,

                            memoryKey =
                                memory.MemoryKey,

                            content =
                                memory.Content,

                            confidence =
                                memory.Confidence,

                            source =
                                memory.Source
                        }
                    )
                    .ToArray()
        };

        var client = _httpClientFactory.CreateClient("AvaGateway");
        using var message = new HttpRequestMessage(HttpMethod.Post, "api/v1/chat")
        {
            Content = JsonContent.Create(request)
        };
        var secret = _configuration["Ava:InternalSecret"];
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("AVA_INTERNAL_SECRET_NOT_CONFIGURED");
        message.Headers.Add("X-AVA-Service-Key", secret);
        message.Headers.Add("X-Request-Id", Guid.NewGuid().ToString("N"));

        using var response = await client.SendAsync(message, cancellationToken);
        if (_environment.IsDevelopment())
        {
            _logger.LogInformation(
                "AVA upstream status={StatusCode}, contentType={ContentType}.",
                (int)response.StatusCode,
                response.Content.Headers.ContentType?.MediaType ?? "unknown");
        }
        if (!response.IsSuccessStatusCode)
        {
            var code = "PROVIDER_UNAVAILABLE";
            try
            {
                using var errorPayload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                if (errorPayload.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                    code = detail.GetString() ?? code;
            }
            catch (JsonException) { }
            _logger.LogWarning("AVA service returned HTTP {StatusCode}, code={Code}.", (int)response.StatusCode, code);
            throw new AvaUpstreamException(response.StatusCode, code);
        }

        try
        {
            var payload = await response.Content.ReadFromJsonAsync<AiChatResponse>(cancellationToken: cancellationToken);
            if (payload == null || string.IsNullOrWhiteSpace(payload.Answer))
            {
                _logger.LogError("AVA returned an empty or invalid success payload.");
                throw new InvalidDataException("AVA_INVALID_RESPONSE");
            }

            if (_environment.IsDevelopment())
            {
                _logger.LogInformation(
                    "AVA deserialize=success, answerPresent={AnswerPresent}, answerLength={AnswerLength}, conversationPresent={ConversationPresent}.",
                    !string.IsNullOrWhiteSpace(payload.Answer),
                    payload.Answer.Length,
                    !string.IsNullOrWhiteSpace(payload.ConversationId));
            }

            return payload;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "AVA response deserialization failed.");
            throw new InvalidDataException("AVA_INVALID_RESPONSE", ex);
        }
    }

    public async Task<string> TranscribeAsync(AiBrowserAttachment attachment, CancellationToken cancellationToken = default)
    {
        var principal = _httpContextUser();
        var username = CurrentActor.Username(principal);
        var authenticated = !string.IsNullOrWhiteSpace(username) &&
            await _db.Users.AsNoTracking().AnyAsync(x => x.Username == username && x.CanLogin, cancellationToken);
        if (!authenticated)
            throw new UnauthorizedAccessException("Authenticated HRMS user could not be resolved.");

        var client = _httpClientFactory.CreateClient("AvaGateway");
        using var message = new HttpRequestMessage(HttpMethod.Post, "api/v1/transcribe")
        {
            Content = JsonContent.Create(new
            {
                fileName = attachment.FileName,
                mimeType = attachment.ContentType,
                contentBase64 = attachment.ContentBase64
            })
        };
        var secret = _configuration["Ava:InternalSecret"];
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("AVA_INTERNAL_SECRET_NOT_CONFIGURED");
        message.Headers.Add("X-AVA-Service-Key", secret);
        using var response = await client.SendAsync(message, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // FINAL STT UPSTREAM ERROR DIAGNOSTIC
            // AVA /transcribe returns bounded machine-readable error codes.
            // Log only the safe detail code; never log audio/base64 content.
            var upstreamBody = await response.Content.ReadAsStringAsync(cancellationToken);

            var upstreamCode = "AVA_TRANSCRIPTION_FAILED";

            try
            {
                using var errorPayload = JsonDocument.Parse(upstreamBody);

                if (
                    errorPayload.RootElement.TryGetProperty("detail", out var detail) &&
                    detail.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(detail.GetString())
                )
                {
                    upstreamCode = detail.GetString()!;
                }
            }
            catch (JsonException)
            {
                // Keep bounded generic code.
            }

            _logger.LogWarning(
                "AVA transcription upstream failed status={StatusCode}, code={ErrorCode}.",
                (int)response.StatusCode,
                upstreamCode);

            throw new AvaUpstreamException(response.StatusCode, upstreamCode);
        }

        using var payload = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: cancellationToken);
        return payload?.RootElement.TryGetProperty("transcript", out var transcript) == true
            ? transcript.GetString() ?? string.Empty
            : throw new InvalidDataException("AVA_TRANSCRIPTION_INVALID");
    }

    private System.Security.Claims.ClaimsPrincipal _httpContextUser() =>
        _httpContextAccessor.HttpContext?.User
        ?? new System.Security.Claims.ClaimsPrincipal();

    private IReadOnlyList<string> ResolveDepartmentNames(string role, string? ownDepartment)
    {
        if (role == AppRoles.HR || role == AppRoles.Admin)
            return _db.Employees.AsNoTracking().Where(x => x.Department != null).Select(x => x.Department!).Distinct().ToArray();
        if (role == AppRoles.Manager)
            return _managerScope.GetManagedOrganizationalUnits();
        if (role == AppRoles.Leader)
            return _leadershipScope.GetOverseenOrganizationalUnits();
        return string.IsNullOrWhiteSpace(ownDepartment) ? [] : [ownDepartment];
    }

    private static IReadOnlyList<string> ResolvePermissions(string role) => role switch
    {
        AppRoles.HR => ["ai.live.read.company", "ai.personal.read.other"],
        AppRoles.Leader => ["ai.live.read.department", "ai.personal.read.other"],
        AppRoles.Manager => ["ai.live.read.department"],
        _ => []
    };
}

