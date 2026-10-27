using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace giao_diện_demo.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttendanceRuleVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VersionCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ScopeType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ScopeId = table.Column<int>(type: "int", nullable: true),
                    LateGraceMinutes = table.Column<int>(type: "int", nullable: false),
                    LateFullCreditThresholdMinutes = table.Column<int>(type: "int", nullable: false),
                    LateHandlingMode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LateMaxDeductionMinutes = table.Column<int>(type: "int", nullable: false),
                    EarlyLeaveGraceMinutes = table.Column<int>(type: "int", nullable: false),
                    EarlyLeaveFullCreditThresholdMinutes = table.Column<int>(type: "int", nullable: false),
                    EarlyLeaveHandlingMode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EarlyLeaveMaxDeductionMinutes = table.Column<int>(type: "int", nullable: false),
                    CheckInRoundingMode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CheckInRoundingStepMinutes = table.Column<int>(type: "int", nullable: false),
                    CheckOutRoundingMode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CheckOutRoundingStepMinutes = table.Column<int>(type: "int", nullable: false),
                    MissingCheckInMode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MissingCheckInAssumedTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    MissingCheckOutMode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MissingCheckOutAssumedTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    DailyOtThreshold = table.Column<double>(type: "float", nullable: false),
                    WeeklyOtThreshold = table.Column<double>(type: "float", nullable: false),
                    AutoCalculateOt = table.Column<bool>(type: "bit", nullable: false),
                    AllowAfterShiftOt = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceRuleVersions", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceRuleVersions");
        }
    }
}
