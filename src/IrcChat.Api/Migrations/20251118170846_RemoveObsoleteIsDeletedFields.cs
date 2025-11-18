using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IrcChat.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveObsoleteIsDeletedFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "PrivateMessages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "PrivateMessages",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
