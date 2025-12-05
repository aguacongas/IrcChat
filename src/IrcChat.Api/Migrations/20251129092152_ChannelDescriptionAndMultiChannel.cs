using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IrcChat.Api.Migrations;

/// <inheritdoc />
public partial class ChannelDescriptionAndMultiChannel : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ConnectedUsers_ConnectionId",
            table: "ConnectedUsers");

        migrationBuilder.DropIndex(
            name: "IX_ConnectedUsers_Username_Channel",
            table: "ConnectedUsers");

        migrationBuilder.AddColumn<string>(
            name: "Description",
            table: "Channels",
            type: "text",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_ConnectedUsers_ConnectionId",
            table: "ConnectedUsers",
            column: "ConnectionId");

        migrationBuilder.CreateIndex(
            name: "IX_ConnectedUsers_Username_Channel",
            table: "ConnectedUsers",
            columns: ["Username", "Channel"],
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ConnectedUsers_ConnectionId",
            table: "ConnectedUsers");

        migrationBuilder.DropIndex(
            name: "IX_ConnectedUsers_Username_Channel",
            table: "ConnectedUsers");

        migrationBuilder.DropColumn(
            name: "Description",
            table: "Channels");

        migrationBuilder.CreateIndex(
            name: "IX_ConnectedUsers_ConnectionId",
            table: "ConnectedUsers",
            column: "ConnectionId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ConnectedUsers_Username_Channel",
            table: "ConnectedUsers",
            columns: ["Username", "Channel"]);
    }
}