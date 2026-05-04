using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IrcChat.Api.Migrations;

/// <inheritdoc />
public partial class MessageReactions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "MessageReactions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<string>(type: "text", nullable: false),
                Username = table.Column<string>(type: "text", nullable: false),
                Emoji = table.Column<string>(type: "text", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_MessageReactions", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_MessageReactions_MessageId",
            table: "MessageReactions",
            column: "MessageId");

        migrationBuilder.CreateIndex(
            name: "IX_MessageReactions_MessageId_UserId",
            table: "MessageReactions",
            columns: ["MessageId", "UserId"],
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "MessageReactions");
    }
}