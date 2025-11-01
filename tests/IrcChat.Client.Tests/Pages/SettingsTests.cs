// tests/IrcChat.Client.Tests/Pages/SettingsTests.cs
using System.Net;
using System.Net.Http.Json;
using Bunit;
using FluentAssertions;
using IrcChat.Client.Pages;
using IrcChat.Client.Services;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace IrcChat.Client.Tests.Pages;

public class SettingsTests : TestContext
{
    private readonly Mock<IUnifiedAuthService> _authServiceMock;
    private readonly Mock<HttpClient> _httpClientMock;
    private readonly Mock<NavigationManager> _navigationManagerMock;

    public SettingsTests()
    {
        _authServiceMock = new Mock<IUnifiedAuthService>();
        _httpClientMock = new Mock<HttpClient>();
        _navigationManagerMock = new Mock<NavigationManager>();

        Services.AddSingleton(_authServiceMock.Object);
        Services.AddSingleton(_httpClientMock.Object);
        Services.AddSingleton(_navigationManagerMock.Object);
    }

    [Fact]
    public void Settings_WhenNoUsername_ShouldRedirectToLogin()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(false);

        var navigateCalled = false;
        _navigationManagerMock
            .Setup(x => x.NavigateTo("/login", false))
            .Callback(() => navigateCalled = true);

        // Act
        var cut = RenderComponent<Settings>();

