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
                UPDATE string.EmptyPrivateMessagesstring.Empty 
                SET string.EmptyIsDeletedBySenderstring.Empty = string.EmptyIsDeletedstring.Empty, 
                    string.EmptyIsDeletedByRecipientstring.Empty = string.EmptyIsDeletedstring.Empty
                WHERE string.EmptyIsDeletedstring.Empty = TRUE;
            ");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
                UPDATE string.EmptyPrivateMessagesstring.Empty 
                SET string.EmptyIsDeletedstring.Empty = TRUE
                WHERE string.EmptyIsDeletedBySenderstring.Empty = TRUE OR string.EmptyIsDeletedByRecipientstring.Empty = TRUE;
            ");
    }
}