using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Maydel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FeesAndInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_StudentId_TermId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_TermId",
                table: "Invoices");

            migrationBuilder.AlterColumn<string>(
                name: "Number",
                table: "Invoices",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AddColumn<Guid>(
                name: "ClassRoomId",
                table: "Invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "InvoiceLines",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "InvoiceLines",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "FeeItems",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "FeeItems",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AddColumn<bool>(
                name: "SiblingDiscount",
                table: "FeeItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "FeeOptIns",
                columns: table => new
                {
                    FeeItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeOptIns", x => new { x.FeeItemId, x.StudentId });
                    table.ForeignKey(
                        name: "FK_FeeOptIns_FeeItems_FeeItemId",
                        column: x => x.FeeItemId,
                        principalTable: "FeeItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FeeOptIns_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FeeSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SiblingMode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SecondChild = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    ThirdAndLater = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InstalmentPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TermId = table.Column<Guid>(type: "uuid", nullable: true),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstalmentPlans", x => x.Id);
                    table.CheckConstraint("CK_InstalmentPlans_Scope", "(\"TermId\" IS NULL) <> (\"SessionId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_InstalmentPlans_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InstalmentPlans_Terms_TermId",
                        column: x => x.TermId,
                        principalTable: "Terms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Instalments",
                columns: table => new
                {
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Instalments", x => new { x.PlanId, x.Order });
                    table.ForeignKey(
                        name: "FK_Instalments_InstalmentPlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "InstalmentPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_StudentId_TermId",
                table: "Invoices",
                columns: new[] { "StudentId", "TermId" },
                unique: true,
                filter: "\"IsCancelled\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_TermId_ClassRoomId",
                table: "Invoices",
                columns: new[] { "TermId", "ClassRoomId" });

            migrationBuilder.CreateIndex(
                name: "IX_FeeOptIns_StudentId",
                table: "FeeOptIns",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_InstalmentPlans_SessionId",
                table: "InstalmentPlans",
                column: "SessionId",
                unique: true,
                filter: "\"SessionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InstalmentPlans_TermId",
                table: "InstalmentPlans",
                column: "TermId",
                unique: true,
                filter: "\"TermId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeeOptIns");

            migrationBuilder.DropTable(
                name: "FeeSettings");

            migrationBuilder.DropTable(
                name: "Instalments");

            migrationBuilder.DropTable(
                name: "InstalmentPlans");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_StudentId_TermId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_TermId_ClassRoomId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ClassRoomId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SiblingDiscount",
                table: "FeeItems");

            migrationBuilder.AlterColumn<string>(
                name: "Number",
                table: "Invoices",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "InvoiceLines",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "InvoiceLines",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(14,2)",
                oldPrecision: 14,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "FeeItems",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "FeeItems",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(14,2)",
                oldPrecision: 14,
                oldScale: 2);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_StudentId_TermId",
                table: "Invoices",
                columns: new[] { "StudentId", "TermId" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_TermId",
                table: "Invoices",
                column: "TermId");
        }
    }
}
