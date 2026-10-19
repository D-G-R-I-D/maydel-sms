using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maydel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CbtMarking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MarkedAt",
                table: "TestAttemptItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MarkedByUserId",
                table: "TestAttemptItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeacherComment",
                table: "TestAttemptItems",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TeacherScore",
                table: "TestAttemptItems",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "GradebookPushedAt",
                table: "CbtTests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ResultsReleasedAt",
                table: "CbtTests",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MarkedAt",
                table: "TestAttemptItems");

            migrationBuilder.DropColumn(
                name: "MarkedByUserId",
                table: "TestAttemptItems");

            migrationBuilder.DropColumn(
                name: "TeacherComment",
                table: "TestAttemptItems");

            migrationBuilder.DropColumn(
                name: "TeacherScore",
                table: "TestAttemptItems");

            migrationBuilder.DropColumn(
                name: "GradebookPushedAt",
                table: "CbtTests");

            migrationBuilder.DropColumn(
                name: "ResultsReleasedAt",
                table: "CbtTests");
        }
    }
}
