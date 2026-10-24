using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maydel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Newsletters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "NewsletterId",
                table: "OutboxEmails",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnsubscribeToken",
                table: "OutboxEmails",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfirmTokenHash",
                table: "NewsletterSubscribers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ConfirmedAt",
                table: "NewsletterSubscribers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NewsletterOptOuts",
                columns: table => new
                {
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsletterOptOuts", x => x.Email);
                });

            migrationBuilder.CreateTable(
                name: "Newsletters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Preheader = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BodyHtml = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    BodyText = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Audience = table.Column<int>(type: "integer", nullable: false),
                    Section = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ClassRoomId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ScheduledFor = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Recipients = table.Column<int>(type: "integer", nullable: false),
                    PublishOnWebsite = table.Column<bool>(type: "boolean", nullable: false),
                    Slug = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Newsletters", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxEmails_NewsletterId",
                table: "OutboxEmails",
                column: "NewsletterId",
                filter: "\"NewsletterId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxEmails_UnsubscribeToken",
                table: "OutboxEmails",
                column: "UnsubscribeToken",
                filter: "\"UnsubscribeToken\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterSubscribers_ConfirmTokenHash",
                table: "NewsletterSubscribers",
                column: "ConfirmTokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_Newsletters_Slug",
                table: "Newsletters",
                column: "Slug");

            migrationBuilder.CreateIndex(
                name: "IX_Newsletters_Status_ScheduledFor",
                table: "Newsletters",
                columns: new[] { "Status", "ScheduledFor" });

            // Earlier sign-ups gave their consent before double opt-in existed: treat the active ones as confirmed.
            migrationBuilder.Sql("UPDATE \"NewsletterSubscribers\" SET \"ConfirmedAt\" = \"CreatedAt\" WHERE \"IsActive\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NewsletterOptOuts");

            migrationBuilder.DropTable(
                name: "Newsletters");

            migrationBuilder.DropIndex(
                name: "IX_OutboxEmails_NewsletterId",
                table: "OutboxEmails");

            migrationBuilder.DropIndex(
                name: "IX_OutboxEmails_UnsubscribeToken",
                table: "OutboxEmails");

            migrationBuilder.DropIndex(
                name: "IX_NewsletterSubscribers_ConfirmTokenHash",
                table: "NewsletterSubscribers");

            migrationBuilder.DropColumn(
                name: "NewsletterId",
                table: "OutboxEmails");

            migrationBuilder.DropColumn(
                name: "UnsubscribeToken",
                table: "OutboxEmails");

            migrationBuilder.DropColumn(
                name: "ConfirmTokenHash",
                table: "NewsletterSubscribers");

            migrationBuilder.DropColumn(
                name: "ConfirmedAt",
                table: "NewsletterSubscribers");
        }
    }
}
