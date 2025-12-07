// tests/IrcChat.Client.Tests/Pages/SettingsTests.cs
using System.Net;
using System.Net.Http.Json;
using IrcChat.Client.Pages;
using IrcChat.Client.Services;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using RichardSzalay.MockHttp;

namespace IrcChat.Client.Tests.Pages;

public class SettingsTests : BunitContext
{
    private readonly Mock<IUnifiedAuthService> authServiceMock;
    private readonly Mock<IChatService> chatServiceMock;
    private readonly Mock<IActiveChannelsService> activeChannelsServiceMock;
    private readonly MockHttpMessageHandler mockHttp;
    private readonly NavigationManager navManager;

    public SettingsTests()
    {
        authServiceMock = new Mock<IUnifiedAuthService>();
        chatServiceMock = new Mock<IChatService>();
        activeChannelsServiceMock = new Mock<IActiveChannelsService>();
        mockHttp = new MockHttpMessageHandler();

        var httpClient = mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("https://localhost:7000");

        Services.AddSingleton(authServiceMock.Object);
        Services.AddSingleton(chatServiceMock.Object);
        Services.AddSingleton(activeChannelsServiceMock.Object);
        Services.AddSingleton(httpClient);
        Services.AddSingleton(JSInterop.JSRuntime);

        navManager = Services.GetRequiredService<NavigationManager>();
    }

    [Fact]
    public void Settings_WhenNoUsername_ShouldRedirectToLogin()
    {
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock.Setup(x => x.HasUsername).Returns(false);

        // Act
        Render<Settings>();

        // Assert
        Assert.EndsWith("/login", navManager.Uri);
    }

    [Fact]
    public void Settings_WithReservedUser_ShouldShowUserInfo()
    {
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock.Setup(x => x.HasUsername).Returns(true);
        authServiceMock.Setup(x => x.Username).Returns("TestUser");
        authServiceMock.Setup(x => x.IsReserved).Returns(true);
        authServiceMock.Setup(x => x.Email).Returns("test@example.com");
        authServiceMock.Setup(x => x.ReservedProvider).Returns(ExternalAuthProvider.Google);
        authServiceMock.Setup(x => x.AvatarUrl).Returns("https://example.com/avatar.jpg");
        authServiceMock.Setup(x => x.IsAdmin).Returns(false);

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
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock.Setup(x => x.HasUsername).Returns(true);
        authServiceMock.Setup(x => x.Username).Returns("GuestUser");
        authServiceMock.Setup(x => x.IsReserved).Returns(false);

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
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock.Setup(x => x.HasUsername).Returns(true);
        authServiceMock.Setup(x => x.Username).Returns("AdminUser");
        authServiceMock.Setup(x => x.IsReserved).Returns(true);
        authServiceMock.Setup(x => x.IsAdmin).Returns(true);
        authServiceMock.Setup(x => x.UserId).Returns(Guid.NewGuid());
        authServiceMock.Setup(x => x.Email).Returns("admin@example.com");

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
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock.Setup(x => x.HasUsername).Returns(true);
        authServiceMock.Setup(x => x.Username).Returns("TestUser");
        authServiceMock.Setup(x => x.IsReserved).Returns(true);
        authServiceMock.Setup(x => x.IsAuthenticated).Returns(true);
        authServiceMock.Setup(x => x.Token).Returns("test-token");

        var createdChannel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "test-channel",
            CreatedBy = "TestUser",
            CreatedAt = DateTime.UtcNow,
        };

        var mockedRequest = mockHttp.When(HttpMethod.Post, "*/api/channels")
            .Respond(HttpStatusCode.Created, JsonContent.Create(createdChannel));

        var cut = Render<Settings>();

        // Act
        var input = await cut.InvokeAsync(() => cut.Find(".channel-form .form-group input.input-text"));
        await cut.InvokeAsync(() => input.Input("test-channel"));

        var createButton = await cut.InvokeAsync(() => cut.Find(".channel-form .btn-primary"));
        await cut.InvokeAsync(() => createButton.Click());
        await Task.Delay(100);

