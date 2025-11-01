// tests/IrcChat.Client.Tests/Pages/LoginTests.cs
using System.Net;
using System.Net.Http.Json;
using Bunit;
using FluentAssertions;
using IrcChat.Client.Pages;
using IrcChat.Client.Services;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using Xunit;

namespace IrcChat.Client.Tests.Pages;

public class LoginTests : TestContext
{
    private readonly Mock<IUnifiedAuthService> _authServiceMock;
    private readonly Mock<HttpClient> _httpClientMock;
    private readonly Mock<IJSRuntime> _jsRuntimeMock;
    private readonly Mock<NavigationManager> _navigationManagerMock;

    public LoginTests()
    {
        _authServiceMock = new Mock<IUnifiedAuthService>();
        _httpClientMock = new Mock<HttpClient>();
        _jsRuntimeMock = new Mock<IJSRuntime>();
        _navigationManagerMock = new Mock<NavigationManager>();

        Services.AddSingleton(_authServiceMock.Object);
        Services.AddSingleton(_httpClientMock.Object);
        Services.AddSingleton(_jsRuntimeMock.Object);
        Services.AddSingleton(_navigationManagerMock.Object);
    }

    [Fact]
    public void Login_WhenAlreadyAuthenticated_ShouldRedirectToChat()
    {
        // Arrange
        _authServiceMock.Setup(x => x.IsAuthenticated).Returns(true);
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        var navManager = Services.GetRequiredService<NavigationManager>();
        var navigateCalled = false;
        _navigationManagerMock
            .Setup(x => x.NavigateTo("/chat", false))
            .Callback(() => navigateCalled = true);

        // Act
        var cut = RenderComponent<Login>();

        // Assert
        navigateCalled.Should().BeTrue();
    }

    [Fact]
    public void Login_WithSavedUsername_ShouldPrefillInput()
    {
        // Arrange
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("SavedUser");
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        // Act
        var cut = RenderComponent<Login>();

        // Assert
        var input = cut.Find("input[placeholder*='pseudo']");
        input.GetAttribute("value").Should().Be("SavedUser");
    }

    [Fact]
    public async Task Login_EnterAsGuest_ShouldSetUsernameAndNavigate()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock
            .Setup(x => x.SetUsernameAsync("TestUser", false, null))
            .Returns(Task.CompletedTask);

        var navigateCalled = false;
        _navigationManagerMock
            .Setup(x => x.NavigateTo("/chat", false))
            .Callback(() => navigateCalled = true);

        var cut = RenderComponent<Login>();

        // Act
        var input = cut.Find("input[placeholder*='pseudo']");
        await cut.InvokeAsync(() => input.Input("TestUser"));
        await Task.Delay(600); // Attendre le debounce

        var guestButton = cut.Find("button:contains('invité')");
        await cut.InvokeAsync(() => guestButton.Click());

        // Assert
        _authServiceMock.Verify(
            x => x.SetUsernameAsync("TestUser", false, null),
            Times.Once);
        navigateCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Login_UsernameCheckRequest_ShouldShowStatus()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        var checkResponse = new UsernameCheckResponse
        {
            Available = true,
            IsReserved = false,
            IsCurrentlyUsed = false
        };

