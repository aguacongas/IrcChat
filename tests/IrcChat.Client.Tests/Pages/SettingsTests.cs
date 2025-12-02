// tests/IrcChat.Client.Tests/Pages/SettingsTests.cs
using System.Net;
using System.Net.Http.Json;
using Bunit;
using IrcChat.Client.Pages;
using IrcChat.Client.Services;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RichardSzalay.MockHttp;
using Xunit;

namespace IrcChat.Client.Tests.Pages;

public class SettingsTests : BunitContext
{
    private readonly Mock<IUnifiedAuthService> _authServiceMock;
    private readonly Mock<IChatService> _chatServiceMock;
    private readonly Mock<IActiveChannelsService> _activeChannelsServiceMock;
    private readonly MockHttpMessageHandler _mockHttp;
    private readonly NavigationManager _navManager;

    public SettingsTests()
    {
        _authServiceMock = new Mock<IUnifiedAuthService>();
        _chatServiceMock = new Mock<IChatService>();
        _activeChannelsServiceMock = new Mock<IActiveChannelsService>();
        _mockHttp = new MockHttpMessageHandler();

        var httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("https://localhost:7000");

        Services.AddSingleton(_authServiceMock.Object);
        Services.AddSingleton(_chatServiceMock.Object);
        Services.AddSingleton(_activeChannelsServiceMock.Object);
        Services.AddSingleton(httpClient);
        Services.AddSingleton(JSInterop.JSRuntime);

        _navManager = Services.GetRequiredService<NavigationManager>();
    }

    [Fact]
    public void Settings_WhenNoUsername_ShouldRedirectToLogin()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(false);

        // Act
        Render<Settings>();

        // Assert
        Assert.EndsWith("/login", _navManager.Uri);
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
        var cut = Render<Settings>();

        // Assert
        Assert.Contains("TestUser", cut.Markup);
        Assert.Contains("test@example.com", cut.Markup);
        Assert.Contains("Google", cut.Markup);
        Assert.Contains("Pseudo réservé", cut.Markup);
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
        var cut = Render<Settings>();

        // Assert
        Assert.Contains("GuestUser", cut.Markup);
        Assert.Contains("Invité", cut.Markup);
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
        var cut = Render<Settings>();

        // Assert
        Assert.Contains("Administration", cut.Markup);
        Assert.Contains("⚡ Admin", cut.Markup);
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

        var mockedRequest = _mockHttp.When(HttpMethod.Post, "*/api/channels")
            .Respond(HttpStatusCode.Created, JsonContent.Create(createdChannel));

        var cut = Render<Settings>();

        // Act
        var input = await cut.InvokeAsync(() => cut.Find(".channel-form .form-group input.input-text"));
        await cut.InvokeAsync(() => input.Input("test-channel"));

        var createButton = await cut.InvokeAsync(() => cut.Find(".channel-form .btn-primary"));
        await cut.InvokeAsync(() => createButton.Click());
        await Task.Delay(100);

        // Assert
        var count = _mockHttp.GetMatchCount(mockedRequest);
        Assert.Equal(1, count);
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

        _mockHttp.When(HttpMethod.Post, "*/api/channels")
            .Respond(HttpStatusCode.BadRequest,
                new StringContent("{\"error\":\"channel_exists\"}"));

        var cut = Render<Settings>();

        // Act
        var input = await cut.InvokeAsync(() => cut.Find(".channel-form .form-group input.input-text"));
        await cut.InvokeAsync(() => input.Input("existing-channel"));

        var createButton = await cut.InvokeAsync(() => cut.Find(".channel-form .btn-primary"));
        await cut.InvokeAsync(() => createButton.Click());
        await Task.Delay(100);

