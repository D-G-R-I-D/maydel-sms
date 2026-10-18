using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maydel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class QuestionBank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Questions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassLevelId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ImagePath = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TrueFalseAnswer = table.Column<bool>(type: "boolean", nullable: true),
                    AcceptedAnswers = table.Column<List<string>>(type: "text[]", nullable: false),
                    MarkingGuide = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Topic = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Difficulty = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Source = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Questions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Questions_ClassLevels_ClassLevelId",
                        column: x => x.ClassLevelId,
                        principalTable: "ClassLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Questions_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuestionOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionOptions_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuestionOptions_QuestionId",
                table: "QuestionOptions",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_Questions_ClassLevelId",
                table: "Questions",
                column: "ClassLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_Questions_SubjectId_ClassLevelId_IsArchived",
                table: "Questions",
                columns: new[] { "SubjectId", "ClassLevelId", "IsArchived" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuestionOptions");

            migrationBuilder.DropTable(
                name: "Questions");
        }
    }
}
