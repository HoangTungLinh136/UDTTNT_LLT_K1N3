using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using giao_dien_demo.Data;

#nullable disable

namespace giao_diện_demo.Migrations
{
    /// <inheritdoc />
    [Migration("20260917100000_Mgr02AllowHistoricalManagerAssignments")]
    [DbContext(typeof(ApplicationDbContext))]
    public partial class Mgr02AllowHistoricalManagerAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ManagerAssignments_UserId_OrgUnit_Unique",
                table: "ManagerAssignments");

            migrationBuilder.CreateIndex(
                name: "IX_ManagerAssignments_UserId_OrgUnit",
                table: "ManagerAssignments",
                columns: new[] { "UserId", "OrganizationalUnit" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ManagerAssignments_UserId_OrgUnit",
                table: "ManagerAssignments");

            migrationBuilder.CreateIndex(
                name: "IX_ManagerAssignments_UserId_OrgUnit_Unique",
                table: "ManagerAssignments",
                columns: new[] { "UserId", "OrganizationalUnit" },
                unique: true);
        }
    }
}
