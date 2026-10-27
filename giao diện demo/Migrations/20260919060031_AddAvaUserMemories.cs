using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace giao_diện_demo.Migrations
{
    /// <inheritdoc />
    public partial class AddAvaUserMemories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserMemories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EmployeeId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    MemoryType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MemoryKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Confidence = table.Column<double>(type: "float", nullable: false, defaultValue: 1.0),
                    Source = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceConversationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMemories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserMemories_SourceConversationId",
                table: "UserMemories",
                column: "SourceConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMemories_User_Type_Key_Active",
                table: "UserMemories",
                columns: new[] { "UserId", "MemoryType", "MemoryKey", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_UserMemories_UserId_IsActive",
                table: "UserMemories",
                columns: new[] { "UserId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserMemories");
        }
    }
}
