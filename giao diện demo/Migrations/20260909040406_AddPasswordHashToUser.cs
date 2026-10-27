using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace giao_diện_demo.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordHashToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Password",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Address",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 11, 4, 3, 523, DateTimeKind.Local).AddTicks(299));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Email",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 11, 4, 3, 523, DateTimeKind.Local).AddTicks(294));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Name",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 11, 4, 3, 522, DateTimeKind.Local).AddTicks(3742));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Phone",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 11, 4, 3, 523, DateTimeKind.Local).AddTicks(295));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Representative",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 11, 4, 3, 523, DateTimeKind.Local).AddTicks(300));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.ShortName",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 11, 4, 3, 523, DateTimeKind.Local).AddTicks(284));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.TaxCode",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 11, 4, 3, 523, DateTimeKind.Local).AddTicks(292));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Website",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 11, 4, 3, 523, DateTimeKind.Local).AddTicks(297));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Loc.Currency",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 11, 4, 3, 523, DateTimeKind.Local).AddTicks(303));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Loc.Language",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 11, 4, 3, 523, DateTimeKind.Local).AddTicks(301));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Loc.Timezone",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 11, 4, 3, 523, DateTimeKind.Local).AddTicks(302));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Storage.AllowedTypes",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 11, 4, 3, 523, DateTimeKind.Local).AddTicks(308));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Storage.MaxFileSizeMB",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 11, 4, 3, 523, DateTimeKind.Local).AddTicks(307));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Sys.MaintenanceMode",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 11, 4, 3, 523, DateTimeKind.Local).AddTicks(309));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "Password",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

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
    }
}
