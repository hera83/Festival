using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace web.Migrations
{
    /// <inheritdoc />
    public partial class AddVolunteerGpsLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VolunteerGpsLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VolunteerId = table.Column<int>(type: "INTEGER", nullable: false),
                    SeasonId = table.Column<int>(type: "INTEGER", nullable: false),
                    VolunteerKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    VolunteerName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Latitude = table.Column<double>(type: "REAL", nullable: false),
                    Longitude = table.Column<double>(type: "REAL", nullable: false),
                    Accuracy = table.Column<double>(type: "REAL", nullable: true),
                    Trigger = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    LoggedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VolunteerGpsLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VolunteerGpsLogs_Volunteers_VolunteerId",
                        column: x => x.VolunteerId,
                        principalTable: "Volunteers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerGpsLogs_LoggedAt",
                table: "VolunteerGpsLogs",
                column: "LoggedAt");

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerGpsLogs_SeasonId_VolunteerId",
                table: "VolunteerGpsLogs",
                columns: new[] { "SeasonId", "VolunteerId" });

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerGpsLogs_VolunteerId",
                table: "VolunteerGpsLogs",
                column: "VolunteerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VolunteerGpsLogs");
        }
    }
}
