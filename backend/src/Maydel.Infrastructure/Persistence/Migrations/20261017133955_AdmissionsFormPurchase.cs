using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maydel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdmissionsFormPurchase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Applications_Status",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "ChildAge",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "ChildName",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "ClassInterested",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "Message",
                table: "Applications");

            migrationBuilder.AlterColumn<string>(
                name: "ReferenceNumber",
                table: "Applications",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "Applications",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "ParentName",
                table: "Applications",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Applications",
                type: "character varying(254)",
                maxLength: 254,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApplicantUserId",
                table: "Applications",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateOnly>(
                name: "ChildDateOfBirth",
                table: "Applications",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChildFirstName",
                table: "Applications",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ChildLastName",
                table: "Applications",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ClassLevelId",
                table: "Applications",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateOnly>(
                name: "ExamWindowEndsOn",
                table: "Applications",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FormCode",
                table: "Applications",
                type: "character varying(9)",
                maxLength: 9,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FormFee",
                table: "Applications",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FormPaidAt",
                table: "Applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FormPaymentMethod",
                table: "Applications",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                table: "Applications",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "AdmissionSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsOpen = table.Column<bool>(type: "boolean", nullable: false),
                    FormFee = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    ExamWindowDays = table.Column<int>(type: "integer", nullable: false),
                    Instructions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmissionSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdmissionSettings_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FormCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    IssuedTo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    IssuedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UsedByApplicationId = table.Column<Guid>(type: "uuid", nullable: true),
                    UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FormPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Message = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormPayments_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Applications_ApplicantUserId",
                table: "Applications",
                column: "ApplicantUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_ClassLevelId",
                table: "Applications",
                column: "ClassLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_SessionId_Status",
                table: "Applications",
                columns: new[] { "SessionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionSettings_SessionId",
                table: "AdmissionSettings",
                column: "SessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormCodes_Code",
                table: "FormCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormCodes_SessionId",
                table: "FormCodes",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_FormPayments_ApplicationId",
                table: "FormPayments",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_FormPayments_Reference",
                table: "FormPayments",
                column: "Reference",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Applications_ClassLevels_ClassLevelId",
                table: "Applications",
                column: "ClassLevelId",
                principalTable: "ClassLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Applications_Sessions_SessionId",
                table: "Applications",
                column: "SessionId",
                principalTable: "Sessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Applications_ClassLevels_ClassLevelId",
                table: "Applications");

            migrationBuilder.DropForeignKey(
                name: "FK_Applications_Sessions_SessionId",
                table: "Applications");

            migrationBuilder.DropTable(
                name: "AdmissionSettings");

            migrationBuilder.DropTable(
                name: "FormCodes");

            migrationBuilder.DropTable(
                name: "FormPayments");

            migrationBuilder.DropIndex(
                name: "IX_Applications_ApplicantUserId",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_ClassLevelId",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_SessionId_Status",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "ApplicantUserId",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "ChildDateOfBirth",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "ChildFirstName",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "ChildLastName",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "ClassLevelId",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "ExamWindowEndsOn",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "FormCode",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "FormFee",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "FormPaidAt",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "FormPaymentMethod",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "Applications");

            migrationBuilder.AlterColumn<string>(
                name: "ReferenceNumber",
                table: "Applications",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "Applications",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "ParentName",
                table: "Applications",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Applications",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(254)",
                oldMaxLength: 254);

            migrationBuilder.AddColumn<int>(
                name: "ChildAge",
                table: "Applications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ChildName",
                table: "Applications",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ClassInterested",
                table: "Applications",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Message",
                table: "Applications",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applications_Status",
                table: "Applications",
                column: "Status");
        }
    }
}
