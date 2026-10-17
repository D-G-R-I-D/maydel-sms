using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maydel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EntranceExam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExamBookedAt",
                table: "Applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExamReminderSentAt",
                table: "Applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExamScore",
                table: "Applications",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExamSlotId",
                table: "Applications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MissedAt",
                table: "Applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ScoredAt",
                table: "Applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StageOneNotifiedAt",
                table: "Applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CutOffMark",
                table: "AdmissionSettings",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExamSlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    Venue = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Mode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExamSlots_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Applications_ExamSlotId",
                table: "Applications",
                column: "ExamSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_ExamSlots_SessionId_StartsAt",
                table: "ExamSlots",
                columns: new[] { "SessionId", "StartsAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Applications_ExamSlots_ExamSlotId",
                table: "Applications",
                column: "ExamSlotId",
                principalTable: "ExamSlots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Applications_ExamSlots_ExamSlotId",
                table: "Applications");

            migrationBuilder.DropTable(
                name: "ExamSlots");

            migrationBuilder.DropIndex(
                name: "IX_Applications_ExamSlotId",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "ExamBookedAt",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "ExamReminderSentAt",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "ExamScore",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "ExamSlotId",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "MissedAt",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "ScoredAt",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "StageOneNotifiedAt",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "CutOffMark",
                table: "AdmissionSettings");
        }
    }
}
