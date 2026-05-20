using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace web.Migrations
{
    /// <inheritdoc />
    public partial class AddVolunteerMetaAppInstall : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AppInstalledAt",
                table: "VolunteerMetas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AppDeviceName",
                table: "VolunteerMetas",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppInstalledAt",
                table: "VolunteerMetas");

            migrationBuilder.DropColumn(
                name: "AppDeviceName",
                table: "VolunteerMetas");
        }
    }
}
