using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations;

/// <summary>
/// Records that retired test-era APIs are no longer part of the active model.
/// Physical tables are deliberately preserved so a deployment cannot destroy historical
/// company data without a separate export, retention decision and reviewed migration.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260725093000_RemoveRetiredTestOperationalApis")]
public partial class RemoveRetiredTestOperationalApis : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("SELECT 1;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("SELECT 1;");
    }
}
