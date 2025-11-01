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

namespace IrcChat.Client.Tests.Pages;

public class AdminLoginTests : TestContext
{
    private readonly Mock<IAuthStateService> _authStateServiceMock;
    private readonly Mock<HttpClient> _httpClientMock;
    private readonly Mock<NavigationManager> _navigationManagerMock;

    public AdminLoginTests()
    {
        _authStateServiceMock = new Mock<IAuthStateService>();
        _httpClientMock = new Mock<HttpClient>();
        _navigationManagerMock = new Mock<NavigationManager>();

        Services.AddSingleton(_authStateServiceMock.Object);
        Services.AddSingleton(_httpClientMock.Object);
        Services.AddSingleton(_navigationManagerMock.Object);
    }

    [Fact]
    public void AdminLogin_WhenAlreadyAuthenticated_ShouldRedirectToAdmin()
    {
        // Arrange
        _authStateServiceMock.Setup(x => x.IsAuthenticated).Returns(true);

        var navigateCalled = false;
        _navigationManagerMock
            .Setup(x => x.NavigateTo("/admin", false))
            .Callback(() => navigateCalled = true);

        // Act
        var cut = RenderComponent<AdminLogin>();

        // Assert
        navigateCalled.Should().BeTrue();
    }

    [Fact]
    public void AdminLogin_ShouldRenderLoginForm()
    {
        // Arrange
        _authStateServiceMock.Setup(x => x.IsAuthenticated).Returns(false);

        // Act
        var cut = RenderComponent<AdminLogin>();

        // Assert
        cut.Markup.Should().Contain("Connexion Administrateur");
        cut.Find("input[placeholder*='utilisateur']").Should().NotBeNull();
        cut.Find("input[type='password']").Should().NotBeNull();
    }

    [Fact]
    public async Task AdminLogin_WithValidCredentials_ShouldAuthenticate()
    {
        // Arrange
        _authStateServiceMock.Setup(x => x.IsAuthenticated).Returns(false);

        var loginResponse = new IrcChat.Shared.Models.LoginResponse
        {
            Token = "test-token",
            Username = "admin"
        };

        _httpClientMock
            .Setup(x => x.PostAsJsonAsync(
                "/api/auth/login",
                It.IsAny<IrcChat.Shared.Models.LoginRequest>(),
                default))
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(loginResponse)
            });

        var navigateCalled = false;
        _navigationManagerMock
            .Setup(x => x.NavigateTo("/admin", false))
            .Callback(() => navigateCalled = true);

        var cut = RenderComponent<AdminLogin>();

        // Act
        var usernameInput = cut.Find("input[placeholder*='utilisateur']");
        var passwordInput = cut.Find("input[type='password']");
        var loginButton = cut.Find("button:contains('Se connecter')");

        await cut.InvokeAsync(() => usernameInput.Input("admin"));
        await cut.InvokeAsync(() => passwordInput.Input("admin123"));
        await cut.InvokeAsync(() => loginButton.Click());
        await Task.Delay(100);

        // Assert
        _authStateServiceMock.Verify(
            x => x.SetAuthState("test-token", "admin"),
            Times.Once);
        navigateCalled.Should().BeTrue();
    }

    [Fact]
    public async Task AdminLogin_WithInvalidCredentials_ShouldShowError()
    {
        // Arrange
        _authStateServiceMock.Setup(x => x.IsAuthenticated).Returns(false);

        _httpClientMock
            .Setup(x => x.PostAsJsonAsync(
                "/api/auth/login",
                It.IsAny<IrcChat.Shared.Models.LoginRequest>(),
                default))
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Unauthorized
            });

        var cut = RenderComponent<AdminLogin>();

        // Act
        var usernameInput = cut.Find("input[placeholder*='utilisateur']");
        var passwordInput = cut.Find("input[type='password']");
        var loginButton = cut.Find("button:contains('Se connecter')");

        await cut.InvokeAsync(() => usernameInput.Input("admin"));
        await cut.InvokeAsync(() => passwordInput.Input("wrongpassword"));
        await cut.InvokeAsync(() => loginButton.Click());
        await Task.Delay(100);

        // Assert
        cut.Markup.Should().Contain("Identifiants incorrects");
        _authStateServiceMock.Verify(
            x => x.SetAuthState(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task AdminLogin_EnterKey_ShouldSubmitForm()
    {
        // Arrange
        _authStateServiceMock.Setup(x => x.IsAuthenticated).Returns(false);

        var loginResponse = new IrcChat.Shared.Models.LoginResponse
        {
            Token = "test-token",
            Username = "admin"
        };

        _httpClientMock
            .Setup(x => x.PostAsJsonAsync(
                "/api/auth/login",
                It.IsAny<IrcChat.Shared.Models.LoginRequest>(),
                default))
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(loginResponse)
            });

        var cut = RenderComponent<AdminLogin>();

        // Act
        var usernameInput = cut.Find("input[placeholder*='utilisateur']");
        var passwordInput = cut.Find("input[type='password']");

        await cut.InvokeAsync(() => usernameInput.Input("admin"));
        await cut.InvokeAsync(() => passwordInput.Input("admin123"));
        await cut.InvokeAsync(() => passwordInput.KeyPress("Enter"));

        // Assert
        _authStateServiceMock.Verify(
            x => x.SetAuthState("test-token", "admin"),
            Times.Once);
    }

    [Fact]
    public void AdminLogin_BackLink_ShouldNavigateToChat()
    {
        // Arrange
        _authStateServiceMock.Setup(x => x.IsAuthenticated).Returns(false);

        var cut = RenderComponent<AdminLogin>();

        // Act
        var backLink = cut.Find("a[href='/login']");

        // Assert
        backLink.Should().NotBeNull();
        backLink.GetAttribute("href").Should().Be("/login");
    }

    [Fact]
    public async Task AdminLogin_NetworkError_ShouldShowError()
    {
        // Arrange
        _authStateServiceMock.Setup(x => x.IsAuthenticated).Returns(false);

        _httpClientMock
            .Setup(x => x.PostAsJsonAsync(
                "/api/auth/login",
                It.IsAny<IrcChat.Shared.Models.LoginRequest>(),
                default))
            .ThrowsAsync(new HttpRequestException("Network error"));

        var cut = RenderComponent<AdminLogin>();

        // Act
        var usernameInput = cut.Find("input[placeholder*='utilisateur']");
        var passwordInput = cut.Find("input[type='password']");
        var loginButton = cut.Find("button:contains('Se connecter')");

        await cut.InvokeAsync(() => usernameInput.Input("admin"));
        await cut.InvokeAsync(() => passwordInput.Input("admin123"));
        await cut.InvokeAsync(() => loginButton.Click());
        await Task.Delay(100);

        // Assert
        cut.Markup.Should().Contain("Erreur de connexion");
    }
}