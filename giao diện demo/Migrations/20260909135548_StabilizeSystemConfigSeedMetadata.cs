using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace giao_diện_demo.Migrations
{
    /// <inheritdoc />
    public partial class StabilizeSystemConfigSeedMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Address",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Email",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Name",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Phone",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Representative",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.ShortName",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.TaxCode",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Website",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Loc.Currency",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Loc.Language",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Loc.Timezone",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Storage.AllowedTypes",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Storage.MaxFileSizeMB",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Sys.MaintenanceMode",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Address",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 20, 48, 49, 524, DateTimeKind.Local).AddTicks(4784));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Email",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 20, 48, 49, 524, DateTimeKind.Local).AddTicks(4778));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Name",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 20, 48, 49, 523, DateTimeKind.Local).AddTicks(3260));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Phone",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 20, 48, 49, 524, DateTimeKind.Local).AddTicks(4780));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Representative",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 20, 48, 49, 524, DateTimeKind.Local).AddTicks(4786));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.ShortName",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 20, 48, 49, 524, DateTimeKind.Local).AddTicks(4761));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.TaxCode",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 20, 48, 49, 524, DateTimeKind.Local).AddTicks(4775));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Website",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 20, 48, 49, 524, DateTimeKind.Local).AddTicks(4782));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Loc.Currency",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 20, 48, 49, 524, DateTimeKind.Local).AddTicks(4865));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Loc.Language",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 20, 48, 49, 524, DateTimeKind.Local).AddTicks(4788));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Loc.Timezone",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 20, 48, 49, 524, DateTimeKind.Local).AddTicks(4863));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Storage.AllowedTypes",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 20, 48, 49, 524, DateTimeKind.Local).AddTicks(4869));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Storage.MaxFileSizeMB",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 20, 48, 49, 524, DateTimeKind.Local).AddTicks(4867));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Sys.MaintenanceMode",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 20, 48, 49, 524, DateTimeKind.Local).AddTicks(4871));
        }
    }
}
