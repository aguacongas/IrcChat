// tests/IrcChat.Client.Tests/Pages/ReserveUsernameTests.cs
using Bunit;
using IrcChat.Client.Pages;
using IrcChat.Client.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace IrcChat.Client.Tests.Pages;

public class ReserveUsernameTests : BunitContext
{
    private readonly Mock<IUnifiedAuthService> _authServiceMock;
    private readonly NavigationManager _navManager;

    public ReserveUsernameTests()
    {
        _authServiceMock = new Mock<IUnifiedAuthService>();

        Services.AddSingleton(_authServiceMock.Object);
        Services.AddSingleton(JSInterop.JSRuntime);

        _navManager = Services.GetRequiredService<NavigationManager>();
    }

    [Fact]
    public void ReserveUsername_WithoutUsernameParam_ShouldRedirectToLogin()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.GetClientUserIdAsync()).ReturnsAsync(Guid.NewGuid().ToString());

        // Act
        Render<ReserveUsername>();

        // Assert
        Assert.EndsWith("/login", _navManager.Uri);
    }

    [Fact]
    public void ReserveUsername_WithUsernameParam_ShouldDisplayUsername()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.GetClientUserIdAsync()).ReturnsAsync(Guid.NewGuid().ToString());

        _navManager.NavigateTo(_navManager.GetUriWithQueryParameter("username", "TestUser"));

        // Act
        var cut = Render<ReserveUsername>();

        // Assert
        Assert.Contains("TestUser", cut.Markup);
        Assert.Contains("Réserver mon pseudo", cut.Markup);
    }

    [Fact]
    public void ReserveUsername_ShouldShowOAuthProviders()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.GetClientUserIdAsync()).ReturnsAsync(Guid.NewGuid().ToString());

        _navManager.NavigateTo(_navManager.GetUriWithQueryParameter("username", "TestUser"));

        // Act
        var cut = Render<ReserveUsername>();

        // Assert
        Assert.Contains("Google", cut.Markup);
        Assert.Contains("Microsoft", cut.Markup);
        Assert.Contains("Facebook", cut.Markup);
    }

    [Fact]
    public async Task ReserveUsername_ClickGoogleProvider_ShouldSaveAndNavigate()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.GetClientUserIdAsync()).ReturnsAsync(Guid.NewGuid().ToString());

        JSInterop.SetupVoid("sessionStorage.setItem", _ => true).SetVoidResult();

        _navManager.NavigateTo(_navManager.GetUriWithQueryParameter("username", "TestUser"));

        var cut = Render<ReserveUsername>();

        // Act
        var googleButton = cut.Find("button.oauth-btn.google");
        await cut.InvokeAsync(() => googleButton.Click());

        // Assert
        JSInterop.VerifyInvoke("sessionStorage.setItem", 2);
        Assert.Contains("oauth-login", _navManager.Uri);
        Assert.Contains("provider=Google", _navManager.Uri);
        Assert.Contains("mode=reserve", _navManager.Uri);
    }

    [Fact]
    public async Task ReserveUsername_ClickMicrosoftProvider_ShouldSaveAndNavigate()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.GetClientUserIdAsync()).ReturnsAsync(Guid.NewGuid().ToString());

        JSInterop.SetupVoid("sessionStorage.setItem", _ => true).SetVoidResult();

        _navManager.NavigateTo(_navManager.GetUriWithQueryParameter("username", "TestUser"));

        var cut = Render<ReserveUsername>();

        // Act
        var microsoftButton = cut.Find("button.oauth-btn.microsoft");
        await cut.InvokeAsync(() => microsoftButton.Click());

        // Assert
        JSInterop.VerifyInvoke("sessionStorage.setItem", 2);
        Assert.Contains("oauth-login", _navManager.Uri);
        Assert.Contains("provider=Microsoft", _navManager.Uri);
        Assert.Contains("mode=reserve", _navManager.Uri);
    }

    [Fact]
    public async Task ReserveUsername_ClickFacebookProvider_ShouldSaveAndNavigate()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.GetClientUserIdAsync()).ReturnsAsync(Guid.NewGuid().ToString());

        JSInterop.SetupVoid("sessionStorage.setItem", _ => true).SetVoidResult();

        _navManager.NavigateTo(_navManager.GetUriWithQueryParameter("username", "TestUser"));

        var cut = Render<ReserveUsername>();

        // Act
        var facebookButton = cut.Find("button.oauth-btn.facebook");
        await cut.InvokeAsync(() => facebookButton.Click());

        // Assert
        JSInterop.VerifyInvoke("sessionStorage.setItem", 2);
        Assert.Contains("oauth-login", _navManager.Uri);
        Assert.Contains("provider=Facebook", _navManager.Uri);
        Assert.Contains("mode=reserve", _navManager.Uri);
    }

    [Fact]
    public async Task ReserveUsername_BackLink_ShouldNavigateToLogin()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.GetClientUserIdAsync()).ReturnsAsync(Guid.NewGuid().ToString());

        _navManager.NavigateTo(_navManager.GetUriWithQueryParameter("username", "TestUser"));

        var cut = Render<ReserveUsername>();

        // Act
        var backLink = await cut.InvokeAsync(() => cut.Find("a.back-link"));

        // Assert
        Assert.NotNull(backLink);
        Assert.Equal("http://localhost/login", backLink.Attributes["href"]!.Value);
    }

    [Fact]
    public void ReserveUsername_ShouldShowDivider()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.GetClientUserIdAsync()).ReturnsAsync(Guid.NewGuid().ToString());

        _navManager.NavigateTo(_navManager.GetUriWithQueryParameter("username", "TestUser"));

        // Act
        var cut = Render<ReserveUsername>();

        // Assert
        Assert.Contains("divider", cut.Markup);
        Assert.Contains("OU", cut.Markup);
    }

    [Fact]
    public void ReserveUsername_WithEmptyUsername_ShouldRedirectToLogin()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.GetClientUserIdAsync()).ReturnsAsync(Guid.NewGuid().ToString());

        _navManager.NavigateTo(_navManager.GetUriWithQueryParameter("username", ""));

        // Act
        Render<ReserveUsername>();

        // Assert
        Assert.EndsWith("/login", _navManager.Uri);
    }

    [Fact]
    public async Task ReserveUsername_SessionStorage_ShouldStoreUsernameAndId()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.GetClientUserIdAsync()).ReturnsAsync(userId);

        JSInterop.SetupVoid("sessionStorage.setItem", _ => true).SetVoidResult();

        _navManager.NavigateTo(_navManager.GetUriWithQueryParameter("username", "MyUsername"));
        var cut = Render<ReserveUsername>();

        // Act
        var googleButton = cut.Find("button.oauth-btn.google");
        await cut.InvokeAsync(() => googleButton.Click());

        // Assert
        Assert.Contains(JSInterop.Invocations, inv =>
            inv.Identifier == "sessionStorage.setItem" &&
            inv.Arguments.Count >= 2 &&
            inv.Arguments[0]!.ToString() == "temp_username_to_reserve" &&
            inv.Arguments[1]!.ToString() == "MyUsername");

        Assert.Contains(JSInterop.Invocations, inv =>
            inv.Identifier == "sessionStorage.setItem" &&
            inv.Arguments.Count >= 2 &&
            inv.Arguments[0]!.ToString() == "temp_user_id" &&
            inv.Arguments[1]!.ToString() == userId);
    }

    [Fact]
    public void ReserveUsername_LoadingState_ShouldNotShowProviders()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync())
            .Returns(async () => await Task.Delay(1000)); // Simulate slow loading
        _authServiceMock.Setup(x => x.GetClientUserIdAsync()).ReturnsAsync(Guid.NewGuid().ToString());

        _navManager.NavigateTo(_navManager.GetUriWithQueryParameter("username", "TestUser"));

        // Act
        var cut = Render<ReserveUsername>();

        // Assert
        Assert.Contains("Chargement", cut.Markup);
    }

    [Fact]
    public async Task ReserveUsername_GetClientUserIdAsync_ShouldBeCalled()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.GetClientUserIdAsync()).ReturnsAsync(userId);

        JSInterop.SetupVoid("sessionStorage.setItem", _ => true).SetVoidResult();

        _navManager.NavigateTo(_navManager.GetUriWithQueryParameter("username", "TestUser"));

        // Act
        Render<ReserveUsername>();

        await Task.Delay(100); // Attendre l'initialisation

        // Assert
        _authServiceMock.Verify(x => x.GetClientUserIdAsync(), Times.Once);
    }

    [Fact]
    public void ReserveUsername_FailedToRetrieveUserId_ShouldShowError()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.GetClientUserIdAsync())
            .ReturnsAsync(string.Empty); // Impossible de récupérer l'UserId

        _navManager.NavigateTo(_navManager.GetUriWithQueryParameter("username", "TestUser"));

        // Act
        var cut = Render<ReserveUsername>();

        // Assert
        Assert.Contains("Impossible de récupérer votre identifiant utilisateur", cut.Markup);
    }

    [Fact]
    public void ReserveUsername_InvalidUserIdFormat_ShouldShowError()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.GetClientUserIdAsync())
            .ReturnsAsync("not-a-valid-guid"); // Format GUID invalide

        _navManager.NavigateTo(_navManager.GetUriWithQueryParameter("username", "TestUser"));

        // Act
        var cut = Render<ReserveUsername>();

        // Assert
        Assert.Contains("Impossible de récupérer votre identifiant utilisateur", cut.Markup);
    }

    [Fact]
    public async Task ReserveUsername_ReserveWithProvider_ShouldNotAllowWithoutUserId()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.GetClientUserIdAsync())
            .Returns(Task.FromResult<string>(null!)); // Aucun UserId

        JSInterop.SetupVoid("sessionStorage.setItem", _ => true).SetVoidResult();

        _navManager.NavigateTo(_navManager.GetUriWithQueryParameter("username", "TestUser"));

        var cut = Render<ReserveUsername>();

        // Act - Le composant devrait afficher une erreur
        await Task.Delay(100);

        // Assert
        Assert.Contains("Impossible de récupérer votre identifiant utilisateur", cut.Markup);
    }

    [Fact]
    public async Task ReserveUsername_ProviderButtons_ShouldBeSaveToSessionStorage()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.GetClientUserIdAsync()).ReturnsAsync(userId);

        JSInterop.SetupVoid("sessionStorage.setItem", _ => true).SetVoidResult();

        _navManager.NavigateTo(_navManager.GetUriWithQueryParameter("username", "MyPseudo"));

        var cut = Render<ReserveUsername>();

        // Act
        var buttons = cut.FindAll("button.oauth-btn");
        Assert.NotEmpty(buttons);

        // Cliquer sur Google
        await cut.InvokeAsync(() => buttons[0].Click());

        // Assert - Vérifier que setItem a été appelé 2 fois (username + userId)
        var setItemCalls = JSInterop.Invocations
            .Where(inv => inv.Identifier == "sessionStorage.setItem")
            .ToList();

        Assert.NotEmpty(setItemCalls);
        Assert.Contains(setItemCalls, inv =>
            inv.Arguments[0]!.ToString() == "temp_username_to_reserve" &&
            inv.Arguments[1]!.ToString() == "MyPseudo");
        Assert.Contains(setItemCalls, inv =>
            inv.Arguments[0]!.ToString() == "temp_user_id" &&
            inv.Arguments[1]!.ToString() == userId);
    }
}