using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maydel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FollowUps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShowScoreOnSubmit",
                table: "CbtTests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "EntryTermId",
                table: "AdmissionSettings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionSettings_EntryTermId",
                table: "AdmissionSettings",
                column: "EntryTermId");

            migrationBuilder.AddForeignKey(
                name: "FK_AdmissionSettings_Terms_EntryTermId",
                table: "AdmissionSettings",
                column: "EntryTermId",
                principalTable: "Terms",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AdmissionSettings_Terms_EntryTermId",
                table: "AdmissionSettings");

            migrationBuilder.DropIndex(
                name: "IX_AdmissionSettings_EntryTermId",
                table: "AdmissionSettings");

            migrationBuilder.DropColumn(
                name: "ShowScoreOnSubmit",
                table: "CbtTests");

            migrationBuilder.DropColumn(
                name: "EntryTermId",
                table: "AdmissionSettings");
        }
    }
}