        // Assert
        navigateCalled.Should().BeTrue();
    }

    [Fact]
    public void Settings_WithReservedUser_ShouldShowUserInfo()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");
        _authServiceMock.Setup(x => x.IsReserved).Returns(true);
        _authServiceMock.Setup(x => x.Email).Returns("test@example.com");
        _authServiceMock.Setup(x => x.ReservedProvider).Returns(ExternalAuthProvider.Google);
        _authServiceMock.Setup(x => x.AvatarUrl).Returns("https://example.com/avatar.jpg");
        _authServiceMock.Setup(x => x.IsAdmin).Returns(false);

        // Act
        var cut = RenderComponent<Settings>();

        // Assert
        cut.Markup.Should().Contain("TestUser");
        cut.Markup.Should().Contain("test@example.com");
        cut.Markup.Should().Contain("Google");
        cut.Markup.Should().Contain("Réservé");
    }

    [Fact]
    public void Settings_WithGuestUser_ShouldShowGuestBadge()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("GuestUser");
        _authServiceMock.Setup(x => x.IsReserved).Returns(false);

        // Act
        var cut = RenderComponent<Settings>();

        // Assert
        cut.Markup.Should().Contain("GuestUser");
        cut.Markup.Should().Contain("Invité");
    }

    [Fact]
    public void Settings_WithAdminUser_ShouldShowAdminPanel()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("AdminUser");
        _authServiceMock.Setup(x => x.IsReserved).Returns(true);
        _authServiceMock.Setup(x => x.IsAdmin).Returns(true);
        _authServiceMock.Setup(x => x.UserId).Returns(Guid.NewGuid());
        _authServiceMock.Setup(x => x.Email).Returns("admin@example.com");

        // Act
        var cut = RenderComponent<Settings>();

        // Assert
        cut.Markup.Should().Contain("Administration");
        cut.Markup.Should().Contain("⚡ Admin");
    }

    [Fact]
    public async Task Settings_CreateChannel_WithReservedUser_ShouldSucceed()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");
        _authServiceMock.Setup(x => x.IsReserved).Returns(true);
        _authServiceMock.Setup(x => x.IsAuthenticated).Returns(true);
        _authServiceMock.Setup(x => x.Token).Returns("test-token");

        var createdChannel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "test-channel",
            CreatedBy = "TestUser",
            CreatedAt = DateTime.UtcNow
        };

        _httpClientMock
            .Setup(x => x.PostAsJsonAsync("/api/channels", It.IsAny<Channel>(), default))
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Created,
                Content = JsonContent.Create(createdChannel)
            });

        var cut = RenderComponent<Settings>();

        // Act
        var input = cut.Find(".input-group input");
        await cut.InvokeAsync(() => input.Input("test-channel"));

        var createButton = cut.Find(".input-group .btn-primary");
        await cut.InvokeAsync(() => createButton.Click());
        await Task.Delay(100);

        // Assert
        _httpClientMock.Verify(
            x => x.PostAsJsonAsync("/api/channels", It.IsAny<Channel>(), default),
            Times.Once);
    }

    [Fact]
    public async Task Settings_CreateChannel_DuplicateName_ShouldShowError()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");
        _authServiceMock.Setup(x => x.IsReserved).Returns(true);
        _authServiceMock.Setup(x => x.IsAuthenticated).Returns(true);
        _authServiceMock.Setup(x => x.Token).Returns("test-token");

        _httpClientMock
            .Setup(x => x.PostAsJsonAsync("/api/channels", It.IsAny<Channel>(), default))
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent("{\"error\":\"channel_exists\"}")
            });

        var cut = RenderComponent<Settings>();

        // Act
        var input = cut.Find(".input-group input");
        await cut.InvokeAsync(() => input.Input("existing-channel"));

        var createButton = cut.Find(".input-group .btn-primary");
        await cut.InvokeAsync(() => createButton.Click());
        await Task.Delay(100);

        // Assert
        cut.Markup.Should().Contain("existe déjà");
    }

    [Fact]
    public async Task Settings_ForgetUsername_ShouldShowConfirmation()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");
        _authServiceMock.Setup(x => x.IsReserved).Returns(true);

        var cut = RenderComponent<Settings>();

        // Act
        var forgetButton = cut.Find("button:contains('Oublier mon pseudo')");
        await cut.InvokeAsync(() => forgetButton.Click());

        // Assert
        cut.Markup.Should().Contain("Confirmer la suppression");
        cut.Markup.Should().Contain("irréversible");
    }

    [Fact]
    public async Task Settings_ConfirmForget_ShouldCallServiceAndRedirect()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");
        _authServiceMock.Setup(x => x.IsReserved).Returns(true);
        _authServiceMock.Setup(x => x.ForgetUsernameAndLogoutAsync()).Returns(Task.CompletedTask);

        var navigateCalled = false;
        _navigationManagerMock
            .Setup(x => x.NavigateTo("/login", false))
            .Callback(() => navigateCalled = true);

        var cut = RenderComponent<Settings>();

        // Act
        var forgetButton = cut.Find("button:contains('Oublier mon pseudo')");
        await cut.InvokeAsync(() => forgetButton.Click());

        var confirmButton = cut.Find(".modal-actions button:contains('Oui')");
        await cut.InvokeAsync(() => confirmButton.Click());

        // Assert
        _authServiceMock.Verify(x => x.ForgetUsernameAndLogoutAsync(), Times.Once);
        navigateCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Settings_Logout_ShouldCallServiceAndRedirect()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");
        _authServiceMock.Setup(x => x.LogoutAsync()).Returns(Task.CompletedTask);

        var navigateCalled = false;
        _navigationManagerMock
            .Setup(x => x.NavigateTo("/login", false))
            .Callback(() => navigateCalled = true);

        var cut = RenderComponent<Settings>();

        // Act
        var logoutButton = cut.Find("button:contains('Se déconnecter')");
        await cut.InvokeAsync(() => logoutButton.Click());

        // Assert
        _authServiceMock.Verify(x => x.LogoutAsync(), Times.Once);
        navigateCalled.Should().BeTrue();
    }

    [Fact]
    public void Settings_BackButton_ShouldNavigateToChat()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        var navigateCalled = false;
        _navigationManagerMock
            .Setup(x => x.NavigateTo("/chat", false))
            .Callback(() => navigateCalled = true);

        var cut = RenderComponent<Settings>();

        // Act
        var backButton = cut.Find(".back-button");
        cut.InvokeAsync(() => backButton.Click());

        // Assert
        navigateCalled.Should().BeTrue();
    }

    [Fact]
    public void Settings_GuestUser_ShouldShowReserveOption()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("GuestUser");
        _authServiceMock.Setup(x => x.IsReserved).Returns(false);

        // Act
        var cut = RenderComponent<Settings>();

        // Assert
        cut.Markup.Should().Contain("réserver votre pseudo");
        cut.Markup.Should().Contain("Réserver mon pseudo");
    }

    [Fact]
    public void Settings_EnterKeyInChannelInput_ShouldCreateChannel()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");
        _authServiceMock.Setup(x => x.IsReserved).Returns(true);
        _authServiceMock.Setup(x => x.IsAuthenticated).Returns(true);
        _authServiceMock.Setup(x => x.Token).Returns("test-token");

        _httpClientMock
            .Setup(x => x.PostAsJsonAsync("/api/channels", It.IsAny<Channel>(), default))
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Created,
                Content = JsonContent.Create(new Channel())
            });

        var cut = RenderComponent<Settings>();

        // Act
        var input = cut.Find(".input-group input");
        cut.InvokeAsync(() => input.Input("test-channel"));
        cut.InvokeAsync(() => input.KeyUp("Enter"));

        // Assert
        _httpClientMock.Verify(
            x => x.PostAsJsonAsync("/api/channels", It.IsAny<Channel>(), default),
            Times.Once);
    }
}