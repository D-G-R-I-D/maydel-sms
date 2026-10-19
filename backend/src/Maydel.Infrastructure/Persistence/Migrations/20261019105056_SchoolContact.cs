using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maydel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SchoolContact : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FacebookUrl",
                table: "SchoolProfile",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstagramUrl",
                table: "SchoolProfile",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MapUrl",
                table: "SchoolProfile",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfficeHours",
                table: "SchoolProfile",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsApp",
                table: "SchoolProfile",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FacebookUrl",
                table: "SchoolProfile");

            migrationBuilder.DropColumn(
                name: "InstagramUrl",
                table: "SchoolProfile");

            migrationBuilder.DropColumn(
                name: "MapUrl",
                table: "SchoolProfile");

            migrationBuilder.DropColumn(
                name: "OfficeHours",
                table: "SchoolProfile");

            migrationBuilder.DropColumn(
                name: "WhatsApp",
                table: "SchoolProfile");
        }
    }
}
