using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace giao_diện_demo.Migrations
{
    /// <inheritdoc />
    public partial class P43_CompleteRelationshipConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Proposals_RelatedEmployeeId",
                table: "Proposals",
                column: "RelatedEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_SourceProposalId",
                table: "Proposals",
                column: "SourceProposalId");

            migrationBuilder.CreateIndex(
                name: "IX_ProposalHistories_ProposalId",
                table: "ProposalHistories",
                column: "ProposalId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPayments_EmployeeId",
                table: "PayrollPayments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_RuleId",
                table: "NotificationLogs",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "IX_HrTasks_AssigneeEmployeeId",
                table: "HrTasks",
                column: "AssigneeEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_HrTasks_RelatedEmployeeId",
                table: "HrTasks",
                column: "RelatedEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_HrTaskHistories_HrTaskId",
                table: "HrTaskHistories",
                column: "HrTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceAdjustmentRequests_AttendanceId",
                table: "AttendanceAdjustmentRequests",
                column: "AttendanceId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceAdjustmentAttachments_RequestId",
                table: "AttendanceAdjustmentAttachments",
                column: "RequestId");

            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceAdjustmentAttachments_AttendanceAdjustmentRequests_RequestId",
                table: "AttendanceAdjustmentAttachments",
                column: "RequestId",
                principalTable: "AttendanceAdjustmentRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceAdjustmentRequests_Attendances_AttendanceId",
                table: "AttendanceAdjustmentRequests",
                column: "AttendanceId",
                principalTable: "Attendances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_HrTaskHistories_HrTasks_HrTaskId",
                table: "HrTaskHistories",
                column: "HrTaskId",
                principalTable: "HrTasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_HrTasks_Employees_AssigneeEmployeeId",
                table: "HrTasks",
                column: "AssigneeEmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_HrTasks_Employees_RelatedEmployeeId",
                table: "HrTasks",
                column: "RelatedEmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationLogs_NotificationRules_RuleId",
                table: "NotificationLogs",
                column: "RuleId",
                principalTable: "NotificationRules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollPayments_Employees_EmployeeId",
                table: "PayrollPayments",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProposalHistories_Proposals_ProposalId",
                table: "ProposalHistories",
                column: "ProposalId",
                principalTable: "Proposals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Proposals_Employees_RelatedEmployeeId",
                table: "Proposals",
                column: "RelatedEmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Proposals_Proposals_SourceProposalId",
                table: "Proposals",
                column: "SourceProposalId",
                principalTable: "Proposals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AttendanceAdjustmentAttachments_AttendanceAdjustmentRequests_RequestId",
                table: "AttendanceAdjustmentAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_AttendanceAdjustmentRequests_Attendances_AttendanceId",
                table: "AttendanceAdjustmentRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_HrTaskHistories_HrTasks_HrTaskId",
                table: "HrTaskHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_HrTasks_Employees_AssigneeEmployeeId",
                table: "HrTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_HrTasks_Employees_RelatedEmployeeId",
                table: "HrTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_NotificationLogs_NotificationRules_RuleId",
                table: "NotificationLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_PayrollPayments_Employees_EmployeeId",
                table: "PayrollPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_ProposalHistories_Proposals_ProposalId",
                table: "ProposalHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_Proposals_Employees_RelatedEmployeeId",
                table: "Proposals");

            migrationBuilder.DropForeignKey(
                name: "FK_Proposals_Proposals_SourceProposalId",
                table: "Proposals");

            migrationBuilder.DropIndex(
                name: "IX_Proposals_RelatedEmployeeId",
                table: "Proposals");

            migrationBuilder.DropIndex(
                name: "IX_Proposals_SourceProposalId",
                table: "Proposals");

            migrationBuilder.DropIndex(
                name: "IX_ProposalHistories_ProposalId",
                table: "ProposalHistories");

            migrationBuilder.DropIndex(
                name: "IX_PayrollPayments_EmployeeId",
                table: "PayrollPayments");

            migrationBuilder.DropIndex(
                name: "IX_NotificationLogs_RuleId",
                table: "NotificationLogs");

            migrationBuilder.DropIndex(
                name: "IX_HrTasks_AssigneeEmployeeId",
                table: "HrTasks");

            migrationBuilder.DropIndex(
                name: "IX_HrTasks_RelatedEmployeeId",
                table: "HrTasks");

            migrationBuilder.DropIndex(
                name: "IX_HrTaskHistories_HrTaskId",
                table: "HrTaskHistories");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceAdjustmentRequests_AttendanceId",
                table: "AttendanceAdjustmentRequests");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceAdjustmentAttachments_RequestId",
                table: "AttendanceAdjustmentAttachments");
        }
    }
}
