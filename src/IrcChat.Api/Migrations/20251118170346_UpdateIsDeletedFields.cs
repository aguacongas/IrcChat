using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IrcChat.Api.Migrations;

/// <inheritdoc />
public partial class UpdateIsDeletedFields : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
                UPDATE ""PrivateMessages"" 
                SET ""IsDeletedBySender"" = ""IsDeleted"", 
                    ""IsDeletedByRecipient"" = ""IsDeleted""
                WHERE ""IsDeleted"" = TRUE;
            ");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
                UPDATE ""PrivateMessages"" 
                SET ""IsDeleted"" = TRUE
                WHERE ""IsDeletedBySender"" = TRUE OR ""IsDeletedByRecipient"" = TRUE;
            ");
    }
}