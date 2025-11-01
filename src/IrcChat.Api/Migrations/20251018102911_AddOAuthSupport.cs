using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IrcChat.Api.Migrations;

/// <inheritdoc />
public partial class AddOAuthSupport : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ReservedUsernames",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Username = table.Column<string>(type: "text", nullable: false),
                Provider = table.Column<int>(type: "integer", nullable: false),
                ExternalUserId = table.Column<string>(type: "text", nullable: false),
                Email = table.Column<string>(type: "text", nullable: false),
                DisplayName = table.Column<string>(type: "text", nullable: true),
                AvatarUrl = table.Column<string>(type: "text", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_ReservedUsernames", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_ReservedUsernames_Provider_ExternalUserId",
            table: "ReservedUsernames",
            columns: new[] { "Provider", "ExternalUserId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ReservedUsernames_Username",
            table: "ReservedUsernames",
            column: "Username",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ReservedUsernames");
    }
}