        // Assert
        Assert.Contains("existe déjà", cut.Markup);
    }

    [Fact]
    public void Settings_BackButton_ShouldNavigateToChat()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        var cut = Render<Settings>();

        // Act
        var backButton = cut.Find(".back-button");
        cut.InvokeAsync(() => backButton.Click());

        // Assert
        Assert.EndsWith("/chat", _navManager.Uri);
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
        var cut = Render<Settings>();

        // Assert
        Assert.Contains("réserver votre pseudo", cut.Markup);
        Assert.Contains("Réserver mon pseudo", cut.Markup);
    }

    [Fact]
    public void Settings_GuestUser_ReserveButton_ShouldNavigateToReserve()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("GuestUser");
        _authServiceMock.Setup(x => x.IsReserved).Returns(false);

        var cut = Render<Settings>();

        // Act
        var reserveButton = cut.Find("button:contains('Réserver mon pseudo')");
        cut.InvokeAsync(() => reserveButton.Click());

        // Assert
        Assert.Contains("reserve?username=GuestUser", _navManager.Uri);
    }

    [Fact]
    public async Task Settings_Logout_ShouldCallServiceAndRedirect()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");
        _authServiceMock.Setup(x => x.LogoutAsync()).Returns(Task.CompletedTask);

        var cut = Render<Settings>();

        // Act
        var logoutButton = cut.Find("button:contains('Se déconnecter')");
        await cut.InvokeAsync(() => logoutButton.Click());

        // Assert
        _authServiceMock.Verify(x => x.LogoutAsync(), Times.Once);
        Assert.EndsWith("/login", _navManager.Uri);
    }

    [Fact]
    public async Task Settings_ForgetUsername_ShouldShowConfirmation()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");
        _authServiceMock.Setup(x => x.IsReserved).Returns(true);

        var cut = Render<Settings>();

        // Act
        var forgetButton = cut.Find("button:contains('Oublier mon pseudo')");
        await cut.InvokeAsync(() => forgetButton.Click());

        // Assert
        Assert.Contains("Confirmer la suppression", cut.Markup);
        Assert.Contains("irréversible", cut.Markup);
    }

    [Fact]
    public async Task Settings_ForgetUsername_Confirm_ShouldCallServiceAndRedirect()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");
        _authServiceMock.Setup(x => x.IsReserved).Returns(true);
        _authServiceMock.Setup(x => x.ForgetUsernameAndLogoutAsync()).Returns(Task.CompletedTask);

        var cut = Render<Settings>();

        // Act
        var forgetButton = cut.Find("button:contains('Oublier mon pseudo')");
        await cut.InvokeAsync(() => forgetButton.Click());

        var confirmButton = cut.Find("button:contains('Oui, oublier')");
        await cut.InvokeAsync(() => confirmButton.Click());

        // Assert
        _authServiceMock.Verify(x => x.ForgetUsernameAndLogoutAsync(), Times.Once);
        Assert.EndsWith("/login", _navManager.Uri);
    }

    [Fact]
    public async Task Settings_ForgetUsername_Cancel_ShouldHideModal()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");
        _authServiceMock.Setup(x => x.IsReserved).Returns(true);

        var cut = Render<Settings>();

        // Act
        var forgetButton = cut.Find("button:contains('Oublier mon pseudo')");
        await cut.InvokeAsync(() => forgetButton.Click());

        var cancelButton = cut.Find("button:contains('Annuler')");
        await cut.InvokeAsync(() => cancelButton.Click());

        // Assert
        _authServiceMock.Verify(x => x.ForgetUsernameAndLogoutAsync(), Times.Never);
        Assert.DoesNotContain("Confirmer la suppression", cut.Markup);
    }

    [Fact]
    public async Task Settings_CreateChannel_Success_ShouldNavigateToChannel()
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
            Name = "new-channel",
            CreatedBy = "TestUser",
            CreatedAt = DateTime.UtcNow
        };

        _mockHttp.When(HttpMethod.Post, "*/api/channels")
            .Respond(HttpStatusCode.Created, JsonContent.Create(createdChannel));

        var cut = Render<Settings>();

        // Act
        var input = await cut.InvokeAsync(() => cut.Find(".channel-form .form-group input.input-text"));
        await cut.InvokeAsync(() => input.Input("new-channel"));

        var createButton = await cut.InvokeAsync(() => cut.Find(".channel-form .btn-primary"));
        await cut.InvokeAsync(() => createButton.Click());
        await Task.Delay(2100); // Attendre le délai + navigation

        // Assert
        Assert.EndsWith("/chat/channel/new-channel", _navManager.Uri);
    }

    [Fact]
    public async Task Settings_CreateChannel_EnterKey_ShouldCreateChannel()
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
            Name = "new-channel",
            CreatedBy = "TestUser",
            CreatedAt = DateTime.UtcNow
        };

        var mockedRequest = _mockHttp.When(HttpMethod.Post, "*/api/channels")
            .Respond(HttpStatusCode.Created, JsonContent.Create(createdChannel));

        var cut = Render<Settings>();

        // Act
        var input = await cut.InvokeAsync(() => cut.Find(".channel-form .form-group input.input-text"));
        await cut.InvokeAsync(() => input.Input("new-channel"));
        await cut.InvokeAsync(() => input.KeyUp("Enter"));
        await Task.Delay(100);

        // Assert
        var count = _mockHttp.GetMatchCount(mockedRequest);
        Assert.Equal(1, count);
    }

    [Fact]
    public void Settings_GuestUser_ShouldNotShowChannelCreation()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("GuestUser");
        _authServiceMock.Setup(x => x.IsReserved).Returns(false);
        _authServiceMock.Setup(x => x.IsAuthenticated).Returns(false);

        // Act
        var cut = Render<Settings>();

        // Assert
        Assert.DoesNotContain("Gestion des salons", cut.Markup);
    }

    [Fact]
    public void Settings_Loading_ShouldShowSpinner()
    {
        // Arrange
        var taskCompletionSource = new TaskCompletionSource();
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(taskCompletionSource.Task);

        // Act
        var cut = Render<Settings>();

        // Assert
        Assert.Contains("Chargement", cut.Markup);
        Assert.Contains("spinner", cut.Markup);
    }

    [Fact]
    public void Settings_AdminUser_OpenAdminPanel_ShouldDisplayPanel()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("AdminUser");
        _authServiceMock.Setup(x => x.IsReserved).Returns(true);
        _authServiceMock.Setup(x => x.IsAdmin).Returns(true);
        _authServiceMock.Setup(x => x.UserId).Returns(Guid.NewGuid());

        var cut = Render<Settings>();

        // Act
        var adminButton = cut.Find("button:contains('panneau d\\'administration')");
        cut.InvokeAsync(() => adminButton.Click());

        // Assert
        // Le panneau admin devrait être visible (AdminPanel component)
        Assert.Contains("admin-panel", cut.Markup);
    }

    [Fact]
    public async Task Settings_CreateChannel_WithError_ShouldShowError()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");
        _authServiceMock.Setup(x => x.IsReserved).Returns(true);
        _authServiceMock.Setup(x => x.IsAuthenticated).Returns(true);
        _authServiceMock.Setup(x => x.Token).Returns("test-token");

        _mockHttp.When(HttpMethod.Post, "*/api/channels")
            .Respond(HttpStatusCode.InternalServerError);

        var cut = Render<Settings>();

        // Act
        var input = await cut.InvokeAsync(() => cut.Find(".channel-form .form-group input.input-text"));
        await cut.InvokeAsync(() => input.Input("test-channel"));

        var createButton = await cut.InvokeAsync(() => cut.Find(".channel-form .btn-primary"));
        await cut.InvokeAsync(() => createButton.Click());
        await Task.Delay(100);

        // Assert
        Assert.Contains("Erreur lors de la création du salon", cut.Markup);
    }

    [Fact]
    public async Task Settings_CreateChannelButton_WithEmptyInput_ShouldBeDisabled()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");
        _authServiceMock.Setup(x => x.IsReserved).Returns(true);
        _authServiceMock.Setup(x => x.IsAuthenticated).Returns(true);

        // Act
        var cut = Render<Settings>();

        // Assert
        var createButton = await cut.InvokeAsync(() => cut.Find(".channel-form .btn-primary"));
        Assert.True(createButton.HasAttribute("disabled"));
    }

    [Fact]
    public void Settings_WithAvatarUrl_ShouldDisplayAvatar()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");
        _authServiceMock.Setup(x => x.AvatarUrl).Returns("https://example.com/avatar.jpg");

        // Act
        var cut = Render<Settings>();

        // Assert
        Assert.Contains("avatar.jpg", cut.Markup);
        var avatar = cut.Find(".profile-avatar");
        Assert.Equal("https://example.com/avatar.jpg", avatar.GetAttribute("src"));
    }

    [Fact]
    public void Settings_WithoutAvatarUrl_ShouldDisplayPlaceholder()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");
        _authServiceMock.Setup(x => x.AvatarUrl).Returns((string?)null);

        // Act
        var cut = Render<Settings>();

        // Assert
        Assert.Contains("avatar-placeholder", cut.Markup);
    }
}