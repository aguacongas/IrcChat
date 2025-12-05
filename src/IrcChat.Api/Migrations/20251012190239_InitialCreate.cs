using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IrcChat.Api.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Admins",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Username = table.Column<string>(type: "text", nullable: false),
                PasswordHash = table.Column<string>(type: "text", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_Admins", x => x.Id));

        migrationBuilder.CreateTable(
            name: "Channels",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "text", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_Channels", x => x.Id));

        migrationBuilder.CreateTable(
            name: "ConnectedUsers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Username = table.Column<string>(type: "text", nullable: false),
                ConnectionId = table.Column<string>(type: "text", nullable: false),
                Channel = table.Column<string>(type: "text", nullable: false),
                ConnectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LastActivity = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_ConnectedUsers", x => x.Id));

        migrationBuilder.CreateTable(
            name: "Messages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Username = table.Column<string>(type: "text", nullable: false),
                Content = table.Column<string>(type: "text", nullable: false),
                Channel = table.Column<string>(type: "text", nullable: false),
                Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_Messages", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_Admins_Username",
            table: "Admins",
            column: "Username",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Channels_Name",
            table: "Channels",
            column: "Name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ConnectedUsers_Channel",
            table: "ConnectedUsers",
            column: "Channel");

        migrationBuilder.CreateIndex(
            name: "IX_ConnectedUsers_ConnectionId",
            table: "ConnectedUsers",
            column: "ConnectionId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ConnectedUsers_Username_Channel",
            table: "ConnectedUsers",
            columns: ["Username", "Channel"]);

        migrationBuilder.CreateIndex(
            name: "IX_Messages_Channel",
            table: "Messages",
            column: "Channel");

        migrationBuilder.CreateIndex(
            name: "IX_Messages_Timestamp",
            table: "Messages",
            column: "Timestamp");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Admins");

        migrationBuilder.DropTable(
            name: "Channels");

        migrationBuilder.DropTable(
            name: "ConnectedUsers");

        migrationBuilder.DropTable(
            name: "Messages");
    }
}