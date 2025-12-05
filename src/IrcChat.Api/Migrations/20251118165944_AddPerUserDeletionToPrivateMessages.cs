using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IrcChat.Api.Migrations;

/// <inheritdoc />
public partial class AddPerUserDeletionToPrivateMessages : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsDeletedByRecipient",
            table: "PrivateMessages",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "IsDeletedBySender",
            table: "PrivateMessages",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateIndex(
            name: "IX_PrivateMessages_RecipientUserId_IsDeletedByRecipient",
            table: "PrivateMessages",
            columns: ["RecipientUserId", "IsDeletedByRecipient"]);

        migrationBuilder.CreateIndex(
            name: "IX_PrivateMessages_SenderUserId_IsDeletedBySender",
            table: "PrivateMessages",
            columns: ["SenderUserId", "IsDeletedBySender"]);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_PrivateMessages_RecipientUserId_IsDeletedByRecipient",
            table: "PrivateMessages");

        migrationBuilder.DropIndex(
            name: "IX_PrivateMessages_SenderUserId_IsDeletedBySender",
            table: "PrivateMessages");

        migrationBuilder.DropColumn(
            name: "IsDeletedByRecipient",
            table: "PrivateMessages");

        migrationBuilder.DropColumn(
            name: "IsDeletedBySender",
            table: "PrivateMessages");
    }
}