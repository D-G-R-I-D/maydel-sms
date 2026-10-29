using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maydel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Homework : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Homework",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassRoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    SetByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SetByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Instructions = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    InstructionsHtml = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: false),
                    AcceptsSubmissions = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Homework", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Homework_ClassRooms_ClassRoomId",
                        column: x => x.ClassRoomId,
                        principalTable: "ClassRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Homework_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HomeworkFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    HomeworkId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeworkFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HomeworkFiles_Homework_HomeworkId",
                        column: x => x.HomeworkId,
                        principalTable: "Homework",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HomeworkSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HomeworkId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsLate = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Feedback = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeworkSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HomeworkSubmissions_Homework_HomeworkId",
                        column: x => x.HomeworkId,
                        principalTable: "Homework",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HomeworkSubmissions_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HomeworkSubmissionFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeworkSubmissionFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HomeworkSubmissionFiles_HomeworkSubmissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "HomeworkSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Homework_ClassRoomId_DueOn",
                table: "Homework",
                columns: new[] { "ClassRoomId", "DueOn" });

            migrationBuilder.CreateIndex(
                name: "IX_Homework_SubjectId",
                table: "Homework",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkFiles_HomeworkId",
                table: "HomeworkFiles",
                column: "HomeworkId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkSubmissionFiles_SubmissionId",
                table: "HomeworkSubmissionFiles",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkSubmissions_HomeworkId_StudentId",
                table: "HomeworkSubmissions",
                columns: new[] { "HomeworkId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkSubmissions_StudentId",
                table: "HomeworkSubmissions",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HomeworkFiles");

            migrationBuilder.DropTable(
                name: "HomeworkSubmissionFiles");

            migrationBuilder.DropTable(
                name: "HomeworkSubmissions");

            migrationBuilder.DropTable(
                name: "Homework");
        }
    }
}
