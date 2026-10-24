using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maydel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StaffClock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClockSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StartsAt = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndsAt = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    GraceMinutes = table.Column<int>(type: "integer", nullable: false),
                    RequireLocation = table.Column<bool>(type: "boolean", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    RadiusMeters = table.Column<int>(type: "integer", nullable: false),
                    CodeSecret = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    KioskTokenHash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClockSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StaffDays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    ClockInAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClockOutAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    InMethod = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    OutMethod = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    InLatitude = table.Column<double>(type: "double precision", nullable: true),
                    InLongitude = table.Column<double>(type: "double precision", nullable: true),
                    IsLate = table.Column<bool>(type: "boolean", nullable: false),
                    Note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffDays_Staff_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StaffDays_Date",
                table: "StaffDays",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_StaffDays_StaffId_Date",
                table: "StaffDays",
                columns: new[] { "StaffId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClockSettings");

            migrationBuilder.DropTable(
                name: "StaffDays");
        }
    }
}
