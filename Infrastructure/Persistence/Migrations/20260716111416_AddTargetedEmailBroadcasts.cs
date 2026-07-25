using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTargetedEmailBroadcasts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "email_broadcast_number_seq",
                minValue: 1L);

            migrationBuilder.CreateTable(
                name: "email_broadcasts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Subject = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Body = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    ReplyTo = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    Purpose = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AudienceSelectionJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduledFor = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    QueuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TotalRecipients = table.Column<int>(type: "integer", nullable: false),
                    PendingRecipients = table.Column<int>(type: "integer", nullable: false),
                    SentRecipients = table.Column<int>(type: "integer", nullable: false),
                    FailedRecipients = table.Column<int>(type: "integer", nullable: false),
                    SkippedRecipients = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_broadcasts", x => x.Id);
                    table.CheckConstraint("CK_email_broadcast_counts", "\"TotalRecipients\" >= 0 AND \"PendingRecipients\" >= 0 AND \"SentRecipients\" >= 0 AND \"FailedRecipients\" >= 0 AND \"SkippedRecipients\" >= 0");
                    table.ForeignKey(
                        name: "FK_email_broadcasts_user_accounts_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_email_broadcasts_user_accounts_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "email_broadcast_attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmailBroadcastId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Checksum = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_broadcast_attachments", x => x.Id);
                    table.CheckConstraint("CK_email_broadcast_attachment_size", "\"SizeBytes\" > 0 AND \"SizeBytes\" <= 20000000");
                    table.ForeignKey(
                        name: "FK_email_broadcast_attachments_email_broadcasts_EmailBroadcast~",
                        column: x => x.EmailBroadcastId,
                        principalTable: "email_broadcasts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "email_broadcast_recipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmailBroadcastId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ProviderReference = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_broadcast_recipients", x => x.Id);
                    table.CheckConstraint("CK_email_broadcast_recipient_attempts", "\"AttemptCount\" >= 0");
                    table.ForeignKey(
                        name: "FK_email_broadcast_recipients_email_broadcasts_EmailBroadcastId",
                        column: x => x.EmailBroadcastId,
                        principalTable: "email_broadcasts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_email_broadcast_attachments_EmailBroadcastId_FileName",
                table: "email_broadcast_attachments",
                columns: new[] { "EmailBroadcastId", "FileName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_email_broadcast_recipients_EmailBroadcastId_NormalizedEmail",
                table: "email_broadcast_recipients",
                columns: new[] { "EmailBroadcastId", "NormalizedEmail" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_email_broadcast_recipients_EmailBroadcastId_Status_CreatedAt",
                table: "email_broadcast_recipients",
                columns: new[] { "EmailBroadcastId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_email_broadcast_recipients_SourceType_SourceId",
                table: "email_broadcast_recipients",
                columns: new[] { "SourceType", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_email_broadcasts_CampaignNumber",
                table: "email_broadcasts",
                column: "CampaignNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_email_broadcasts_CancelledByUserId",
                table: "email_broadcasts",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_email_broadcasts_CreatedByUserId",
                table: "email_broadcasts",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_email_broadcasts_Purpose_CreatedAt",
                table: "email_broadcasts",
                columns: new[] { "Purpose", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_email_broadcasts_Status_ScheduledFor_CreatedAt",
                table: "email_broadcasts",
                columns: new[] { "Status", "ScheduledFor", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_broadcast_attachments");

            migrationBuilder.DropTable(
                name: "email_broadcast_recipients");

            migrationBuilder.DropTable(
                name: "email_broadcasts");

            migrationBuilder.DropSequence(
                name: "email_broadcast_number_seq");
        }
    }
}
