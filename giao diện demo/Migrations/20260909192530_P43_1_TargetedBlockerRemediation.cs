using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace giao_diện_demo.Migrations
{
    /// <inheritdoc />
    public partial class P43_1_TargetedBlockerRemediation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Proposals",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeduplicationKey",
                table: "Notifications",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecipientUserId",
                table: "Notifications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SenderName",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SenderUserId",
                table: "Notifications",
                type: "int",
                nullable: true);

            // Preserve legacy notification rows while moving ownership from display text
            // to the canonical User foreign key. Abort instead of deleting or guessing if
            // any historical recipient cannot be mapped unambiguously enough to a User.
            migrationBuilder.Sql(
                @"UPDATE notification
                  SET RecipientUserId = recipient.Id
                  FROM Notifications AS notification
                  CROSS APPLY
                  (
                      SELECT TOP (1) appUser.Id
                      FROM Users AS appUser
                      LEFT JOIN Employees AS employee ON employee.Id = appUser.EmployeeId
                      WHERE appUser.Username = notification.NguoiNhan
                         OR appUser.FullName = notification.NguoiNhan
                         OR employee.Name = notification.NguoiNhan
                      ORDER BY CASE
                          WHEN appUser.Username = notification.NguoiNhan THEN 0
                          WHEN appUser.FullName = notification.NguoiNhan THEN 1
                          ELSE 2
                      END, appUser.Id
                  ) AS recipient;

                  IF EXISTS (SELECT 1 FROM Notifications WHERE RecipientUserId IS NULL)
                      THROW 51000, 'P43.1 cannot map one or more legacy notification recipients to Users.', 1;");

            migrationBuilder.AlterColumn<int>(
                name: "RecipientUserId",
                table: "Notifications",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_DeduplicationKey",
                table: "Notifications",
                column: "DeduplicationKey",
                unique: true,
                filter: "[DeduplicationKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientUserId",
                table: "Notifications",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_SenderUserId",
                table: "Notifications",
                column: "SenderUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Users_RecipientUserId",
                table: "Notifications",
                column: "RecipientUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Users_SenderUserId",
                table: "Notifications",
                column: "SenderUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Users_RecipientUserId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Users_SenderUserId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_DeduplicationKey",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_RecipientUserId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_SenderUserId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Proposals");

            migrationBuilder.DropColumn(
                name: "DeduplicationKey",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "RecipientUserId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "SenderName",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "SenderUserId",
                table: "Notifications");
        }
    }
}
