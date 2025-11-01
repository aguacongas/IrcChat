using System.Net;
using System.Net.Http.Json;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using IrcChat.Client.Pages;
using IrcChat.Client.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RichardSzalay.MockHttp;
using Xunit;
using static IrcChat.Client.Components.AdminStatsGrid;
using static IrcChat.Client.Components.AdminUsersTable;

namespace IrcChat.Client.Tests.Pages;

public class AdminTests : TestContext
{
    private readonly Mock<IAuthStateService> _authStateServiceMock;
    private readonly MockHttpMessageHandler _mockHttp;
    private readonly FakeNavigationManager _navManager;

    public AdminTests()
    {
        _authStateServiceMock = new Mock<IAuthStateService>();
        _mockHttp = new MockHttpMessageHandler();
        _navManager = Services.GetRequiredService<FakeNavigationManager>();

        var httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("https://localhost:7000");

        Services.AddSingleton(_authStateServiceMock.Object);
        Services.AddSingleton(httpClient);
    }

    [Fact]
    public void Admin_WhenNotAuthenticated_ShouldRedirectToLogin()
    {
        // Arrange
        _authStateServiceMock.Setup(x => x.IsAuthenticated).Returns(false);

        // Act
        var cut = RenderComponent<Admin>();

        // Assert
        _navManager.Uri.Should().EndWith("/admin/login");
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

        _mockHttp.When(HttpMethod.Get, "*/api/admin/stats")
            .Respond(HttpStatusCode.OK, JsonContent.Create(stats));

        _mockHttp.When(HttpMethod.Get, "*/api/admin/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<UserStats>()));

        // Act
        var cut = RenderComponent<Admin>();
        await Task.Delay(200);

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

        _mockHttp.When(HttpMethod.Get, "*/api/admin/stats")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new AdminStats()));

        _mockHttp.When(HttpMethod.Get, "*/api/admin/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<UserStats>()));

        var cut = RenderComponent<Admin>();
        await Task.Delay(200);

        // Act
        var logoutButton = cut.Find("button:contains('Déconnexion')");
        await cut.InvokeAsync(() => logoutButton.Click());

        // Assert
        _authStateServiceMock.Verify(x => x.ClearAuthState(), Times.Once);
        _navManager.Uri.Should().EndWith("/admin/login");
    }
}