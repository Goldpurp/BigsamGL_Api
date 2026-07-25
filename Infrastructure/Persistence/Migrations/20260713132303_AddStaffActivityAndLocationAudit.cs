using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffActivityAndLocationAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "staff_location_pings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    StaffProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    StaffName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    EmployeeNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BranchCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    Longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    AccuracyMeters = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ConsentConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DeviceId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_location_pings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_staff_location_pings_staff_profiles_StaffProfileId",
                        column: x => x.StaffProfileId,
                        principalTable: "staff_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_staff_location_pings_user_accounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "staff_activity_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    StaffProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    StaffName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    StaffEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    Role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EmployeeNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BranchCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ActivityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    HttpMethod = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    RouteTemplate = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    QueryString = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Controller = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ActionName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    StatusCode = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DurationMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserAgent = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    DeviceId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SessionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LocationPingId = table.Column<Guid>(type: "uuid", nullable: true),
                    LocationSource = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    LocationFreshness = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    LocationAccuracyMeters = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    LocationCapturedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_activity_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_staff_activity_logs_staff_location_pings_LocationPingId",
                        column: x => x.LocationPingId,
                        principalTable: "staff_location_pings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_staff_activity_logs_staff_profiles_StaffProfileId",
                        column: x => x.StaffProfileId,
                        principalTable: "staff_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_staff_activity_logs_user_accounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_staff_activity_logs_BranchCode_OccurredAt",
                table: "staff_activity_logs",
                columns: new[] { "BranchCode", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_activity_logs_CorrelationId",
                table: "staff_activity_logs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_staff_activity_logs_LocationFreshness_OccurredAt",
                table: "staff_activity_logs",
                columns: new[] { "LocationFreshness", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_activity_logs_LocationPingId",
                table: "staff_activity_logs",
                column: "LocationPingId");

            migrationBuilder.CreateIndex(
                name: "IX_staff_activity_logs_Outcome_OccurredAt",
                table: "staff_activity_logs",
                columns: new[] { "Outcome", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_activity_logs_RouteTemplate_OccurredAt",
                table: "staff_activity_logs",
                columns: new[] { "RouteTemplate", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_activity_logs_StaffProfileId",
                table: "staff_activity_logs",
                column: "StaffProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_staff_activity_logs_UserAccountId_OccurredAt",
                table: "staff_activity_logs",
                columns: new[] { "UserAccountId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_location_pings_BranchCode_CapturedAt",
                table: "staff_location_pings",
                columns: new[] { "BranchCode", "CapturedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_location_pings_StaffProfileId",
                table: "staff_location_pings",
                column: "StaffProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_staff_location_pings_UserAccountId_CapturedAt",
                table: "staff_location_pings",
                columns: new[] { "UserAccountId", "CapturedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "staff_activity_logs");

            migrationBuilder.DropTable(
                name: "staff_location_pings");
        }
    }
}
