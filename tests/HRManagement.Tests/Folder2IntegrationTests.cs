using System.Text.Json;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using giao_dien_demo.Models.AI;
using giao_dien_demo.Services.AI;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HRManagement.Tests;

public sealed class Folder2IntegrationTests
{
    [Fact]
    public async Task Unlinked_onboarding_record_materializes_without_creating_an_employee()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.Set<OnboardingRecord>().Add(new OnboardingRecord { EmployeeId = null, EmployeeName = "Pending hire" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Assert.Null((await db.Set<OnboardingRecord>().SingleAsync()).EmployeeId);
        Assert.Empty(await db.Employees.ToListAsync());
    }

    [Theory]
    [InlineData(typeof(AuditLog), "Resource", "AuditLogs", "Resource")]
    [InlineData(typeof(AuditLog), "EventType", "AuditLogs", "EventType")]
    [InlineData(typeof(BenefitEnrollment), "BenefitProgramId", "BenefitEnrollments", "BenefitProgramId")]
    [InlineData(typeof(BenefitEnrollment), "RegistrationDate", "BenefitEnrollments", "RegistrationDate")]
    [InlineData(typeof(CourseEnrollment), "ApprovalStatus", "CourseEnrollments", "ApprovalStatus")]
    [InlineData(typeof(CourseEnrollment), "ProgressStatus", "CourseEnrollments", "ProgressStatus")]
    [InlineData(typeof(TrainingMaterial), "Title", "TrainingMaterials", "Title")]
    [InlineData(typeof(TrainingCourse), "Instructor", "TrainingCourses", "Instructor")]
    [InlineData(typeof(TrainingCertificate), "IssuedDate", "TrainingCertificates", "IssuedDate")]
    public void Existing_columns_keep_the_verified_folder1_mapping(Type type, string property, string table, string column)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=unused;Database=metadata_only;Trusted_Connection=True;TrustServerCertificate=True").Options);
        var entity = db.Model.FindEntityType(type)!;
        Assert.Equal(table, entity.GetTableName());
        Assert.Equal(column, entity.FindProperty(property)!.GetColumnName(StoreObjectIdentifier.Table(table, entity.GetSchema())));
    }

    [Fact]
    public async Task Audit_bigint_identity_and_security_metadata_roundtrip()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.AuditLogs.Add(new AuditLog { Id = 3_000_000_001L, Action = "Login", EventType = "Security", Status = "Failed", Role = "Employee", Resource = "User", Details = "F2 details retained" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var audit = await db.AuditLogs.SingleAsync(x => x.EventType == "Security" && x.Status == "Failed");
        Assert.Equal(3_000_000_001L, audit.Id);
        Assert.Equal("Employee", audit.Role);
        Assert.Equal("F2 details retained", audit.Details);
    }

    [Fact]
    public async Task Attachment_history_is_persisted_and_isolated_by_user_and_conversation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var owner = new User { Username = "integration.owner", FullName = "Owner", Role = "Employee", CanLogin = true, Email = "owner@example.test" };
        var other = new User { Username = "integration.other", FullName = "Other", Role = "Employee", CanLogin = true, Email = "other@example.test" };
        db.Users.AddRange(owner, other);
        await db.SaveChangesAsync();
        var store = new AvaConversationStore(db, NullLogger<AvaConversationStore>.Instance);
        var context = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("""
            {"attachment_count":1,"items":[{"file_name":"contract.txt","mime_type":"text/plain","extracted_text":"Hợp đồng 12 tháng"}]}
            """)!;
        var id = await store.SaveExchangeAsync(owner.Id, null, "phân tích tài liệu này", new AiChatResponse { Answer = "Document analysis", AttachmentContext = context });
        var history = await store.LoadHistoryAsync(owner.Id, id);
        Assert.Equal(2, history.Count);
        Assert.Equal("user", history[0].Role);
        Assert.Contains("12 tháng", history[0].AttachmentContext["items"][0].GetProperty("extracted_text").GetString());
        Assert.Empty(await store.LoadHistoryAsync(other.Id, id));
        Assert.Empty(await store.LoadHistoryAsync(owner.Id, null));
        Assert.Empty(await store.LoadHistoryAsync(owner.Id, "new-conversation"));
        var otherId = await store.SaveExchangeAsync(other.Id, id, "hello", new AiChatResponse { Answer = "hello" });
        Assert.NotEqual(id, otherId);
        Assert.Equal(2, (await store.LoadHistoryAsync(owner.Id, id)).Count);
    }
}
