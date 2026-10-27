using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace giao_diện_demo.Migrations
{
    /// <inheritdoc />
    public partial class P42_AlignModelToSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ManagerAssignments_Users_UserId",
                table: "ManagerAssignments");

            migrationBuilder.DropIndex(
                name: "IX_ManagerAssignments_UserId",
                table: "ManagerAssignments");

            migrationBuilder.AlterColumn<string>(
                name: "OrganizationalUnit",
                table: "ManagerAssignments",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateTable(
                name: "LeaderAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    OrganizationalUnit = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AssignedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaderAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaderAssignments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeaderAssignments_UserId_OrgUnit_Unique",
                table: "LeaderAssignments",
                columns: new[] { "UserId", "OrganizationalUnit" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ManagerAssignments_UserId_OrgUnit_Unique",
                table: "ManagerAssignments",
                columns: new[] { "UserId", "OrganizationalUnit" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ManagerAssignments_Users_UserId",
                table: "ManagerAssignments",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ManagerAssignments_Users_UserId",
                table: "ManagerAssignments");

            migrationBuilder.DropTable(
                name: "LeaderAssignments");

            migrationBuilder.DropIndex(
                name: "IX_ManagerAssignments_UserId_OrgUnit_Unique",
                table: "ManagerAssignments");

            migrationBuilder.AlterColumn<string>(
                name: "OrganizationalUnit",
                table: "ManagerAssignments",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);

            migrationBuilder.CreateIndex(
                name: "IX_ManagerAssignments_UserId",
                table: "ManagerAssignments",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ManagerAssignments_Users_UserId",
                table: "ManagerAssignments",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
