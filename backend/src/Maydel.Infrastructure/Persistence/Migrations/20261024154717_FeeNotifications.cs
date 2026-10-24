using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maydel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FeeNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "LastReminderOn",
                table: "Invoices",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReminderIntervalDays",
                table: "FeeSettings",
                type: "integer",
                nullable: false,
                defaultValue: 7);

            migrationBuilder.AddColumn<bool>(
                name: "RemindersEnabled",
                table: "FeeSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    LinkUrl = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: true),
                    DedupeKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_CreatedAt",
                table: "Notifications",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_DedupeKey",
                table: "Notifications",
                columns: new[] { "UserId", "DedupeKey" },
                unique: true,
                filter: "\"DedupeKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropColumn(
                name: "LastReminderOn",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ReminderIntervalDays",
                table: "FeeSettings");

            migrationBuilder.DropColumn(
                name: "RemindersEnabled",
                table: "FeeSettings");
        }
    }
}
