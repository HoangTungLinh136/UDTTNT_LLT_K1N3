using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace giao_diện_demo.Migrations
{
    /// <inheritdoc />
    public partial class AddBenefitModuleClean : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BenefitEnrollments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BenefitProgramId = table.Column<int>(type: "int", nullable: false),
                    RegistrationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BenefitEnrollments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BenefitEntitlements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BenefitProgramId = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BenefitEntitlements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BenefitPrograms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BenefitType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RegistrationStart = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RegistrationEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequiresApproval = table.Column<bool>(type: "bit", nullable: false),
                    IsAutoApplied = table.Column<bool>(type: "bit", nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: true),
                    RequiredContractType = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BenefitPrograms", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "BenefitPrograms",
                columns: new[] { "Id", "BenefitType", "Capacity", "Description", "IsAutoApplied", "Name", "RegistrationEnd", "RegistrationStart", "RequiredContractType", "RequiresApproval", "Status" },
                values: new object[,]
                {
                    { 1, "Insurance", null, "Bảo hiểm theo quy định của nhà nước.", true, "Bảo hiểm Xã hội (BHXH)", null, null, null, false, "Open" },
                    { 2, "Insurance", null, "Khám chữa bệnh BHYT.", true, "Bảo hiểm Y tế (BHYT)", null, null, null, false, "Open" },
                    { 3, "Health", 100, "Khám tổng quát tại bệnh viện Thu Cúc.", false, "Khám sức khỏe định kỳ 2026", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, true, "Open" },
                    { 4, "Allowance", null, "Phụ cấp 35k/ngày đi làm.", true, "Phụ cấp ăn trưa", null, null, null, false, "Open" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BenefitEnrollments");

            migrationBuilder.DropTable(
                name: "BenefitEntitlements");

            migrationBuilder.DropTable(
                name: "BenefitPrograms");
        }
    }
}