        _httpClientMock
            .Setup(x => x.PostAsJsonAsync(
                "/api/oauth/check-username",
                It.IsAny<UsernameCheckRequest>(),
                default))
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(checkResponse)
            });

        var cut = RenderComponent<Login>();

        // Act
        var input = cut.Find("input[placeholder*='pseudo']");
        await cut.InvokeAsync(() => input.Input("TestUser"));
        await Task.Delay(600); // Attendre le debounce

        // Assert
        cut.Markup.Should().Contain("Entrer en tant qu'invité");
        cut.Markup.Should().Contain("Réserver ce pseudo");
    }

    [Fact]
    public async Task Login_ReservedUsername_ShouldShowLoginButton()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        var checkResponse = new UsernameCheckResponse
        {
            Available = false,
            IsReserved = true,
            ReservedProvider = ExternalAuthProvider.Google,
            IsCurrentlyUsed = false
        };

        _httpClientMock
            .Setup(x => x.PostAsJsonAsync(
                "/api/oauth/check-username",
                It.IsAny<UsernameCheckRequest>(),
                default))
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(checkResponse)
            });

        var cut = RenderComponent<Login>();

        // Act
        var input = cut.Find("input[placeholder*='pseudo']");
        await cut.InvokeAsync(() => input.Input("ReservedUser"));
        await Task.Delay(600);

        // Assert
        cut.Markup.Should().Contain("réservé");
        cut.Markup.Should().Contain("Se connecter");
    }

    [Fact]
    public async Task Login_CurrentlyUsedUsername_ShouldShowWarning()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        var checkResponse = new UsernameCheckResponse
        {
            Available = false,
            IsReserved = false,
            IsCurrentlyUsed = true
        };

        _httpClientMock
            .Setup(x => x.PostAsJsonAsync(
                "/api/oauth/check-username",
                It.IsAny<UsernameCheckRequest>(),
                default))
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(checkResponse)
            });

        var cut = RenderComponent<Login>();

        // Act
        var input = cut.Find("input[placeholder*='pseudo']");
        await cut.InvokeAsync(() => input.Input("UsedUsername"));
        await Task.Delay(600);

        // Assert
        cut.Markup.Should().Contain("actuellement utilisé");
    }

    [Fact]
    public async Task Login_ShortUsername_ShouldNotCheckAvailability()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        var cut = RenderComponent<Login>();

        // Act
        var input = cut.Find("input[placeholder*='pseudo']");
        await cut.InvokeAsync(() => input.Input("AB")); // < 3 caractères
        await Task.Delay(600);

        // Assert
        _httpClientMock.Verify(
            x => x.PostAsJsonAsync(
                It.IsAny<string>(),
                It.IsAny<UsernameCheckRequest>(),
                default),
            Times.Never);
    }

    [Fact]
    public async Task Login_EnterKey_ShouldSubmitWhenAvailable()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock
            .Setup(x => x.SetUsernameAsync("TestUser", false, null))
            .Returns(Task.CompletedTask);

        var checkResponse = new UsernameCheckResponse
        {
            Available = true,
            IsReserved = false,
            IsCurrentlyUsed = false
        };

        _httpClientMock
            .Setup(x => x.PostAsJsonAsync(
                "/api/oauth/check-username",
                It.IsAny<UsernameCheckRequest>(),
                default))
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(checkResponse)
            });

        var cut = RenderComponent<Login>();

        // Act
        var input = cut.Find("input[placeholder*='pseudo']");
        await cut.InvokeAsync(() => input.Input("TestUser"));
        await Task.Delay(600);
        await cut.InvokeAsync(() => input.KeyUp("Enter"));

        // Assert
        _authServiceMock.Verify(
            x => x.SetUsernameAsync("TestUser", false, null),
            Times.Once);
    }

    [Fact]
    public void Login_LogoutButton_WhenAuthenticated_ShouldCallLogout()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");
        _authServiceMock.Setup(x => x.IsAuthenticated).Returns(true);
        _authServiceMock.Setup(x => x.IsReserved).Returns(true);
        _authServiceMock.Setup(x => x.ReservedProvider).Returns(ExternalAuthProvider.Google);
        _authServiceMock.Setup(x => x.LogoutAsync()).Returns(Task.CompletedTask);

        var cut = RenderComponent<Login>();

        // Act
        var logoutButton = cut.Find("button:contains('Se déconnecter')");
        cut.InvokeAsync(() => logoutButton.Click());

        // Assert
        _authServiceMock.Verify(x => x.LogoutAsync(), Times.Once);
    }
}