using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace web.Migrations
{
    /// <inheritdoc />
    public partial class AddSmsMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SmsMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SeasonId = table.Column<int>(type: "INTEGER", nullable: false),
                    MessageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    VolunteerId = table.Column<int>(type: "INTEGER", nullable: false),
                    PhoneNumberSnapshot = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    MessageBody = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    SegmentCount = table.Column<int>(type: "INTEGER", nullable: false),
                    UnitPriceDkk = table.Column<decimal>(type: "TEXT", nullable: false),
                    TotalPriceDkk = table.Column<decimal>(type: "TEXT", nullable: false),
                    QueuedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SentByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SmsMessages_Volunteers_VolunteerId",
                        column: x => x.VolunteerId,
                        principalTable: "Volunteers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SmsMessages_CreatedAt",
                table: "SmsMessages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SmsMessages_MessageId",
                table: "SmsMessages",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SmsMessages_SeasonId_VolunteerId",
                table: "SmsMessages",
                columns: new[] { "SeasonId", "VolunteerId" });

            migrationBuilder.CreateIndex(
                name: "IX_SmsMessages_VolunteerId",
                table: "SmsMessages",
                column: "VolunteerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SmsMessages");
        }
    }
}
