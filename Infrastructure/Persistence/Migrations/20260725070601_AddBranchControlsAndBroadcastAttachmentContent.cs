using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchControlsAndBroadcastAttachmentContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsWarehouseOpen",
                table: "branch_controls",
                newName: "IsReceivingEnabled");

            migrationBuilder.RenameColumn(
                name: "IsAcceptingBookings",
                table: "branch_controls",
                newName: "IsBookingEnabled");

            migrationBuilder.AddColumn<byte[]>(
                name: "Content",
                table: "email_broadcast_attachments",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Content",
                table: "email_broadcast_attachments");

            migrationBuilder.RenameColumn(
                name: "IsReceivingEnabled",
                table: "branch_controls",
                newName: "IsWarehouseOpen");

            migrationBuilder.RenameColumn(
                name: "IsBookingEnabled",
                table: "branch_controls",
                newName: "IsAcceptingBookings");
        }
    }
}
