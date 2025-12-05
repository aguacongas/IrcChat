using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IrcChat.Api.Migrations;

/// <inheritdoc />
public partial class AddPrivateMessages : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PrivateMessages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                SenderUsername = table.Column<string>(type: "text", nullable: false),
                RecipientUsername = table.Column<string>(type: "text", nullable: false),
                Content = table.Column<string>(type: "text", nullable: false),
                Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsRead = table.Column<bool>(type: "boolean", nullable: false),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_PrivateMessages", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_PrivateMessages_RecipientUsername",
            table: "PrivateMessages",
            column: "RecipientUsername");

        migrationBuilder.CreateIndex(
            name: "IX_PrivateMessages_SenderUsername",
            table: "PrivateMessages",
            column: "SenderUsername");

        migrationBuilder.CreateIndex(
            name: "IX_PrivateMessages_Timestamp",
            table: "PrivateMessages",
            column: "Timestamp");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "PrivateMessages");
    }
}