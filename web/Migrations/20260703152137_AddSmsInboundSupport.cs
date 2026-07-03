using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace web.Migrations
{
    /// <inheritdoc />
    public partial class AddSmsInboundSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SmsMessages_Volunteers_VolunteerId",
                table: "SmsMessages");

            migrationBuilder.RenameColumn(
                name: "QueuedAt",
                table: "SmsMessages",
                newName: "OccurredAt");

            migrationBuilder.AlterColumn<int>(
                name: "VolunteerId",
                table: "SmsMessages",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<string>(
                name: "SentByUserId",
                table: "SmsMessages",
                type: "TEXT",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 450);

            migrationBuilder.AlterColumn<Guid>(
                name: "MessageId",
                table: "SmsMessages",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            // De 3 eksisterende rækker er alle udgående sms'er sendt før denne feature —
            // backfill dem eksplicit som Outbound (1), ikke enummets CLR-default Inbound (0).
            migrationBuilder.AddColumn<int>(
                name: "Direction",
                table: "SmsMessages",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_SmsMessages_Direction",
                table: "SmsMessages",
                column: "Direction");

            migrationBuilder.AddForeignKey(
                name: "FK_SmsMessages_Volunteers_VolunteerId",
                table: "SmsMessages",
                column: "VolunteerId",
                principalTable: "Volunteers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SmsMessages_Volunteers_VolunteerId",
                table: "SmsMessages");

            migrationBuilder.DropIndex(
                name: "IX_SmsMessages_Direction",
                table: "SmsMessages");

            migrationBuilder.DropColumn(
                name: "Direction",
                table: "SmsMessages");

            migrationBuilder.RenameColumn(
                name: "OccurredAt",
                table: "SmsMessages",
                newName: "QueuedAt");

            migrationBuilder.AlterColumn<int>(
                name: "VolunteerId",
                table: "SmsMessages",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SentByUserId",
                table: "SmsMessages",
                type: "TEXT",
                maxLength: 450,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "MessageId",
                table: "SmsMessages",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SmsMessages_Volunteers_VolunteerId",
                table: "SmsMessages",
                column: "VolunteerId",
                principalTable: "Volunteers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
