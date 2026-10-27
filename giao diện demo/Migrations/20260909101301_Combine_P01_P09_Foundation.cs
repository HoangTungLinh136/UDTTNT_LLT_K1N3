using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace giao_diện_demo.Migrations
{
    /// <inheritdoc />
    public partial class Combine_P01_P09_Foundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppointmentRecords_Employees_EmployeeId",
                table: "AppointmentRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_DismissalRecords_Employees_EmployeeId",
                table: "DismissalRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_IncidentParticipants_Employees_EmployeeId",
                table: "IncidentParticipants");

            migrationBuilder.DropForeignKey(
                name: "FK_IncidentRecords_Employees_ResponsiblePersonId",
                table: "IncidentRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_OffboardingRecords_Employees_EmployeeId",
                table: "OffboardingRecords");

            // Some legacy databases recorded the onboarding migrations without
            // creating this FK. Drop it only when it is physically present; the
            // canonical Restrict FK is added below for both legacy and clean DBs.
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.foreign_keys
                           WHERE name = N'FK_OnboardingRecords_Employees_EmployeeId'
                             AND parent_object_id = OBJECT_ID(N'[OnboardingRecords]'))
                    ALTER TABLE [OnboardingRecords]
                    DROP CONSTRAINT [FK_OnboardingRecords_Employees_EmployeeId];");

            migrationBuilder.DropForeignKey(
                name: "FK_RewardRecords_Employees_EmployeeId",
                table: "RewardRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_TransferRecords_Employees_EmployeeId",
                table: "TransferRecords");

            migrationBuilder.RenameColumn(
                name: "EmployeeName",
                table: "Salaries",
                newName: "EmployeeNameSnapshot");

            migrationBuilder.RenameColumn(
                name: "EmployeeName",
                table: "Leaves",
                newName: "EmployeeNameSnapshot");

            migrationBuilder.RenameColumn(
                name: "EmployeeName",
                table: "Contracts",
                newName: "EmployeeNameSnapshot");

            migrationBuilder.RenameColumn(
                name: "EmployeeName",
                table: "Attendances",
                newName: "EmployeeNameSnapshot");

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "Users",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "EmployeeId",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "EmployeeId",
                table: "Salaries",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "EmployeeId",
                table: "Leaves",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EmployeeCode",
                table: "Employees",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmployeeId",
                table: "Contracts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmployeeId",
                table: "Attendances",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Address",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 17, 12, 58, 529, DateTimeKind.Local).AddTicks(1372));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Email",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 17, 12, 58, 529, DateTimeKind.Local).AddTicks(1366));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Name",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 17, 12, 58, 527, DateTimeKind.Local).AddTicks(6501));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Phone",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 17, 12, 58, 529, DateTimeKind.Local).AddTicks(1368));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Representative",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 17, 12, 58, 529, DateTimeKind.Local).AddTicks(1374));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.ShortName",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 17, 12, 58, 529, DateTimeKind.Local).AddTicks(1350));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.TaxCode",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 17, 12, 58, 529, DateTimeKind.Local).AddTicks(1364));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Website",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 17, 12, 58, 529, DateTimeKind.Local).AddTicks(1370));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Loc.Currency",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 17, 12, 58, 529, DateTimeKind.Local).AddTicks(1380));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Loc.Language",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 17, 12, 58, 529, DateTimeKind.Local).AddTicks(1376));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Loc.Timezone",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 17, 12, 58, 529, DateTimeKind.Local).AddTicks(1378));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Storage.AllowedTypes",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 17, 12, 58, 529, DateTimeKind.Local).AddTicks(1383));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Storage.MaxFileSizeMB",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 17, 12, 58, 529, DateTimeKind.Local).AddTicks(1381));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Sys.MaintenanceMode",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 17, 12, 58, 529, DateTimeKind.Local).AddTicks(1385));

            migrationBuilder.CreateIndex(
                name: "IX_Users_EmployeeId",
                table: "Users",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Salaries_EmployeeId",
                table: "Salaries",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Leaves_EmployeeId",
                table: "Leaves",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_EmployeeCode",
                table: "Employees",
                column: "EmployeeCode",
                unique: true,
                filter: "[EmployeeCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_EmployeeId",
                table: "Contracts",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_EmployeeId",
                table: "Attendances",
                column: "EmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppointmentRecords_Employees_EmployeeId",
                table: "AppointmentRecords",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Attendances_Employees_EmployeeId",
                table: "Attendances",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_Employees_EmployeeId",
                table: "Contracts",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DismissalRecords_Employees_EmployeeId",
                table: "DismissalRecords",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IncidentParticipants_Employees_EmployeeId",
                table: "IncidentParticipants",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IncidentRecords_Employees_ResponsiblePersonId",
                table: "IncidentRecords",
                column: "ResponsiblePersonId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Leaves_Employees_EmployeeId",
                table: "Leaves",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OffboardingRecords_Employees_EmployeeId",
                table: "OffboardingRecords",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OnboardingRecords_Employees_EmployeeId",
                table: "OnboardingRecords",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RewardRecords_Employees_EmployeeId",
                table: "RewardRecords",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Salaries_Employees_EmployeeId",
                table: "Salaries",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TransferRecords_Employees_EmployeeId",
                table: "TransferRecords",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Employees_EmployeeId",
                table: "Users",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppointmentRecords_Employees_EmployeeId",
                table: "AppointmentRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_Attendances_Employees_EmployeeId",
                table: "Attendances");

            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_Employees_EmployeeId",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_DismissalRecords_Employees_EmployeeId",
                table: "DismissalRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_IncidentParticipants_Employees_EmployeeId",
                table: "IncidentParticipants");

            migrationBuilder.DropForeignKey(
                name: "FK_IncidentRecords_Employees_ResponsiblePersonId",
                table: "IncidentRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_Leaves_Employees_EmployeeId",
                table: "Leaves");

            migrationBuilder.DropForeignKey(
                name: "FK_OffboardingRecords_Employees_EmployeeId",
                table: "OffboardingRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_OnboardingRecords_Employees_EmployeeId",
                table: "OnboardingRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_RewardRecords_Employees_EmployeeId",
                table: "RewardRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_Salaries_Employees_EmployeeId",
                table: "Salaries");

            migrationBuilder.DropForeignKey(
                name: "FK_TransferRecords_Employees_EmployeeId",
                table: "TransferRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Employees_EmployeeId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_EmployeeId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Username",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Salaries_EmployeeId",
                table: "Salaries");

            migrationBuilder.DropIndex(
                name: "IX_Leaves_EmployeeId",
                table: "Leaves");

            migrationBuilder.DropIndex(
                name: "IX_Employees_EmployeeCode",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_EmployeeId",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_Attendances_EmployeeId",
                table: "Attendances");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "Leaves");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "Attendances");

            migrationBuilder.RenameColumn(
                name: "EmployeeNameSnapshot",
                table: "Salaries",
                newName: "EmployeeName");

            migrationBuilder.RenameColumn(
                name: "EmployeeNameSnapshot",
                table: "Leaves",
                newName: "EmployeeName");

            migrationBuilder.RenameColumn(
                name: "EmployeeNameSnapshot",
                table: "Contracts",
                newName: "EmployeeName");

            migrationBuilder.RenameColumn(
                name: "EmployeeNameSnapshot",
                table: "Attendances",
                newName: "EmployeeName");

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<int>(
                name: "EmployeeId",
                table: "Salaries",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EmployeeCode",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Address",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 16, 35, 12, 424, DateTimeKind.Local).AddTicks(5219));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Email",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 16, 35, 12, 424, DateTimeKind.Local).AddTicks(5212));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Name",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 16, 35, 12, 422, DateTimeKind.Local).AddTicks(227));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Phone",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 16, 35, 12, 424, DateTimeKind.Local).AddTicks(5214));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Representative",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 16, 35, 12, 424, DateTimeKind.Local).AddTicks(5222));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.ShortName",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 16, 35, 12, 424, DateTimeKind.Local).AddTicks(5180));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.TaxCode",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 16, 35, 12, 424, DateTimeKind.Local).AddTicks(5209));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Company.Website",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 16, 35, 12, 424, DateTimeKind.Local).AddTicks(5217));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Loc.Currency",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 16, 35, 12, 424, DateTimeKind.Local).AddTicks(5228));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Loc.Language",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 16, 35, 12, 424, DateTimeKind.Local).AddTicks(5224));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Loc.Timezone",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 16, 35, 12, 424, DateTimeKind.Local).AddTicks(5226));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Storage.AllowedTypes",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 16, 35, 12, 424, DateTimeKind.Local).AddTicks(5232));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Storage.MaxFileSizeMB",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 16, 35, 12, 424, DateTimeKind.Local).AddTicks(5230));

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Key",
                keyValue: "Sys.MaintenanceMode",
                column: "UpdatedAt",
                value: new DateTime(2026, 9, 9, 16, 35, 12, 424, DateTimeKind.Local).AddTicks(5235));

            migrationBuilder.AddForeignKey(
                name: "FK_AppointmentRecords_Employees_EmployeeId",
                table: "AppointmentRecords",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DismissalRecords_Employees_EmployeeId",
                table: "DismissalRecords",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_IncidentParticipants_Employees_EmployeeId",
                table: "IncidentParticipants",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_IncidentRecords_Employees_ResponsiblePersonId",
                table: "IncidentRecords",
                column: "ResponsiblePersonId",
                principalTable: "Employees",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OffboardingRecords_Employees_EmployeeId",
                table: "OffboardingRecords",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OnboardingRecords_Employees_EmployeeId",
                table: "OnboardingRecords",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RewardRecords_Employees_EmployeeId",
                table: "RewardRecords",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TransferRecords_Employees_EmployeeId",
                table: "TransferRecords",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
