using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maydel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdmissionForm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Form_ChildMiddleName",
                table: "Applications",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Form_ChildSex",
                table: "Applications",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Form_Declaration",
                table: "Applications",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Form_EmergencyContactName",
                table: "Applications",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Form_EmergencyContactPhone",
                table: "Applications",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Form_FatherEmail",
                table: "Applications",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Form_FatherName",
                table: "Applications",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Form_FatherOccupation",
                table: "Applications",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Form_FatherPhone",
                table: "Applications",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Form_HomeAddress",
                table: "Applications",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Form_Lga",
                table: "Applications",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Form_MedicalNotes",
                table: "Applications",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Form_MotherEmail",
                table: "Applications",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Form_MotherName",
                table: "Applications",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Form_MotherOccupation",
                table: "Applications",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Form_MotherPhone",
                table: "Applications",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Form_PreviousClass",
                table: "Applications",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Form_PreviousSchool",
                table: "Applications",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Form_Religion",
                table: "Applications",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Form_SpecialNeeds",
                table: "Applications",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Form_StateOfOrigin",
                table: "Applications",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SubmittedAt",
                table: "Applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApplicationDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FilePath = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationDocuments_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationDocuments_ApplicationId",
                table: "ApplicationDocuments",
                column: "ApplicationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationDocuments");

            migrationBuilder.DropColumn(
                name: "Form_ChildMiddleName",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "Form_ChildSex",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "Form_Declaration",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "Form_EmergencyContactName",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "Form_EmergencyContactPhone",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "Form_FatherEmail",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "Form_FatherName",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "Form_FatherOccupation",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "Form_FatherPhone",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "Form_HomeAddress",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "Form_Lga",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "Form_MedicalNotes",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "Form_MotherEmail",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "Form_MotherName",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "Form_MotherOccupation",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "Form_MotherPhone",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "Form_PreviousClass",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "Form_PreviousSchool",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "Form_Religion",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "Form_SpecialNeeds",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "Form_StateOfOrigin",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "Applications");
        }
    }
}
