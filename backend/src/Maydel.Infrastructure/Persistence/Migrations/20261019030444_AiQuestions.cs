using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maydel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AiQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiAllowances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MonthlyGenerations = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiAllowances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiGenerations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassLevelId = table.Column<Guid>(type: "uuid", nullable: false),
                    TermId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssessmentType = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Kinds = table.Column<List<string>>(type: "text[]", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    Easy = table.Column<int>(type: "integer", nullable: false),
                    Medium = table.Column<int>(type: "integer", nullable: false),
                    Hard = table.Column<int>(type: "integer", nullable: false),
                    Guidance = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiGenerations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiGenerations_ClassLevels_ClassLevelId",
                        column: x => x.ClassLevelId,
                        principalTable: "ClassLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiGenerations_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AiSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultMonthlyGenerations = table.Column<int>(type: "integer", nullable: false),
                    SchoolMonthlyGenerations = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiSourceFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FilePath = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    GenerationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiSourceFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiSourceFiles_AiGenerations_GenerationId",
                        column: x => x.GenerationId,
                        principalTable: "AiGenerations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AiVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    QuestionsJson = table.Column<string>(type: "jsonb", maxLength: 256, nullable: false),
                    Model = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    InputTokens = table.Column<long>(type: "bigint", nullable: false),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: false),
                    GenerationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiVersions_AiGenerations_GenerationId",
                        column: x => x.GenerationId,
                        principalTable: "AiGenerations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiAllowances_UserId",
                table: "AiAllowances",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiGenerations_ClassLevelId",
                table: "AiGenerations",
                column: "ClassLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_AiGenerations_SubjectId",
                table: "AiGenerations",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AiGenerations_UserId_CreatedAt",
                table: "AiGenerations",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiSourceFiles_GenerationId",
                table: "AiSourceFiles",
                column: "GenerationId");

            migrationBuilder.CreateIndex(
                name: "IX_AiVersions_GenerationId",
                table: "AiVersions",
                column: "GenerationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiAllowances");

            migrationBuilder.DropTable(
                name: "AiSettings");

            migrationBuilder.DropTable(
                name: "AiSourceFiles");

            migrationBuilder.DropTable(
                name: "AiVersions");

            migrationBuilder.DropTable(
                name: "AiGenerations");
        }
    }
}