        // Assert
        var count = mockHttp.GetMatchCount(mockedRequest);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Settings_CreateChannel_DuplicateName_ShouldShowError()
    {
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock.Setup(x => x.HasUsername).Returns(true);
        authServiceMock.Setup(x => x.Username).Returns("TestUser");
        authServiceMock.Setup(x => x.IsReserved).Returns(true);
        authServiceMock.Setup(x => x.IsAuthenticated).Returns(true);
        authServiceMock.Setup(x => x.Token).Returns("test-token");

        mockHttp.When(HttpMethod.Post, "*/api/channels")
            .Respond(
                HttpStatusCode.BadRequest,
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
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock.Setup(x => x.HasUsername).Returns(true);
        authServiceMock.Setup(x => x.Username).Returns("TestUser");

        var cut = Render<Settings>();

        // Act
        var backButton = cut.Find(".back-button");
        cut.InvokeAsync(() => backButton.Click());

        // Assert
        Assert.EndsWith("/chat", navManager.Uri);
    }

    [Fact]
    public void Settings_GuestUser_ShouldShowReserveOption()
    {
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock.Setup(x => x.HasUsername).Returns(true);
        authServiceMock.Setup(x => x.Username).Returns("GuestUser");
        authServiceMock.Setup(x => x.IsReserved).Returns(false);

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
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock.Setup(x => x.HasUsername).Returns(true);
        authServiceMock.Setup(x => x.Username).Returns("GuestUser");
        authServiceMock.Setup(x => x.IsReserved).Returns(false);

        var cut = Render<Settings>();

        // Act
        var reserveButton = cut.Find("button:contains('Réserver mon pseudo')");
        cut.InvokeAsync(() => reserveButton.Click());

        // Assert
        Assert.Contains("reserve?username=GuestUser", navManager.Uri);
    }

    [Fact]
    public async Task Settings_Logout_ShouldCallServiceAndRedirect()
    {
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock.Setup(x => x.HasUsername).Returns(true);
        authServiceMock.Setup(x => x.Username).Returns("TestUser");
        authServiceMock.Setup(x => x.LogoutAsync()).Returns(Task.CompletedTask);

        var cut = Render<Settings>();

        // Act
        var logoutButton = cut.Find("button:contains('Se déconnecter')");
        await cut.InvokeAsync(() => logoutButton.Click());

        // Assert
        authServiceMock.Verify(x => x.LogoutAsync(), Times.Once);
        Assert.EndsWith("/login", navManager.Uri);
    }

    [Fact]
    public async Task Settings_ForgetUsername_ShouldShowConfirmation()
    {
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock.Setup(x => x.HasUsername).Returns(true);
        authServiceMock.Setup(x => x.Username).Returns("TestUser");
        authServiceMock.Setup(x => x.IsReserved).Returns(true);

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
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock.Setup(x => x.HasUsername).Returns(true);
        authServiceMock.Setup(x => x.Username).Returns("TestUser");
        authServiceMock.Setup(x => x.IsReserved).Returns(true);
        authServiceMock.Setup(x => x.ForgetUsernameAndLogoutAsync()).Returns(Task.CompletedTask);

        var cut = Render<Settings>();

        // Act
        var forgetButton = cut.Find("button:contains('Oublier mon pseudo')");
        await cut.InvokeAsync(() => forgetButton.Click());

        var confirmButton = cut.Find("button:contains('Oui, oublier')");
        await cut.InvokeAsync(() => confirmButton.Click());

        // Assert
        authServiceMock.Verify(x => x.ForgetUsernameAndLogoutAsync(), Times.Once);
        Assert.EndsWith("/login", navManager.Uri);
    }

    [Fact]
    public async Task Settings_ForgetUsername_Cancel_ShouldHideModal()
    {
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock.Setup(x => x.HasUsername).Returns(true);
        authServiceMock.Setup(x => x.Username).Returns("TestUser");
        authServiceMock.Setup(x => x.IsReserved).Returns(true);

        var cut = Render<Settings>();

        // Act
        var forgetButton = cut.Find("button:contains('Oublier mon pseudo')");
        await cut.InvokeAsync(() => forgetButton.Click());

        var cancelButton = cut.Find("button:contains('Annuler')");
        await cut.InvokeAsync(() => cancelButton.Click());

        // Assert
        authServiceMock.Verify(x => x.ForgetUsernameAndLogoutAsync(), Times.Never);
        Assert.DoesNotContain("Confirmer la suppression", cut.Markup);
    }

    [Fact]
    public async Task Settings_CreateChannel_Success_ShouldNavigateToChannel()
    {
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock.Setup(x => x.HasUsername).Returns(true);
        authServiceMock.Setup(x => x.Username).Returns("TestUser");
        authServiceMock.Setup(x => x.IsReserved).Returns(true);
        authServiceMock.Setup(x => x.IsAuthenticated).Returns(true);
        authServiceMock.Setup(x => x.Token).Returns("test-token");

        var createdChannel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "new-channel",
            CreatedBy = "TestUser",
            CreatedAt = DateTime.UtcNow,
        };

        mockHttp.When(HttpMethod.Post, "*/api/channels")
            .Respond(HttpStatusCode.Created, JsonContent.Create(createdChannel));

        var cut = Render<Settings>();

        // Act
        var input = await cut.InvokeAsync(() => cut.Find(".channel-form .form-group input.input-text"));
        await cut.InvokeAsync(() => input.Input("new-channel"));

        var createButton = await cut.InvokeAsync(() => cut.Find(".channel-form .btn-primary"));
        await cut.InvokeAsync(() => createButton.Click());
        await Task.Delay(2100); // Attendre le délai + navigation

        // Assert
        Assert.EndsWith("/chat/channel/new-channel", navManager.Uri);
    }

