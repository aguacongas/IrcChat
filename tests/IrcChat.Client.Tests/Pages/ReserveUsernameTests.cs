using IrcChat.Client.Pages;
using IrcChat.Client.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

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
        _authServiceMock.Setup(x => x.DateOfBirth).Returns((DateTime?)null);

        // Act
        Render<ReserveUsername>();

        // Assert
        Assert.EndsWith("/login", _navManager.Uri);
    }

    [Fact]
    public void ReserveUsername_WithUsernameButNoDateOfBirth_ShouldShowError()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.GetClientUserIdAsync()).ReturnsAsync(Guid.NewGuid().ToString());
        _authServiceMock.Setup(x => x.DateOfBirth).Returns((DateTime?)null);

        _navManager.NavigateTo(_navManager.GetUriWithQueryParameter("username", "TestUser"));

        // Act
        var cut = Render<ReserveUsername>();

        // Assert
        Assert.Contains("Date de naissance manquante", cut.Markup);
    }

    [Fact]
    public void ReserveUsername_WithUsernameAndDateOfBirth_ShouldDisplayUsername()
    {
        // Arrange
        var dateOfBirth = new DateTime(2000, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.GetClientUserIdAsync()).ReturnsAsync(Guid.NewGuid().ToString());
        _authServiceMock.Setup(x => x.DateOfBirth).Returns(dateOfBirth);

        _navManager.NavigateTo(_navManager.GetUriWithQueryParameter("username", "TestUser"));

        // Act
        var cut = Render<ReserveUsername>();

        // Assert
        Assert.Contains("TestUser", cut.Markup);
        Assert.Contains("Réserver mon pseudo", cut.Markup);
    }

    [Fact]
    public void ReserveUsername_WithQueryParams_ShouldUseQueryParamsOverAuthService()
    {
        // Arrange
        var authDob = new DateTime(1995, 3, 10, 0, 0, 0, DateTimeKind.Utc);
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.GetClientUserIdAsync()).ReturnsAsync(Guid.NewGuid().ToString());
        _authServiceMock.Setup(x => x.DateOfBirth).Returns(authDob);

        _navManager.NavigateTo(_navManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            ["username"] = "TestUser",
            ["day"] = "20",
            ["month"] = "6",
            ["year"] = "2000"
        }));

        // Act
        var cut = Render<ReserveUsername>();

        // Assert
        Assert.DoesNotContain("Date de naissance manquante", cut.Markup);
        Assert.Contains("TestUser", cut.Markup);
    }

    [Fact]
    public void ReserveUsername_WithInvalidDateInQueryParams_ShouldShowError()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.GetClientUserIdAsync()).ReturnsAsync(Guid.NewGuid().ToString());

        _navManager.NavigateTo(_navManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            ["username"] = "TestUser",
            ["day"] = "31",
            ["month"] = "2",
            ["year"] = "2000"
        }));

        // Act
        var cut = Render<ReserveUsername>();

        // Assert
        Assert.Contains("La date de naissance fournie n'est pas valide", cut.Markup);
    }

    [Fact]
    public void ReserveUsername_ShouldShowOAuthProviders()
    {
        // Arrange
        var dateOfBirth = new DateTime(2000, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.GetClientUserIdAsync()).ReturnsAsync(Guid.NewGuid().ToString());
        _authServiceMock.Setup(x => x.DateOfBirth).Returns(dateOfBirth);

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
        var userId = Guid.NewGuid();
        var dateOfBirth = new DateTime(2000, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.GetClientUserIdAsync()).ReturnsAsync(userId.ToString());
        _authServiceMock.Setup(x => x.DateOfBirth).Returns(dateOfBirth);

        JSInterop.SetupVoid("sessionStorage.setItem", _ => true).SetVoidResult();

        _navManager.NavigateTo(_navManager.GetUriWithQueryParameter("username", "TestUser"));

        var cut = Render<ReserveUsername>();

        // Act
        var googleButton = cut.Find("button.oauth-btn.google");
        await cut.InvokeAsync(() => googleButton.Click());

        // Assert
        JSInterop.VerifyInvoke("sessionStorage.setItem", 3); // username + userId + dob
        Assert.Contains("oauth-login", _navManager.Uri);
        Assert.Contains("provider=Google", _navManager.Uri);
        Assert.Contains("mode=reserve", _navManager.Uri);
    }

    [Fact]
    public async Task ReserveUsername_ClickMicrosoftProvider_ShouldSaveAndNavigate()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var dateOfBirth = new DateTime(2000, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.GetClientUserIdAsync()).ReturnsAsync(userId.ToString());
        _authServiceMock.Setup(x => x.DateOfBirth).Returns(dateOfBirth);

        JSInterop.SetupVoid("sessionStorage.setItem", _ => true).SetVoidResult();

        _navManager.NavigateTo(_navManager.GetUriWithQueryParameter("username", "TestUser"));

        var cut = Render<ReserveUsername>();

        // Act
        var microsoftButton = cut.Find("button.oauth-btn.microsoft");
        await cut.InvokeAsync(() => microsoftButton.Click());

        // Assert
        JSInterop.VerifyInvoke("sessionStorage.setItem", 3); // username + userId + dob
        Assert.Contains("oauth-login", _navManager.Uri);
        Assert.Contains("provider=Microsoft", _navManager.Uri);
        Assert.Contains("mode=reserve", _navManager.Uri);
    }

    [Fact]
    public async Task ReserveUsername_ClickFacebookProvider_ShouldSaveAndNavigate()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var dateOfBirth = new DateTime(2000, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.GetClientUserIdAsync()).ReturnsAsync(userId.ToString());
        _authServiceMock.Setup(x => x.DateOfBirth).Returns(dateOfBirth);

        JSInterop.SetupVoid("sessionStorage.setItem", _ => true).SetVoidResult();

        _navManager.NavigateTo(_navManager.GetUriWithQueryParameter("username", "TestUser"));

        var cut = Render<ReserveUsername>();

        // Act
        var facebookButton = cut.Find("button.oauth-btn.facebook");
        await cut.InvokeAsync(() => facebookButton.Click());

        // Assert
        JSInterop.VerifyInvoke("sessionStorage.setItem", 3); // username + userId + dob
        Assert.Contains("oauth-login", _navManager.Uri);
        Assert.Contains("provider=Facebook", _navManager.Uri);
        Assert.Contains("mode=reserve", _navManager.Uri);
    }

    [Fact]
    public async Task ReserveUsername_BackLink_ShouldNavigateToLogin()
    {
        // Arrange
        var dateOfBirth = new DateTime(2000, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.GetClientUserIdAsync()).ReturnsAsync(Guid.NewGuid().ToString());
        _authServiceMock.Setup(x => x.DateOfBirth).Returns(dateOfBirth);

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
        var dateOfBirth = new DateTime(2000, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.GetClientUserIdAsync()).ReturnsAsync(Guid.NewGuid().ToString());
        _authServiceMock.Setup(x => x.DateOfBirth).Returns(dateOfBirth);

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
        _authServiceMock.Setup(x => x.DateOfBirth).Returns((DateTime?)null);

        _navManager.NavigateTo(_navManager.GetUriWithQueryParameter("username", string.Empty));

        // Act
        Render<ReserveUsername>();

        // Assert
        Assert.EndsWith("/login", _navManager.Uri);
    }

    [Fact]
    public async Task ReserveUsername_SessionStorage_ShouldStoreUsernameUserIdAndDob()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var dateOfBirth = new DateTime(2000, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.GetClientUserIdAsync()).ReturnsAsync(userId);
        _authServiceMock.Setup(x => x.DateOfBirth).Returns(dateOfBirth);

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

        Assert.Contains(JSInterop.Invocations, inv =>
            inv.Identifier == "sessionStorage.setItem" &&
            inv.Arguments.Count >= 2 &&
            inv.Arguments[0]!.ToString() == "temp_dob");
    }

    [Fact]
    public void ReserveUsername_LoadingState_ShouldNotShowProviders()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync())
            .Returns(async () => await Task.Delay(1000)); // Simulate slow loading
        _authServiceMock.Setup(x => x.GetClientUserIdAsync()).ReturnsAsync(Guid.NewGuid().ToString());
        _authServiceMock.Setup(x => x.DateOfBirth).Returns((DateTime?)null);

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
        var dateOfBirth = new DateTime(2000, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.GetClientUserIdAsync()).ReturnsAsync(userId);
        _authServiceMock.Setup(x => x.DateOfBirth).Returns(dateOfBirth);

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
        var dateOfBirth = new DateTime(2000, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.GetClientUserIdAsync())
            .ReturnsAsync(string.Empty); // Impossible de récupérer l'UserId
        _authServiceMock.Setup(x => x.DateOfBirth).Returns(dateOfBirth);

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
        var dateOfBirth = new DateTime(2000, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.GetClientUserIdAsync())
            .ReturnsAsync("not-a-valid-guid"); // Format GUID invalide
        _authServiceMock.Setup(x => x.DateOfBirth).Returns(dateOfBirth);

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
        var dateOfBirth = new DateTime(2000, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.GetClientUserIdAsync())
            .Returns(Task.FromResult<string>(null!)); // Aucun UserId
        _authServiceMock.Setup(x => x.DateOfBirth).Returns(dateOfBirth);

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
        var dateOfBirth = new DateTime(2000, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.GetClientUserIdAsync()).ReturnsAsync(userId);
        _authServiceMock.Setup(x => x.DateOfBirth).Returns(dateOfBirth);

        JSInterop.SetupVoid("sessionStorage.setItem", _ => true).SetVoidResult();

        _navManager.NavigateTo(_navManager.GetUriWithQueryParameter("username", "MyPseudo"));

        var cut = Render<ReserveUsername>();

        // Act
        var buttons = cut.FindAll("button.oauth-btn");
        Assert.NotEmpty(buttons);

        // Cliquer sur Google
        await cut.InvokeAsync(() => buttons[0].Click());

        // Assert - Vérifier que setItem a été appelé 3 fois (username + userId + dob)
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
        Assert.Contains(setItemCalls, inv =>
            inv.Arguments[0]!.ToString() == "temp_dob");
    }

    [Fact]
    public void ReserveUsername_WithDateOfBirthFromAuthService_ShouldWork()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var dateOfBirth = new DateTime(1995, 3, 10, 0, 0, 0, DateTimeKind.Utc);

        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.GetClientUserIdAsync()).ReturnsAsync(userId);
        _authServiceMock.Setup(x => x.DateOfBirth).Returns(dateOfBirth);

        _navManager.NavigateTo(_navManager.GetUriWithQueryParameter("username", "TestUser"));

        // Act
        var cut = Render<ReserveUsername>();

        // Assert
        Assert.DoesNotContain("Date de naissance manquante", cut.Markup);
        Assert.Contains("TestUser", cut.Markup);
        Assert.Contains("Google", cut.Markup);
    }

    [Fact]
    public void ReserveUsername_WithDateOfBirthFromQueryParams_ShouldWork()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();

        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.GetClientUserIdAsync()).ReturnsAsync(userId);
        _authServiceMock.Setup(x => x.DateOfBirth).Returns((DateTime?)null);

        _navManager.NavigateTo(_navManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            ["username"] = "TestUser",
            ["day"] = "15",
            ["month"] = "6",
            ["year"] = "2000"
        }));

        // Act
        var cut = Render<ReserveUsername>();

        // Assert
        Assert.DoesNotContain("Date de naissance manquante", cut.Markup);
        Assert.Contains("TestUser", cut.Markup);
    }

    [Fact]
    public void ReserveUsername_WithoutDateOfBirth_ShouldShowErrorAndBackLink()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();

        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.GetClientUserIdAsync()).ReturnsAsync(userId);
        _authServiceMock.Setup(x => x.DateOfBirth).Returns((DateTime?)null);

        _navManager.NavigateTo(_navManager.GetUriWithQueryParameter("username", "TestUser"));

        // Act
        var cut = Render<ReserveUsername>();

        // Assert
        Assert.Contains("Date de naissance manquante", cut.Markup);
        Assert.Contains("Veuillez revenir à la page de connexion", cut.Markup);

        var backLink = cut.Find("a.back-link");
        Assert.NotNull(backLink);
    }
}