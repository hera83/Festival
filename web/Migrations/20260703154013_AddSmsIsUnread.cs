using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace web.Migrations
{
    /// <inheritdoc />
    public partial class AddSmsIsUnread : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsUnread",
                table: "SmsMessages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            // Backfill eksisterende indgående rækker (før denne feature) til det nye,
            // ensartede "Modtaget"-status, og udled IsUnread fra den tidligere gemte
            // rå modem-status.
            migrationBuilder.Sql(
                "UPDATE SmsMessages SET IsUnread = 1 WHERE Direction = 0 AND Status LIKE '%UNREAD%';");
            migrationBuilder.Sql(
                "UPDATE SmsMessages SET Status = 'Modtaget' WHERE Direction = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsUnread",
                table: "SmsMessages");
        }
    }
}
