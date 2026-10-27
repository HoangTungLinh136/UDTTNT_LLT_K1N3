using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace giao_diện_demo.Migrations
{
    /// <inheritdoc />
    public partial class AddManagerScopeAssignmentsP26 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Preserve all legacy columns. Add canonical columns and copy data
            // according to business meaning instead of relying on rename guesses.
            migrationBuilder.AddColumn<string>(
                name: "EmployeeNameSnapshot",
                table: "TrainingCertificates",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProgressStatus",
                table: "CourseEnrollments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EmployeeNameSnapshot",
                table: "CourseEnrollments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EmployeeNameSnapshot",
                table: "BenefitEntitlements",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EmployeeNameSnapshot",
                table: "BenefitEnrollments",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                table: "BenefitEnrollments",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "DRAFT");

            migrationBuilder.Sql(@"
                UPDATE [TrainingCertificates] SET [EmployeeNameSnapshot] = LEFT([EmployeeName], 100);
                UPDATE [CourseEnrollments] SET [EmployeeNameSnapshot] = LEFT([EmployeeName], 100), [ProgressStatus] = LEFT([Status], 50);
                UPDATE [BenefitEntitlements] SET [EmployeeNameSnapshot] = [EmployeeName];
                UPDATE [BenefitEnrollments] SET [EmployeeNameSnapshot] = [EmployeeName], [ApprovalStatus] = [Status];");

            migrationBuilder.AddColumn<string>(
                name: "CanonicalFileId",
                table: "TrainingCertificates",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "EmployeeId",
                table: "TrainingCertificates",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LeaveType",
                table: "Leaves",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Leaves",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "Leaves",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedBy",
                table: "Leaves",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                table: "CourseEnrollments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "EmployeeId",
                table: "CourseEnrollments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Contracts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "Contracts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedBy",
                table: "Contracts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmployeeId",
                table: "BenefitEntitlements",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EmployeeId",
                table: "BenefitEnrollments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Restore canonical User/record -> Employee links only where the
            // historical snapshot has one unambiguous exact employee match.
            migrationBuilder.Sql(@"
                UPDATE u SET u.EmployeeId = e.Id
                FROM [Users] u INNER JOIN [Employees] e ON e.[Name] = u.[FullName]
                WHERE u.EmployeeId IS NULL;

                UPDATE a SET a.EmployeeId = e.Id
                FROM [Attendances] a INNER JOIN [Employees] e ON e.[Name] = a.[EmployeeNameSnapshot]
                WHERE a.EmployeeId IS NULL;

                UPDATE c SET c.EmployeeId = e.Id
                FROM [Contracts] c INNER JOIN [Employees] e ON e.[Name] = c.[EmployeeNameSnapshot]
                WHERE c.EmployeeId IS NULL;

                UPDATE l SET l.EmployeeId = e.Id
                FROM [Leaves] l INNER JOIN [Employees] e ON e.[Name] = l.[EmployeeNameSnapshot]
                WHERE l.EmployeeId IS NULL;

                UPDATE s SET s.EmployeeId = e.Id
                FROM [Salaries] s INNER JOIN [Employees] e ON e.[Name] = s.[EmployeeNameSnapshot]
                WHERE s.EmployeeId IS NULL;

                UPDATE b SET b.EmployeeId = e.Id
                FROM [BenefitEnrollments] b INNER JOIN [Employees] e ON e.[Name] = b.[EmployeeNameSnapshot]
                WHERE b.EmployeeId = 0;

                UPDATE b SET b.EmployeeId = e.Id
                FROM [BenefitEntitlements] b INNER JOIN [Employees] e ON e.[Name] = b.[EmployeeNameSnapshot]
                WHERE b.EmployeeId = 0;

                UPDATE c SET c.EmployeeId = e.Id
                FROM [CourseEnrollments] c INNER JOIN [Employees] e ON e.[Name] = c.[EmployeeNameSnapshot]
                WHERE c.EmployeeId = 0;

                UPDATE t SET t.EmployeeId = e.Id
                FROM [TrainingCertificates] t INNER JOIN [Employees] e ON e.[Name] = t.[EmployeeNameSnapshot]
                WHERE t.EmployeeId = 0;");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "AttendanceAdjustmentRequests",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateTable(
                name: "ManagerAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    OrganizationalUnit = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AssignedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagerAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ManagerAssignments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_TrainingCertificates_EmployeeId",
                table: "TrainingCertificates",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_SuccessionPlans_PlanningPeriodId",
                table: "SuccessionPlans",
                column: "PlanningPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_SuccessionPlans_TargetPositionId",
                table: "SuccessionPlans",
                column: "TargetPositionId");

            migrationBuilder.CreateIndex(
                name: "IX_SuccessionHistories_SuccessionPlanId",
                table: "SuccessionHistories",
                column: "SuccessionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_SuccessionCandidates_EmployeeId",
                table: "SuccessionCandidates",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_SuccessionCandidates_SuccessionPlanId",
                table: "SuccessionCandidates",
                column: "SuccessionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseEnrollments_EmployeeId",
                table: "CourseEnrollments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateDevelopmentPlans_SuccessionCandidateId",
                table: "CandidateDevelopmentPlans",
                column: "SuccessionCandidateId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BenefitEntitlements_EmployeeId",
                table: "BenefitEntitlements",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_BenefitEnrollments_EmployeeId",
                table: "BenefitEnrollments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ManagerAssignments_UserId",
                table: "ManagerAssignments",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_BenefitEnrollments_Employees_EmployeeId",
                table: "BenefitEnrollments",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BenefitEntitlements_Employees_EmployeeId",
                table: "BenefitEntitlements",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CandidateDevelopmentPlans_SuccessionCandidates_SuccessionCandidateId",
                table: "CandidateDevelopmentPlans",
                column: "SuccessionCandidateId",
                principalTable: "SuccessionCandidates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CourseEnrollments_Employees_EmployeeId",
                table: "CourseEnrollments",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SuccessionCandidates_Employees_EmployeeId",
                table: "SuccessionCandidates",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SuccessionCandidates_SuccessionPlans_SuccessionPlanId",
                table: "SuccessionCandidates",
                column: "SuccessionPlanId",
                principalTable: "SuccessionPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SuccessionHistories_SuccessionPlans_SuccessionPlanId",
                table: "SuccessionHistories",
                column: "SuccessionPlanId",
                principalTable: "SuccessionPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SuccessionPlans_JobPositions_TargetPositionId",
                table: "SuccessionPlans",
                column: "TargetPositionId",
                principalTable: "JobPositions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SuccessionPlans_PlanningPeriods_PlanningPeriodId",
                table: "SuccessionPlans",
                column: "PlanningPeriodId",
                principalTable: "PlanningPeriods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TrainingCertificates_Employees_EmployeeId",
                table: "TrainingCertificates",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BenefitEnrollments_Employees_EmployeeId",
                table: "BenefitEnrollments");

            migrationBuilder.DropForeignKey(
                name: "FK_BenefitEntitlements_Employees_EmployeeId",
                table: "BenefitEntitlements");

            migrationBuilder.DropForeignKey(
                name: "FK_CandidateDevelopmentPlans_SuccessionCandidates_SuccessionCandidateId",
                table: "CandidateDevelopmentPlans");

            migrationBuilder.DropForeignKey(
                name: "FK_CourseEnrollments_Employees_EmployeeId",
                table: "CourseEnrollments");

            migrationBuilder.DropForeignKey(
                name: "FK_SuccessionCandidates_Employees_EmployeeId",
                table: "SuccessionCandidates");

            migrationBuilder.DropForeignKey(
                name: "FK_SuccessionCandidates_SuccessionPlans_SuccessionPlanId",
                table: "SuccessionCandidates");

            migrationBuilder.DropForeignKey(
                name: "FK_SuccessionHistories_SuccessionPlans_SuccessionPlanId",
                table: "SuccessionHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_SuccessionPlans_JobPositions_TargetPositionId",
                table: "SuccessionPlans");

            migrationBuilder.DropForeignKey(
                name: "FK_SuccessionPlans_PlanningPeriods_PlanningPeriodId",
                table: "SuccessionPlans");

            migrationBuilder.DropForeignKey(
                name: "FK_TrainingCertificates_Employees_EmployeeId",
                table: "TrainingCertificates");

            migrationBuilder.DropTable(
                name: "ManagerAssignments");

            migrationBuilder.DropIndex(
                name: "IX_TrainingCertificates_EmployeeId",
                table: "TrainingCertificates");

            migrationBuilder.DropIndex(
                name: "IX_SuccessionPlans_PlanningPeriodId",
                table: "SuccessionPlans");

            migrationBuilder.DropIndex(
                name: "IX_SuccessionPlans_TargetPositionId",
                table: "SuccessionPlans");

            migrationBuilder.DropIndex(
                name: "IX_SuccessionHistories_SuccessionPlanId",
                table: "SuccessionHistories");

            migrationBuilder.DropIndex(
                name: "IX_SuccessionCandidates_EmployeeId",
                table: "SuccessionCandidates");

            migrationBuilder.DropIndex(
                name: "IX_SuccessionCandidates_SuccessionPlanId",
                table: "SuccessionCandidates");

            migrationBuilder.DropIndex(
                name: "IX_CourseEnrollments_EmployeeId",
                table: "CourseEnrollments");

            migrationBuilder.DropIndex(
                name: "IX_CandidateDevelopmentPlans_SuccessionCandidateId",
                table: "CandidateDevelopmentPlans");

            migrationBuilder.DropIndex(
                name: "IX_BenefitEntitlements_EmployeeId",
                table: "BenefitEntitlements");

            migrationBuilder.DropIndex(
                name: "IX_BenefitEnrollments_EmployeeId",
                table: "BenefitEnrollments");

            migrationBuilder.DropColumn(
                name: "CanonicalFileId",
                table: "TrainingCertificates");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "TrainingCertificates");

            migrationBuilder.DropColumn(
                name: "LeaveType",
                table: "Leaves");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Leaves");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "Leaves");

            migrationBuilder.DropColumn(
                name: "ReviewedBy",
                table: "Leaves");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "CourseEnrollments");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "CourseEnrollments");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "ReviewedBy",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "BenefitEntitlements");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "BenefitEnrollments");

            migrationBuilder.DropColumn(
                name: "EmployeeNameSnapshot",
                table: "TrainingCertificates");

            migrationBuilder.DropColumn(
                name: "ProgressStatus",
                table: "CourseEnrollments");

            migrationBuilder.DropColumn(
                name: "EmployeeNameSnapshot",
                table: "CourseEnrollments");

            migrationBuilder.DropColumn(
                name: "EmployeeNameSnapshot",
                table: "BenefitEntitlements");

            migrationBuilder.DropColumn(
                name: "EmployeeNameSnapshot",
                table: "BenefitEnrollments");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "BenefitEnrollments");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "AttendanceAdjustmentRequests",
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
        }
    }
}
