using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSecureAttendanceKiosk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_attendance_records_staff_profiles_StaffProfileId",
                table: "attendance_records");

            migrationBuilder.AddColumn<int>(
                name: "AttendancePinFailedAttempts",
                table: "staff_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AttendancePinHash",
                table: "staff_profiles",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AttendancePinLockedUntil",
                table: "staff_profiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AttendancePinUpdatedAt",
                table: "staff_profiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AccuracyMeters",
                table: "attendance_records",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AttendanceSessionId",
                table: "attendance_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AttendanceStationId",
                table: "attendance_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClientCapturedAt",
                table: "attendance_records",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceId",
                table: "attendance_records",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "DistanceFromStationMeters",
                table: "attendance_records",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EmailVerified",
                table: "attendance_records",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "GeofenceVerified",
                table: "attendance_records",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "IpAddress",
                table: "attendance_records",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsSuspicious",
                table: "attendance_records",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "Latitude",
                table: "attendance_records",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Longitude",
                table: "attendance_records",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PinVerified",
                table: "attendance_records",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "attendance_records",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "StationCodeVerified",
                table: "attendance_records",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "VerificationNotes",
                table: "attendance_records",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                "UPDATE attendance_records SET \"Source\" = 'AuthenticatedApp', " +
                "\"VerificationNotes\" = 'Legacy authenticated attendance record created before secure kiosk verification.' " +
                "WHERE \"Source\" = ''");

            migrationBuilder.CreateTable(
                name: "attendance_stations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    Longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    AllowedRadiusMeters = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    MaximumAccuracyMeters = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    AllowSharedDevice = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SecretHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SecretVersion = table.Column<int>(type: "integer", nullable: false),
                    SecretRotatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendance_stations", x => x.Id);
                    table.CheckConstraint("CK_attendance_stations_accuracy", "\"MaximumAccuracyMeters\" BETWEEN 5 AND 500");
                    table.CheckConstraint("CK_attendance_stations_latitude", "\"Latitude\" BETWEEN -90 AND 90");
                    table.CheckConstraint("CK_attendance_stations_longitude", "\"Longitude\" BETWEEN -180 AND 180");
                    table.CheckConstraint("CK_attendance_stations_radius", "\"AllowedRadiusMeters\" BETWEEN 20 AND 5000");
                    table.ForeignKey(
                        name: "FK_attendance_stations_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attendance_kiosk_challenges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttendanceStationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DeviceId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    Longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    AccuracyMeters = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    DistanceFromStationMeters = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    GeofenceVerified = table.Column<bool>(type: "boolean", nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendance_kiosk_challenges", x => x.Id);
                    table.CheckConstraint("CK_attendance_kiosk_challenges_accuracy", "\"AccuracyMeters\" BETWEEN 0 AND 100000");
                    table.CheckConstraint("CK_attendance_kiosk_challenges_distance", "\"DistanceFromStationMeters\" >= 0");
                    table.CheckConstraint("CK_attendance_kiosk_challenges_latitude", "\"Latitude\" BETWEEN -90 AND 90");
                    table.CheckConstraint("CK_attendance_kiosk_challenges_longitude", "\"Longitude\" BETWEEN -180 AND 180");
                    table.ForeignKey(
                        name: "FK_attendance_kiosk_challenges_attendance_stations_AttendanceS~",
                        column: x => x.AttendanceStationId,
                        principalTable: "attendance_stations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "attendance_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClockInStationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClockOutStationId = table.Column<Guid>(type: "uuid", nullable: true),
                    BranchCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ClockInAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClockOutAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WorkedMinutes = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IsFlagged = table.Column<bool>(type: "boolean", nullable: false),
                    FlagReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendance_sessions", x => x.Id);
                    table.CheckConstraint("CK_attendance_sessions_status", "\"Status\" IN ('Open','Completed','Corrected','Cancelled','RequiresReview')");
                    table.CheckConstraint("CK_attendance_sessions_times", "\"ClockOutAt\" IS NULL OR \"ClockOutAt\" >= \"ClockInAt\"");
                    table.CheckConstraint("CK_attendance_sessions_worked_minutes", "\"WorkedMinutes\" IS NULL OR \"WorkedMinutes\" >= 0");
                    table.ForeignKey(
                        name: "FK_attendance_sessions_attendance_stations_ClockInStationId",
                        column: x => x.ClockInStationId,
                        principalTable: "attendance_stations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_attendance_sessions_attendance_stations_ClockOutStationId",
                        column: x => x.ClockOutStationId,
                        principalTable: "attendance_stations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_attendance_sessions_staff_profiles_StaffProfileId",
                        column: x => x.StaffProfileId,
                        principalTable: "staff_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_attendance_sessions_user_accounts_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_attendance_records_AttendanceSessionId",
                table: "attendance_records",
                column: "AttendanceSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_records_AttendanceStationId_OccurredAt",
                table: "attendance_records",
                columns: new[] { "AttendanceStationId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_attendance_records_DeviceId_OccurredAt",
                table: "attendance_records",
                columns: new[] { "DeviceId", "OccurredAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_attendance_records_accuracy",
                table: "attendance_records",
                sql: "\"AccuracyMeters\" IS NULL OR \"AccuracyMeters\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_attendance_records_distance",
                table: "attendance_records",
                sql: "\"DistanceFromStationMeters\" IS NULL OR \"DistanceFromStationMeters\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_attendance_records_latitude",
                table: "attendance_records",
                sql: "\"Latitude\" IS NULL OR \"Latitude\" BETWEEN -90 AND 90");

            migrationBuilder.AddCheckConstraint(
                name: "CK_attendance_records_longitude",
                table: "attendance_records",
                sql: "\"Longitude\" IS NULL OR \"Longitude\" BETWEEN -180 AND 180");

            migrationBuilder.AddCheckConstraint(
                name: "CK_attendance_records_source",
                table: "attendance_records",
                sql: "\"Source\" IN ('SecureBranchKiosk','AuthenticatedApp','AdministratorCorrection')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_attendance_records_type",
                table: "attendance_records",
                sql: "\"Type\" IN ('ClockIn','ClockOut','BreakStart','BreakEnd','RemoteWork')");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_kiosk_challenges_AttendanceStationId_ExpiresAt_U~",
                table: "attendance_kiosk_challenges",
                columns: new[] { "AttendanceStationId", "ExpiresAt", "UsedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_attendance_kiosk_challenges_TokenHash",
                table: "attendance_kiosk_challenges",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_attendance_sessions_BranchCode_ClockInAt",
                table: "attendance_sessions",
                columns: new[] { "BranchCode", "ClockInAt" });

            migrationBuilder.CreateIndex(
                name: "IX_attendance_sessions_ClockInStationId",
                table: "attendance_sessions",
                column: "ClockInStationId");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_sessions_ClockOutStationId",
                table: "attendance_sessions",
                column: "ClockOutStationId");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_sessions_ReviewedByUserId",
                table: "attendance_sessions",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_sessions_StaffProfileId",
                table: "attendance_sessions",
                column: "StaffProfileId",
                unique: true,
                filter: "\"Status\" = 'Open'");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_sessions_StaffProfileId_ClockInAt",
                table: "attendance_sessions",
                columns: new[] { "StaffProfileId", "ClockInAt" });

            migrationBuilder.CreateIndex(
                name: "IX_attendance_stations_BranchId_IsActive",
                table: "attendance_stations",
                columns: new[] { "BranchId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_attendance_stations_Code",
                table: "attendance_stations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_attendance_stations_PublicId",
                table: "attendance_stations",
                column: "PublicId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_attendance_records_attendance_sessions_AttendanceSessionId",
                table: "attendance_records",
                column: "AttendanceSessionId",
                principalTable: "attendance_sessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_attendance_records_attendance_stations_AttendanceStationId",
                table: "attendance_records",
                column: "AttendanceStationId",
                principalTable: "attendance_stations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_attendance_records_staff_profiles_StaffProfileId",
                table: "attendance_records",
                column: "StaffProfileId",
                principalTable: "staff_profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_attendance_records_attendance_sessions_AttendanceSessionId",
                table: "attendance_records");

            migrationBuilder.DropForeignKey(
                name: "FK_attendance_records_attendance_stations_AttendanceStationId",
                table: "attendance_records");

            migrationBuilder.DropForeignKey(
                name: "FK_attendance_records_staff_profiles_StaffProfileId",
                table: "attendance_records");

            migrationBuilder.DropTable(
                name: "attendance_kiosk_challenges");

            migrationBuilder.DropTable(
                name: "attendance_sessions");

            migrationBuilder.DropTable(
                name: "attendance_stations");

            migrationBuilder.DropIndex(
                name: "IX_attendance_records_AttendanceSessionId",
                table: "attendance_records");

            migrationBuilder.DropIndex(
                name: "IX_attendance_records_AttendanceStationId_OccurredAt",
                table: "attendance_records");

            migrationBuilder.DropIndex(
                name: "IX_attendance_records_DeviceId_OccurredAt",
                table: "attendance_records");

            migrationBuilder.DropCheckConstraint(
                name: "CK_attendance_records_accuracy",
                table: "attendance_records");

            migrationBuilder.DropCheckConstraint(
                name: "CK_attendance_records_distance",
                table: "attendance_records");

            migrationBuilder.DropCheckConstraint(
                name: "CK_attendance_records_latitude",
                table: "attendance_records");

            migrationBuilder.DropCheckConstraint(
                name: "CK_attendance_records_longitude",
                table: "attendance_records");

            migrationBuilder.DropCheckConstraint(
                name: "CK_attendance_records_source",
                table: "attendance_records");

            migrationBuilder.DropCheckConstraint(
                name: "CK_attendance_records_type",
                table: "attendance_records");

            migrationBuilder.DropColumn(
                name: "AttendancePinFailedAttempts",
                table: "staff_profiles");

            migrationBuilder.DropColumn(
                name: "AttendancePinHash",
                table: "staff_profiles");

            migrationBuilder.DropColumn(
                name: "AttendancePinLockedUntil",
                table: "staff_profiles");

            migrationBuilder.DropColumn(
                name: "AttendancePinUpdatedAt",
                table: "staff_profiles");

            migrationBuilder.DropColumn(
                name: "AccuracyMeters",
                table: "attendance_records");

            migrationBuilder.DropColumn(
                name: "AttendanceSessionId",
                table: "attendance_records");

            migrationBuilder.DropColumn(
                name: "AttendanceStationId",
                table: "attendance_records");

            migrationBuilder.DropColumn(
                name: "ClientCapturedAt",
                table: "attendance_records");

            migrationBuilder.DropColumn(
                name: "DeviceId",
                table: "attendance_records");

            migrationBuilder.DropColumn(
                name: "DistanceFromStationMeters",
                table: "attendance_records");

            migrationBuilder.DropColumn(
                name: "EmailVerified",
                table: "attendance_records");

            migrationBuilder.DropColumn(
                name: "GeofenceVerified",
                table: "attendance_records");

            migrationBuilder.DropColumn(
                name: "IpAddress",
                table: "attendance_records");

            migrationBuilder.DropColumn(
                name: "IsSuspicious",
                table: "attendance_records");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "attendance_records");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "attendance_records");

            migrationBuilder.DropColumn(
                name: "PinVerified",
                table: "attendance_records");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "attendance_records");

            migrationBuilder.DropColumn(
                name: "StationCodeVerified",
                table: "attendance_records");

            migrationBuilder.DropColumn(
                name: "VerificationNotes",
                table: "attendance_records");

            migrationBuilder.AddForeignKey(
                name: "FK_attendance_records_staff_profiles_StaffProfileId",
                table: "attendance_records",
                column: "StaffProfileId",
                principalTable: "staff_profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
