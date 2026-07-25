using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyCommercialOperationsAndDirectories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "company_purchases",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Service");

            migrationBuilder.Sql("""
                UPDATE company_purchases
                SET "Type" = CASE
                    WHEN "Category" = 'Supplies' THEN 'GoodsInput'
                    WHEN "Category" = 'Equipment' THEN 'Asset'
                    WHEN "Category" = 'Other' THEN 'Other'
                    ELSE 'Service'
                END;
                UPDATE contacts SET "Type" = 'Client' WHERE "Type" = 'Customer';
                UPDATE contact_interactions SET "Type" = 'Note' WHERE "Type" = 'FollowUp';
                UPDATE appointments SET "Type" = 'ExecutiveMeeting' WHERE "Type" = 'SalesCall';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_company_purchases_Type_PurchaseDate",
                table: "company_purchases",
                columns: new[] { "Type", "PurchaseDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE contacts SET "Type" = 'Customer' WHERE "Type" = 'Client';
                UPDATE appointments SET "Type" = 'SalesCall' WHERE "Type" = 'ExecutiveMeeting';
                """);

            migrationBuilder.DropIndex(
                name: "IX_company_purchases_Type_PurchaseDate",
                table: "company_purchases");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "company_purchases");
        }
    }
}
