using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace giao_diện_demo.Migrations
{
    /// <inheritdoc />
    public partial class AlignNullableOnboardingEmployee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The existing F1 database already permits unlinked onboarding records.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[OnboardingRecords]') AND name = N'EmployeeId' AND is_nullable = 0)
                    ALTER TABLE [OnboardingRecords] ALTER COLUMN [EmployeeId] int NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new System.NotSupportedException("Automatic rollback would invalidate existing unlinked onboarding records. Reconcile those records explicitly before changing nullability.");
        }
    }
}
