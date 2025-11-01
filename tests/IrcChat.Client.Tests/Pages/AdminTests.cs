using System.Net;
using System.Net.Http.Json;
using Bunit;
using FluentAssertions;
using IrcChat.Client.Pages;
using IrcChat.Client.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using static IrcChat.Client.Components.AdminStatsGrid;
using static IrcChat.Client.Components.AdminUsersTable;

namespace IrcChat.Client.Tests.Pages;

public class AdminTests : TestContext
{
    private readonly Mock<IAuthStateService> _authStateServiceMock;
    private readonly Mock<HttpClient> _httpClientMock;
    private readonly Mock<NavigationManager> _navigationManagerMock;

    public AdminTests()
    {
        _authStateServiceMock = new Mock<IAuthStateService>();
        _httpClientMock = new Mock<HttpClient>();
        _navigationManagerMock = new Mock<NavigationManager>();

        Services.AddSingleton(_authStateServiceMock.Object);
        Services.AddSingleton(_httpClientMock.Object);
        Services.AddSingleton(_navigationManagerMock.Object);
    }

    [Fact]
    public void Admin_WhenNotAuthenticated_ShouldRedirectToLogin()
    {
        // Arrange
        _authStateServiceMock.Setup(x => x.IsAuthenticated).Returns(false);

        var navigateCalled = false;
        _navigationManagerMock
            .Setup(x => x.NavigateTo("/admin/login", false))
            .Callback(() => navigateCalled = true);

        // Act
        var cut = RenderComponent<Admin>();

        // Assert
        navigateCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Admin_WhenAuthenticated_ShouldLoadStats()
    {
        // Arrange
        _authStateServiceMock.Setup(x => x.IsAuthenticated).Returns(true);
        _authStateServiceMock.Setup(x => x.Username).Returns("admin");

        var stats = new AdminStats
        {
            TotalMessages = 100,
            TotalChannels = 5,
            MessagesToday = 20,
            ActiveChannels = 3
        };

        _httpClientMock
            .Setup(x => x.GetFromJsonAsync<AdminStats>("/api/admin/stats", default))
            .ReturnsAsync(stats);

        _httpClientMock
            .Setup(x => x.GetFromJsonAsync<List<UserStats>>("/api/admin/users", default))
            .ReturnsAsync(new List<UserStats>());

        // Act
        var cut = RenderComponent<Admin>();
        await Task.Delay(100);

        // Assert
        cut.Markup.Should().Contain("100"); // TotalMessages
        cut.Markup.Should().Contain("20");  // MessagesToday
    }

    [Fact]
    public async Task Admin_Logout_ShouldClearAuthAndRedirect()
    {
        // Arrange
        _authStateServiceMock.Setup(x => x.IsAuthenticated).Returns(true);
        _authStateServiceMock.Setup(x => x.Username).Returns("admin");

        _httpClientMock
            .Setup(x => x.GetFromJsonAsync<AdminStats>("/api/admin/stats", default))
            .ReturnsAsync(new AdminStats());

        _httpClientMock
            .Setup(x => x.GetFromJsonAsync<List<UserStats>>("/api/admin/users", default))
            .ReturnsAsync(new List<UserStats>());

        var navigateCalled = false;
        _navigationManagerMock
            .Setup(x => x.NavigateTo("/admin/login", false))
            .Callback(() => navigateCalled = true);

        var cut = RenderComponent<Admin>();
        await Task.Delay(100);

        // Act
        var logoutButton = cut.Find("button:contains('Déconnexion')");
        await cut.InvokeAsync(() => logoutButton.Click());

        // Assert
        _authStateServiceMock.Verify(x => x.ClearAuthState(), Times.Once);
        navigateCalled.Should().BeTrue();
    }
}
