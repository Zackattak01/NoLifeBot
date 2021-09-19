using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace NoLifeBot.Data.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "voice_periods",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<ulong>(type: "numeric(20,0)", nullable: false),
                    channel_id = table.Column<ulong>(type: "numeric(20,0)", nullable: false),
                    guild_id = table.Column<ulong>(type: "numeric(20,0)", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ended_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    was_muted = table.Column<bool>(type: "boolean", nullable: false),
                    was_deafened = table.Column<bool>(type: "boolean", nullable: false),
                    was_streaming = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_voice_periods", x => x.id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "voice_periods");
        }
    }
}