    [Fact]
    public async Task Settings_CreateChannel_EnterKey_ShouldCreateChannel()
    {
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock.Setup(x => x.HasUsername).Returns(true);
        authServiceMock.Setup(x => x.Username).Returns("TestUser");
        authServiceMock.Setup(x => x.IsReserved).Returns(true);
        authServiceMock.Setup(x => x.IsAuthenticated).Returns(true);
        authServiceMock.Setup(x => x.Token).Returns("test-token");

        var createdChannel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "new-channel",
            CreatedBy = "TestUser",
            CreatedAt = DateTime.UtcNow,
        };

        var mockedRequest = mockHttp.When(HttpMethod.Post, "*/api/channels")
            .Respond(HttpStatusCode.Created, JsonContent.Create(createdChannel));

        var cut = Render<Settings>();

        // Act
        var input = await cut.InvokeAsync(() => cut.Find(".channel-form .form-group input.input-text"));
        await cut.InvokeAsync(() => input.Input("new-channel"));
        await cut.InvokeAsync(() => input.KeyUp("Enter"));
        await Task.Delay(100);

        // Assert
        var count = mockHttp.GetMatchCount(mockedRequest);
        Assert.Equal(1, count);
    }

    [Fact]
    public void Settings_GuestUser_ShouldNotShowChannelCreation()
    {
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock.Setup(x => x.HasUsername).Returns(true);
        authServiceMock.Setup(x => x.Username).Returns("GuestUser");
        authServiceMock.Setup(x => x.IsReserved).Returns(false);
        authServiceMock.Setup(x => x.IsAuthenticated).Returns(false);

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
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(taskCompletionSource.Task);

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
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock.Setup(x => x.HasUsername).Returns(true);
        authServiceMock.Setup(x => x.Username).Returns("AdminUser");
        authServiceMock.Setup(x => x.IsReserved).Returns(true);
        authServiceMock.Setup(x => x.IsAdmin).Returns(true);
        authServiceMock.Setup(x => x.UserId).Returns(Guid.NewGuid());

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
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock.Setup(x => x.HasUsername).Returns(true);
        authServiceMock.Setup(x => x.Username).Returns("TestUser");
        authServiceMock.Setup(x => x.IsReserved).Returns(true);
        authServiceMock.Setup(x => x.IsAuthenticated).Returns(true);
        authServiceMock.Setup(x => x.Token).Returns("test-token");

        mockHttp.When(HttpMethod.Post, "*/api/channels")
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
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock.Setup(x => x.HasUsername).Returns(true);
        authServiceMock.Setup(x => x.Username).Returns("TestUser");
        authServiceMock.Setup(x => x.IsReserved).Returns(true);
        authServiceMock.Setup(x => x.IsAuthenticated).Returns(true);

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
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock.Setup(x => x.HasUsername).Returns(true);
        authServiceMock.Setup(x => x.Username).Returns("TestUser");
        authServiceMock.Setup(x => x.AvatarUrl).Returns("https://example.com/avatar.jpg");

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
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock.Setup(x => x.HasUsername).Returns(true);
        authServiceMock.Setup(x => x.Username).Returns("TestUser");
        authServiceMock.Setup(x => x.AvatarUrl).Returns((string?)null);

        // Act
        var cut = Render<Settings>();

        // Assert
        Assert.Contains("avatar-placeholder", cut.Markup);
    }

    // Tests à ajouter dans tests/IrcChat.Client.Tests/Pages/SettingsTests.cs

    // ==================== TESTS TOGGLE MODE NO PV ====================

    [Fact]
    public void Settings_ShouldDisplayNoPvModeToggle()
    {
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock.Setup(x => x.HasUsername).Returns(true);
        authServiceMock.Setup(x => x.Username).Returns("TestUser");
        authServiceMock.Setup(x => x.IsNoPvMode).Returns(false);

        // Act
        var cut = Render<Settings>();

        // Assert
        Assert.Contains("Mode non MP", cut.Markup);
        Assert.Contains("Bloquer les messages privés non sollicités", cut.Markup);
        Assert.NotNull(cut.Find(".toggle"));
    }

    [Fact]
    public void Settings_WithNoPvModeDisabled_ShouldShowInactiveToggle()
    {
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock.Setup(x => x.HasUsername).Returns(true);
        authServiceMock.Setup(x => x.Username).Returns("TestUser");
        authServiceMock.Setup(x => x.IsNoPvMode).Returns(false);

        // Act
        var cut = Render<Settings>();

        // Assert
        var toggle = cut.Find(".toggle");
        Assert.DoesNotContain("active", toggle.ClassList);
        Assert.Contains("Désactivé", cut.Markup);
    }

    [Fact]
    public void Settings_WithNoPvModeEnabled_ShouldShowActiveToggle()
    {
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock.Setup(x => x.HasUsername).Returns(true);
        authServiceMock.Setup(x => x.Username).Returns("TestUser");
        authServiceMock.Setup(x => x.IsNoPvMode).Returns(true);

        // Act
        var cut = Render<Settings>();

        // Assert
        var toggle = cut.Find(".toggle");
        Assert.Contains("active", toggle.ClassList);
        Assert.Contains("Activé", cut.Markup);
    }

    [Fact]
    public async Task Settings_ToggleNoPvMode_ShouldCallService()
    {
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock.Setup(x => x.HasUsername).Returns(true);
        authServiceMock.Setup(x => x.Username).Returns("TestUser");
        authServiceMock.Setup(x => x.IsNoPvMode).Returns(false);
        authServiceMock.Setup(x => x.SetNoPvModeAsync(It.IsAny<bool>())).Returns(Task.CompletedTask);

        var cut = Render<Settings>();

        // Act
        var toggle = cut.Find(".toggle");
        await cut.InvokeAsync(() => toggle.Click());

        // Assert
        authServiceMock.Verify(x => x.SetNoPvModeAsync(true), Times.Once);
    }

    [Fact]
    public async Task Settings_ToggleNoPvMode_FromEnabledToDisabled_ShouldCallService()
    {
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock.Setup(x => x.HasUsername).Returns(true);
        authServiceMock.Setup(x => x.Username).Returns("TestUser");
        authServiceMock.Setup(x => x.IsNoPvMode).Returns(true);
        authServiceMock.Setup(x => x.SetNoPvModeAsync(It.IsAny<bool>())).Returns(Task.CompletedTask);

        var cut = Render<Settings>();

        // Act
        var toggle = cut.Find(".toggle");
        await cut.InvokeAsync(() => toggle.Click());

        // Assert
        authServiceMock.Verify(x => x.SetNoPvModeAsync(false), Times.Once);
    }

    [Fact]
    public async Task Settings_ToggleNoPvMode_ShouldUpdateUI()
    {
        // Arrange
        var isNoPvMode = false;
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock.Setup(x => x.HasUsername).Returns(true);
        authServiceMock.Setup(x => x.Username).Returns("TestUser");
        authServiceMock.Setup(x => x.IsNoPvMode).Returns(() => isNoPvMode);
        authServiceMock.Setup(x => x.SetNoPvModeAsync(It.IsAny<bool>()))
            .Callback<bool>(enabled => isNoPvMode = enabled)
            .Returns(Task.CompletedTask);

        var cut = Render<Settings>();

        // Act - Premier clic (activer)
        var toggle = cut.Find(".toggle");
        await cut.InvokeAsync(() => toggle.Click());

        // Assert - Doit être activé
        Assert.Contains("active", cut.Find(".toggle").ClassList);
        Assert.Contains("Activé", cut.Markup);

        // Act - Deuxième clic (désactiver)
        await cut.InvokeAsync(() => toggle.Click());

        // Assert - Doit être désactivé
        Assert.DoesNotContain("active", cut.Find(".toggle").ClassList);
        Assert.Contains("Désactivé", cut.Markup);
    }

    [Fact]
    public async Task Settings_ToggleNoPvMode_MultipleTimes_ShouldToggleCorrectly()
    {
        // Arrange
        var isNoPvMode = false;
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock.Setup(x => x.HasUsername).Returns(true);
        authServiceMock.Setup(x => x.Username).Returns("TestUser");
        authServiceMock.Setup(x => x.IsNoPvMode).Returns(() => isNoPvMode);
        authServiceMock.Setup(x => x.SetNoPvModeAsync(It.IsAny<bool>()))
            .Callback<bool>(enabled => isNoPvMode = enabled)
            .Returns(Task.CompletedTask);

        var cut = Render<Settings>();

        // Act & Assert - Cliquer 5 fois
        for (var i = 0; i < 5; i++)
        {
            var toggle = cut.Find(".toggle");
            await cut.InvokeAsync(() => toggle.Click());

            var expectedState = (i + 1) % 2 == 1; // Impair = activé, Pair = désactivé
            Assert.Equal(expectedState, isNoPvMode);
        }

        // Vérifier que SetNoPvModeAsync a été appelé 5 fois
        authServiceMock.Verify(x => x.SetNoPvModeAsync(It.IsAny<bool>()), Times.Exactly(5));
    }

    [Fact]
    public void Settings_NoPvModeSection_ShouldExplainFeature()
    {
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock.Setup(x => x.HasUsername).Returns(true);
        authServiceMock.Setup(x => x.Username).Returns("TestUser");

        // Act
        var cut = Render<Settings>();

        // Assert - Vérifier que l'explication est présente
        Assert.Contains("Bloquer les messages privés non sollicités", cut.Markup);
        Assert.Contains("ne recevrez des messages privés que des utilisateurs avec qui vous avez déjà une conversation active", cut.Markup);
    }

    [Fact]
    public async Task Settings_ToggleNoPvMode_ShouldCallStateHasChanged()
    {
        // Arrange
        var stateChangedCount = 0;
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock.Setup(x => x.HasUsername).Returns(true);
        authServiceMock.Setup(x => x.Username).Returns("TestUser");
        authServiceMock.Setup(x => x.IsNoPvMode).Returns(false);
        authServiceMock.Setup(x => x.SetNoPvModeAsync(It.IsAny<bool>()))
            .Returns(Task.CompletedTask)
            .Callback(() => stateChangedCount++);

        var cut = Render<Settings>();

        // Act
        var toggle = cut.Find(".toggle");
        await cut.InvokeAsync(() => toggle.Click());

        // Assert - StateHasChanged devrait avoir été appelé
        Assert.True(stateChangedCount > 0);
    }

    [Fact]
    public void Settings_NoPvModeToggle_ShouldHaveAriaLabel()
    {
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock.Setup(x => x.HasUsername).Returns(true);
        authServiceMock.Setup(x => x.Username).Returns("TestUser");
        authServiceMock.Setup(x => x.IsNoPvMode).Returns(false);

        // Act
        var cut = Render<Settings>();

        // Assert
        var toggle = cut.Find(".toggle");
        var ariaLabel = toggle.GetAttribute("aria-label");
        Assert.NotNull(ariaLabel);
        Assert.Contains("mode non MP", ariaLabel.ToLower());
    }

    [Fact]
    public void Settings_NoPvModeSection_ShouldBeInCorrectCard()
    {
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock.Setup(x => x.HasUsername).Returns(true);
        authServiceMock.Setup(x => x.Username).Returns("TestUser");

        // Act
        var cut = Render<Settings>();

        // Assert - Vérifier que la section est dans une carte dédiée
        var cards = cut.FindAll(".settings-card");
        var noPvCard = cards.FirstOrDefault(c => c.TextContent.Contains("Mode non MP"));
        Assert.NotNull(noPvCard);
    }
}