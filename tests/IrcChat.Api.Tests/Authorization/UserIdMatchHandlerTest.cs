using System.Security.Claims;
using IrcChat.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace IrcChat.Api.Tests.Authorization;

public class UserIdMatchHandlerTest
{
    [Fact]
    public async Task HandleAsync_WhenUserIdMatchesRequirement_Succeeds()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var requirement = new UserIdMatchRequirement(userId.ToString(), string.Empty);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId)
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var user = new ClaimsPrincipal(identity);
        var context = new AuthorizationHandlerContext([requirement], user, null);
        var sut = new UserIdMatchHandler(null!);
        // Act
        await sut.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_WhenUserIdDoesNotMatch_Fails()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var requirement = new UserIdMatchRequirement(string.Empty, string.Empty);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId)
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var user = new ClaimsPrincipal(identity);
        var context = new AuthorizationHandlerContext([requirement], user, null);
        var sut = new UserIdMatchHandler(null!);
        // Act
        await sut.HandleAsync(context);

        // Assert
        Assert.True(context.HasFailed);
    }
}
