using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace giao_diện_demo.Migrations
{
    /// <inheritdoc />
    public partial class Folder2SchemaCompatibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPublished",
                table: "TrainingMaterials",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalFileName",
                table: "TrainingMaterials",
                type: "nvarchar(520)",
                maxLength: 520,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StoredFileName",
                table: "TrainingMaterials",
                type: "nvarchar(520)",
                maxLength: 520,
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "DurationHours",
                table: "TrainingCourses",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EligibleDepartment",
                table: "TrainingCourses",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EligiblePosition",
                table: "TrainingCourses",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublished",
                table: "TrainingCourses",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "PrerequisiteCourseId",
                table: "TrainingCourses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProgramId",
                table: "TrainingCourses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Skill",
                table: "TrainingCourses",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "TrainingCertificates",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublished",
                table: "TrainingCertificates",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ProgressPercent",
                table: "CourseEnrollments",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "CourseEnrollments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Score",
                table: "CourseEnrollments",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "CourseEnrollments",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "SELF");

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "CourseEnrollments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ContractSalarySnapshot",
                table: "Contracts",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DepartmentSnapshot",
                table: "Contracts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmployeeSignedAt",
                table: "Contracts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JobTitleSnapshot",
                table: "Contracts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PositionSnapshot",
                table: "Contracts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "BenefitEnrollments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StatusReason",
                table: "BenefitEnrollments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Details",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPublished",
                table: "TrainingMaterials");

            migrationBuilder.DropColumn(
                name: "OriginalFileName",
                table: "TrainingMaterials");

            migrationBuilder.DropColumn(
                name: "StoredFileName",
                table: "TrainingMaterials");

            migrationBuilder.DropColumn(
                name: "EligibleDepartment",
                table: "TrainingCourses");

            migrationBuilder.DropColumn(
                name: "EligiblePosition",
                table: "TrainingCourses");

            migrationBuilder.DropColumn(
                name: "IsPublished",
                table: "TrainingCourses");

            migrationBuilder.DropColumn(
                name: "PrerequisiteCourseId",
                table: "TrainingCourses");

            migrationBuilder.DropColumn(
                name: "ProgramId",
                table: "TrainingCourses");

            migrationBuilder.DropColumn(
                name: "Skill",
                table: "TrainingCourses");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "TrainingCertificates");

            migrationBuilder.DropColumn(
                name: "IsPublished",
                table: "TrainingCertificates");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "CourseEnrollments");

            migrationBuilder.DropColumn(
                name: "Score",
                table: "CourseEnrollments");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "CourseEnrollments");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "CourseEnrollments");

            migrationBuilder.DropColumn(
                name: "ContractSalarySnapshot",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "DepartmentSnapshot",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "EmployeeSignedAt",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "JobTitleSnapshot",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "PositionSnapshot",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "BenefitEnrollments");

            migrationBuilder.DropColumn(
                name: "StatusReason",
                table: "BenefitEnrollments");

            migrationBuilder.DropColumn(
                name: "Details",
                table: "AuditLogs");

            migrationBuilder.AlterColumn<int>(
                name: "DurationHours",
                table: "TrainingCourses",
                type: "int",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ProgressPercent",
                table: "CourseEnrollments",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");
        }
    }
}

