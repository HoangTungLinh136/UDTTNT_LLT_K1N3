using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using giao_dien_demo.Services.AI;
using giao_dien_demo.Authorization;
using giao_dien_demo.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace giao_dien_demo.Controllers;

[ApiController]
[Authorize]
[Route("api/ai")]
[AutoValidateAntiforgeryToken]
public sealed class CompanyAiWorkspaceController(
    ApplicationDbContext db,
    IHttpClientFactory clients, IAiPermissionService permissions, IConfiguration configuration, IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet("capabilities")]
    public async Task<IActionResult> Capabilities(CancellationToken cancellationToken)
    {
        var actor = await CurrentActor.ResolveUserAsync(User, db);
        if (actor == null) return Unauthorized();

        var context = await permissions.GetContextAsync(actor, cancellationToken);
        JsonElement service = JsonSerializer.SerializeToElement(new { status = "unavailable", fullyOperational = false });
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            using var response = await clients.CreateClient("AvaGateway").GetAsync("health", timeout.Token);
            if (response.IsSuccessStatusCode)
            {
                using var health = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
                service = health.RootElement.Clone();
            }
        }
        catch (HttpRequestException) { }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
        catch (JsonException) { }
        string Status(string capability)
        {
            if (service.TryGetProperty("capabilities", out var caps) && caps.TryGetProperty(capability, out var item)
                && item.TryGetProperty("status", out var status)) return status.GetString() ?? "unknown";
            return "unavailable";
        }
        var specs = new[]
        {
            ("chat", "Trò chuyện", "ai.chat", "text"),
            ("knowledge", "Kiến thức nội bộ", "ai.rag.search", "knowledge"),
            ("personal", "Thông tin cá nhân", "ai.personal.read.self", "sql"),
            ("hr", "Dữ liệu HRMS", "ai.hr.employee.read", "sql"),
            ("documents", "Đọc tài liệu", "ai.documents.read", "documents"),
            ("data", "Phân tích bảng tính", "ai.excel.analyze", "documents"),
            ("vision", "Đọc ảnh & OCR", "ai.vision.analyze", "vision"),
            ("transcribe", "Nhận dạng giọng nói", "ai.audio.stt", "speechToText"),
            ("tts", "Đọc câu trả lời", "ai.audio.tts", "textToSpeech"),
            ("training", "Bổ sung tri thức", "ai.skill.manage", "knowledge"),
            ("image", "Tạo ảnh", "ai.image.generate", "image"),
            ("word", "Tạo Word", "ai.documents.create", "office"),
            ("excel", "Tạo Excel", "ai.excel.create", "office"),
            ("slides", "Tạo PowerPoint", "ai.reports.create", "office"),
            ("coding", "Phân tích mã nguồn", "ai.coding.execute", "repository")
        };
        var flows = specs.Select(x => new
        {
            id = x.Item1, title = x.Item2, allowed = context.Permissions.Contains("ai.chat") && context.Permissions.Contains(x.Item3),
            status = !context.Permissions.Contains("ai.chat") || !context.Permissions.Contains(x.Item3) ? "forbidden"
                : x.Item4 == "sql" ? (service.TryGetProperty("sqlConnectivity", out var sql) && sql.GetString() == "ready" ? "ready" : "unavailable")
                : Status(x.Item4)
        });
        return Ok(new { service, user = new { dataScope = context.DataScope }, flows });
    }

    [HttpGet("conversations")]
    public async Task<IActionResult> Conversations(CancellationToken cancellationToken)
    {
        var actor = await CurrentActor.ResolveUserAsync(User, db);
        if (actor == null) return Unauthorized();
        var rows = await db.AiConversations.AsNoTracking().Where(x => x.UserId == actor.Id)
            .OrderByDescending(x => x.UpdatedAt).Take(100)
            .Select(x => new { x.Id, x.Title, x.CreatedAt, x.UpdatedAt })
            .ToListAsync(cancellationToken);
        return Ok(rows);
    }

    [HttpGet("conversations/{id}")]
    public async Task<IActionResult> Conversation(string id, CancellationToken cancellationToken)
    {
        var actor = await CurrentActor.ResolveUserAsync(User, db);
        if (actor == null) return Unauthorized();
        if (!await db.AiConversations.AsNoTracking()
            .AnyAsync(x => x.Id == id && x.UserId == actor.Id, cancellationToken))
            return NotFound(new { error = "Không tìm thấy hội thoại." });
        var messages = await db.AiMessages.AsNoTracking().Where(x => x.ConversationId == id)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(100)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => new { x.Role, x.Content, x.CreatedAt, x.Route, warning = x.WarningCode, x.SourceMetadataJson })
            .ToListAsync(cancellationToken);
        return Ok(new { id, messages = messages.Select(x => new {
            x.Role, x.Content, x.CreatedAt, x.Route, x.warning,
            attachmentContext = Metadata(x.SourceMetadataJson, "attachmentContext"),
            verification = Metadata(x.SourceMetadataJson, "verification"),
            sources = Metadata(x.SourceMetadataJson, "sources")
        }) });
    }

    private static JsonElement? Metadata(string? json, string key)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { using var doc = JsonDocument.Parse(json); return doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty(key, out var value) ? value.Clone() : null; }
        catch (JsonException) { return null; }
    }

    [HttpPost("tts")]
    [RequestSizeLimit(32_000)]
    public async Task<IActionResult> Speech([FromBody] SpeechRequest request, CancellationToken cancellationToken)
    {
        var actor = await CurrentActor.ResolveUserAsync(User, db);
        if (actor == null) return Unauthorized();
        var context = await permissions.GetContextAsync(actor, cancellationToken);
        if (!context.Permissions.Contains("ai.chat") || !context.Permissions.Contains("ai.audio.tts")) return Forbid();
        return await Proxy("api/v1/tts", new { request.Text, request.Voice }, cancellationToken);
    }

    [HttpPost("train-skill")]
    [RequestSizeLimit(32_000)]
    public async Task<IActionResult> Train([FromBody] KnowledgeRequest request, CancellationToken cancellationToken)
    {
        var actor = await CurrentActor.ResolveUserAsync(User, db);
        if (actor == null) return Unauthorized();
        var context = await permissions.GetContextAsync(actor, cancellationToken);
        if (!context.Permissions.Contains("ai.chat") || !context.Permissions.Contains("ai.skill.manage")) return Forbid();
        return await Proxy("api/v1/skills/train", new { request.Title, request.Content, request.Category, trainedBy = actor.Id.ToString() }, cancellationToken);
    }

    private async Task<IActionResult> Proxy(string path, object payload, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(payload) };
        var secret = configuration["Ava:InternalSecret"];
        if (string.IsNullOrWhiteSpace(secret)) return StatusCode(503, new { error = "Chưa cấu hình kết nối AVA." });
        message.Headers.Add("X-AVA-Service-Key", secret);
        try
        {
            using var response = await clients.CreateClient("AvaGateway").SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode) return StatusCode((int)response.StatusCode,
                new { error = "AVA chưa hoàn thành yêu cầu. Kiểm tra trạng thái kết nối và thử lại." });
            var result = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            return Ok(result);
        }
        catch (HttpRequestException) { return StatusCode(503, new { error = "Chưa kết nối được dịch vụ AVA." }); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return StatusCode(504, new { error = "AVA đã hết thời gian xử lý." }); }
    }

    [HttpGet("conversations/{id}/artifacts/{fileName}")]
    public async Task<IActionResult> Artifact(string id, string fileName, CancellationToken cancellationToken)
    {
        var actor = await CurrentActor.ResolveUserAsync(User, db);
        if (actor == null) return Unauthorized();
        var context = await permissions.GetContextAsync(actor, cancellationToken);
        if (!context.Permissions.Contains("ai.chat")) return Forbid();
        if (fileName != Path.GetFileName(fileName) || fileName.IndexOfAny(['/', '\\']) >= 0) return NotFound();
        var exists = await db.AiConversations.AsNoTracking().AnyAsync(x => x.Id == id && x.UserId == actor.Id, cancellationToken);
        if (!exists) return NotFound();
        var metadata = await db.AiMessages.AsNoTracking().Where(x => x.ConversationId == id && x.SourceMetadataJson != null)
            .Select(x => x.SourceMetadataJson).ToListAsync(cancellationToken);
        var root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuration["Ava:ArtifactRoot"] ?? "../../../data/agent_outputs"));
        string? artifact = null;
        foreach (var json in metadata)
        {
            try
            {
                using var doc = JsonDocument.Parse(json!);
                var node = doc.RootElement;
                if (node.ValueKind != JsonValueKind.Object) continue;
                if (node.TryGetProperty("attachmentContext", out var nested)) node = nested;
                if (!node.TryGetProperty("agentArtifacts", out var files) || files.ValueKind != JsonValueKind.Array) continue;
                foreach (var file in files.EnumerateArray())
                {
                    if (file.ValueKind != JsonValueKind.String) continue;
                    var raw = file.GetString();
                    if (string.IsNullOrWhiteSpace(raw) || Path.GetFileName(raw) != fileName) continue;
                    var full = Path.GetFullPath(raw);
                    if (string.Equals(Path.GetDirectoryName(full), root, StringComparison.OrdinalIgnoreCase)) artifact = full;
                }
            }
            catch (JsonException) { }
            catch (ArgumentException) { }
        }
        var mime = Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".pdf" => "application/pdf", ".png" => "image/png", _ => null
        };
        if (artifact == null || mime == null || !System.IO.File.Exists(artifact)) return NotFound();
        // Reject reparse points so a replaced file cannot escape the authorized output directory.
        if ((System.IO.File.GetAttributes(artifact) & FileAttributes.ReparsePoint) != 0) return NotFound();
        return PhysicalFile(artifact, mime, fileName);
    }
}

public sealed class SpeechRequest
{
    [Required, StringLength(4000, MinimumLength = 1)] public string Text { get; set; } = "";
    [StringLength(80)] public string? Voice { get; set; }
}
public sealed class KnowledgeRequest
{
    [Required, StringLength(160, MinimumLength = 1)] public string Title { get; set; } = "";
    [Required, StringLength(4000, MinimumLength = 5)] public string Content { get; set; } = "";
    [StringLength(80)] public string? Category { get; set; }
}
