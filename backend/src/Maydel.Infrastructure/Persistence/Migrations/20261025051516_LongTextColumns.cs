using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maydel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LongTextColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "BodyText",
                table: "Newsletters",
                type: "character varying(100000)",
                maxLength: 100000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "BodyHtml",
                table: "Newsletters",
                type: "character varying(100000)",
                maxLength: 100000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "Text",
                table: "HomeworkSubmissions",
                type: "character varying(20000)",
                maxLength: 20000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "InstructionsHtml",
                table: "Homework",
                type: "character varying(100000)",
                maxLength: 100000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Instructions",
                table: "Homework",
                type: "character varying(20000)",
                maxLength: 20000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "DescriptionHtml",
                table: "Events",
                type: "character varying(100000)",
                maxLength: 100000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Events",
                type: "character varying(20000)",
                maxLength: 20000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BodyHtml",
                table: "Announcements",
                type: "character varying(100000)",
                maxLength: 100000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "BodyText",
                table: "Newsletters",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100000)",
                oldMaxLength: 100000);

            migrationBuilder.AlterColumn<string>(
                name: "BodyHtml",
                table: "Newsletters",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100000)",
                oldMaxLength: 100000);

            migrationBuilder.AlterColumn<string>(
                name: "Text",
                table: "HomeworkSubmissions",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20000)",
                oldMaxLength: 20000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "InstructionsHtml",
                table: "Homework",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100000)",
                oldMaxLength: 100000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Instructions",
                table: "Homework",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20000)",
                oldMaxLength: 20000);

            migrationBuilder.AlterColumn<string>(
                name: "DescriptionHtml",
                table: "Events",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100000)",
                oldMaxLength: 100000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Events",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20000)",
                oldMaxLength: 20000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BodyHtml",
                table: "Announcements",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100000)",
                oldMaxLength: 100000,
                oldNullable: true);
        }
    }
}
