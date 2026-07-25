using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMscDirectManifestTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_manifest_tracking_connections_Provider_ExternalReference",
                table: "manifest_tracking_connections");

            migrationBuilder.AddColumn<string>(
                name: "ReferenceType",
                table: "manifest_tracking_connections",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "ContainerNumber");

            migrationBuilder.CreateIndex(
                name: "IX_manifest_tracking_connections_Provider_ExternalReference",
                table: "manifest_tracking_connections",
                columns: new[] { "Provider", "ExternalReference" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_manifest_tracking_connections_Provider_ExternalReference",
                table: "manifest_tracking_connections");

            migrationBuilder.DropColumn(
                name: "ReferenceType",
                table: "manifest_tracking_connections");

            migrationBuilder.CreateIndex(
                name: "IX_manifest_tracking_connections_Provider_ExternalReference",
                table: "manifest_tracking_connections",
                columns: new[] { "Provider", "ExternalReference" },
                unique: true);
        }
    }
}
