using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Maydel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BehaviourTraits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BehaviourTraits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BehaviourTraits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReportClubs",
                columns: table => new
                {
                    TermReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Rating = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportClubs", x => new { x.TermReportId, x.Order });
                    table.ForeignKey(
                        name: "FK_ReportClubs_TermReports_TermReportId",
                        column: x => x.TermReportId,
                        principalTable: "TermReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TraitRatings",
                columns: table => new
                {
                    TermReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    TraitId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rating = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TraitRatings", x => new { x.TermReportId, x.TraitId });
                    table.ForeignKey(
                        name: "FK_TraitRatings_BehaviourTraits_TraitId",
                        column: x => x.TraitId,
                        principalTable: "BehaviourTraits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TraitRatings_TermReports_TermReportId",
                        column: x => x.TermReportId,
                        principalTable: "TermReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TraitRatings_TraitId",
                table: "TraitRatings",
                column: "TraitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReportClubs");

            migrationBuilder.DropTable(
                name: "TraitRatings");

            migrationBuilder.DropTable(
                name: "BehaviourTraits");
        }
    }
}
