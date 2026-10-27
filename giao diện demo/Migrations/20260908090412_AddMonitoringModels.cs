using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace giao_diện_demo.Migrations
{
    /// <inheritdoc />
    public partial class AddMonitoringModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MonitoringAlerts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Severity = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AlertName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MetricName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CurrentValue = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Threshold = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AcknowledgedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcknowledgedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonitoringAlerts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemErrorLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Level = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Service = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExceptionType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StackTrace = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequestId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    User = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IP = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemErrorLogs", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Address",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 8, 16, 4, 11, 648, DateTimeKind.Local).AddTicks(2737));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Email",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 8, 16, 4, 11, 648, DateTimeKind.Local).AddTicks(2733));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Name",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 8, 16, 4, 11, 647, DateTimeKind.Local).AddTicks(7128));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Phone",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 8, 16, 4, 11, 648, DateTimeKind.Local).AddTicks(2734));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Representative",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 8, 16, 4, 11, 648, DateTimeKind.Local).AddTicks(2738));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.ShortName",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 8, 16, 4, 11, 648, DateTimeKind.Local).AddTicks(2721));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.TaxCode",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 8, 16, 4, 11, 648, DateTimeKind.Local).AddTicks(2731));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Website",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 8, 16, 4, 11, 648, DateTimeKind.Local).AddTicks(2736));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Loc.Currency",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 8, 16, 4, 11, 648, DateTimeKind.Local).AddTicks(2742));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Loc.Language",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 8, 16, 4, 11, 648, DateTimeKind.Local).AddTicks(2739));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Loc.Timezone",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 8, 16, 4, 11, 648, DateTimeKind.Local).AddTicks(2741));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Storage.AllowedTypes",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 8, 16, 4, 11, 648, DateTimeKind.Local).AddTicks(2744));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Storage.MaxFileSizeMB",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 8, 16, 4, 11, 648, DateTimeKind.Local).AddTicks(2743));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Sys.MaintenanceMode",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 8, 16, 4, 11, 648, DateTimeKind.Local).AddTicks(2746));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MonitoringAlerts");

            migrationBuilder.DropTable(
                name: "SystemErrorLogs");

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Address",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 8, 15, 53, 8, 118, DateTimeKind.Local).AddTicks(3914));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Email",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 8, 15, 53, 8, 118, DateTimeKind.Local).AddTicks(3911));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Name",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 8, 15, 53, 8, 117, DateTimeKind.Local).AddTicks(9132));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Phone",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 8, 15, 53, 8, 118, DateTimeKind.Local).AddTicks(3912));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Representative",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 8, 15, 53, 8, 118, DateTimeKind.Local).AddTicks(3916));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.ShortName",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 8, 15, 53, 8, 118, DateTimeKind.Local).AddTicks(3902));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.TaxCode",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 8, 15, 53, 8, 118, DateTimeKind.Local).AddTicks(3909));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Website",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 8, 15, 53, 8, 118, DateTimeKind.Local).AddTicks(3913));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Loc.Currency",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 8, 15, 53, 8, 118, DateTimeKind.Local).AddTicks(3919));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Loc.Language",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 8, 15, 53, 8, 118, DateTimeKind.Local).AddTicks(3917));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Loc.Timezone",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 8, 15, 53, 8, 118, DateTimeKind.Local).AddTicks(3918));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Storage.AllowedTypes",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 8, 15, 53, 8, 118, DateTimeKind.Local).AddTicks(3921));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Storage.MaxFileSizeMB",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 8, 15, 53, 8, 118, DateTimeKind.Local).AddTicks(3920));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Sys.MaintenanceMode",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 8, 15, 53, 8, 118, DateTimeKind.Local).AddTicks(3923));
        }
    }
}
