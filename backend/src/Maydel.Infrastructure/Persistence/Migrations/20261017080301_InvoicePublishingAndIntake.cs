using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maydel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InvoicePublishingAndIntake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "InvoiceId",
                table: "OutboxEmails",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NewIntake",
                table: "Invoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PublishedAt",
                table: "Invoices",
                type: "timestamp with time zone",
                nullable: true);

            // Invoices that existed before drafts were introduced were already visible to parents: keep them published.
            migrationBuilder.Sql("UPDATE \"Invoices\" SET \"PublishedAt\" = \"CreatedAt\" WHERE \"PublishedAt\" IS NULL;");

            migrationBuilder.AddColumn<string>(
                name: "Audience",
                table: "FeeItems",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "ChargedByDefault",
                table: "FeeItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InvoiceId",
                table: "OutboxEmails");

            migrationBuilder.DropColumn(
                name: "NewIntake",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "Audience",
                table: "FeeItems");

            migrationBuilder.DropColumn(
                name: "ChargedByDefault",
                table: "FeeItems");
        }
    }
}
