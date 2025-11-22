using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IrcChat.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMutedUsersTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MutedUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelName = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    MutedByUserId = table.Column<string>(type: "text", nullable: false),
                    MutedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MutedUsers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MutedUsers_ChannelName",
                table: "MutedUsers",
                column: "ChannelName");

            migrationBuilder.CreateIndex(
                name: "IX_MutedUsers_ChannelName_UserId",
                table: "MutedUsers",
                columns: new[] { "ChannelName", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MutedUsers_UserId",
                table: "MutedUsers",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MutedUsers");
        }
    }
}
