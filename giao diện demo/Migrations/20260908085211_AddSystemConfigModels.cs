using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace giao_diện_demo.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemConfigModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SystemConfigs",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemConfigs", x => x.Key);
                });

            migrationBuilder.InsertData(
                table: "SystemConfigs",
                columns: new[] { "Key", "Category", "Description", "UpdatedAt", "UpdatedBy", "Value" },
                values: new object[,]
                {
                    { "Company.Address", "Company", "Địa chỉ trụ sở", new DateTime(2026, 9, 8, 15, 52, 10, 659, DateTimeKind.Local).AddTicks(1505), "System", "Tầng 12, Tòa nhà Tech, Hà Nội" },
                    { "Company.Email", "Company", "Email liên hệ", new DateTime(2026, 9, 8, 15, 52, 10, 659, DateTimeKind.Local).AddTicks(1501), "System", "contact@hrms.vn" },
                    { "Company.Name", "Company", "Tên công ty", new DateTime(2026, 9, 8, 15, 52, 10, 658, DateTimeKind.Local).AddTicks(6412), "System", "Công ty CP Công nghệ HRMS" },
                    { "Company.Phone", "Company", "Hotline", new DateTime(2026, 9, 8, 15, 52, 10, 659, DateTimeKind.Local).AddTicks(1503), "System", "1900 1000" },
                    { "Company.Representative", "Company", "Người đại diện", new DateTime(2026, 9, 8, 15, 52, 10, 659, DateTimeKind.Local).AddTicks(1506), "System", "Nguyễn Văn A" },
                    { "Company.ShortName", "Company", "Tên viết tắt", new DateTime(2026, 9, 8, 15, 52, 10, 659, DateTimeKind.Local).AddTicks(1489), "System", "HRMS Corp" },
                    { "Company.TaxCode", "Company", "Mã số thuế", new DateTime(2026, 9, 8, 15, 52, 10, 659, DateTimeKind.Local).AddTicks(1500), "System", "0101234567" },
                    { "Company.Website", "Company", "Website", new DateTime(2026, 9, 8, 15, 52, 10, 659, DateTimeKind.Local).AddTicks(1504), "System", "https://hrms.vn" },
                    { "Loc.Currency", "Localization", "Tiền tệ", new DateTime(2026, 9, 8, 15, 52, 10, 659, DateTimeKind.Local).AddTicks(1510), "System", "VND" },
                    { "Loc.Language", "Localization", "Ngôn ngữ mặc định", new DateTime(2026, 9, 8, 15, 52, 10, 659, DateTimeKind.Local).AddTicks(1507), "System", "vi-VN" },
                    { "Loc.Timezone", "Localization", "Múi giờ", new DateTime(2026, 9, 8, 15, 52, 10, 659, DateTimeKind.Local).AddTicks(1508), "System", "SE Asia Standard Time" },
                    { "Storage.AllowedTypes", "Storage", "Định dạng cho phép", new DateTime(2026, 9, 8, 15, 52, 10, 659, DateTimeKind.Local).AddTicks(1512), "System", ".jpg,.jpeg,.png,.pdf,.doc,.docx,.xls,.xlsx" },
                    { "Storage.MaxFileSizeMB", "Storage", "Dung lượng upload tối đa (MB)", new DateTime(2026, 9, 8, 15, 52, 10, 659, DateTimeKind.Local).AddTicks(1511), "System", "10" },
                    { "Sys.MaintenanceMode", "System", "Chế độ bảo trì", new DateTime(2026, 9, 8, 15, 52, 10, 659, DateTimeKind.Local).AddTicks(1514), "System", "false" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemConfigs");
        }
    }
}
