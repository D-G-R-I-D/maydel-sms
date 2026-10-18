using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maydel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CbtTests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CbtTests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Instructions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassLevelId = table.Column<Guid>(type: "uuid", nullable: false),
                    TermId = table.Column<Guid>(type: "uuid", nullable: false),
                    ComponentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClassRoomIds = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    OpensAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosesAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    ShuffleQuestions = table.Column<bool>(type: "boolean", nullable: false),
                    ShuffleOptions = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CbtTests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CbtTests_AssessmentComponents_ComponentId",
                        column: x => x.ComponentId,
                        principalTable: "AssessmentComponents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CbtTests_ClassLevels_ClassLevelId",
                        column: x => x.ClassLevelId,
                        principalTable: "ClassLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CbtTests_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CbtTests_Terms_TermId",
                        column: x => x.TermId,
                        principalTable: "Terms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CbtTestSections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Instructions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    MarksPerQuestion = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    Mode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    QuestionIds = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    RandomCount = table.Column<int>(type: "integer", nullable: true),
                    Topic = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Difficulty = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    TestId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CbtTestSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CbtTestSections_CbtTests_TestId",
                        column: x => x.TestId,
                        principalTable: "CbtTests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CbtTests_ClassLevelId",
                table: "CbtTests",
                column: "ClassLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_CbtTests_ComponentId",
                table: "CbtTests",
                column: "ComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_CbtTests_SubjectId",
                table: "CbtTests",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_CbtTests_TermId_SubjectId_ClassLevelId",
                table: "CbtTests",
                columns: new[] { "TermId", "SubjectId", "ClassLevelId" });

            migrationBuilder.CreateIndex(
                name: "IX_CbtTestSections_TestId",
                table: "CbtTestSections",
                column: "TestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CbtTestSections");

            migrationBuilder.DropTable(
                name: "CbtTests");
        }
    }
}
