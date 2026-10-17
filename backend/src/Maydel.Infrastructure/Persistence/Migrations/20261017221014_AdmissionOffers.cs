using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maydel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdmissionOffers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ApplicationId",
                table: "OutboxEmails",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdmissionNumber",
                table: "Applications",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DataPurgedAt",
                table: "Applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DecidedAt",
                table: "Applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Decision",
                table: "Applications",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EnrolledAt",
                table: "Applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RespondedAt",
                table: "Applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applications_AdmissionNumber",
                table: "Applications",
                column: "AdmissionNumber",
                unique: true,
                filter: "\"AdmissionNumber\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Applications_AdmissionNumber",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "ApplicationId",
                table: "OutboxEmails");

            migrationBuilder.DropColumn(
                name: "AdmissionNumber",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "DataPurgedAt",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "DecidedAt",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "Decision",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "EnrolledAt",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "RespondedAt",
                table: "Applications");
        }
    }
}
