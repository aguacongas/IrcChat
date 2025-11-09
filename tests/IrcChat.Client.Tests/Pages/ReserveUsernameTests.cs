// tests/IrcChat.Client.Tests/Pages/ReserveUsernameTests.cs
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using IrcChat.Client.Pages;
using IrcChat.Client.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace IrcChat.Client.Tests.Pages;

public class ReserveUsernameTests : TestContext
{
    private readonly Mock<IUnifiedAuthService> _authServiceMock;
    private readonly FakeNavigationManager _navManager;

    public ReserveUsernameTests()
    {
        _authServiceMock = new Mock<IUnifiedAuthService>();

        Services.AddSingleton(_authServiceMock.Object);
        Services.AddSingleton(JSInterop.JSRuntime);

        _navManager = Services.GetRequiredService<FakeNavigationManager>();
    }

    [Fact]
    public void ReserveUsername_WithoutUsernameParam_ShouldRedirectToLogin()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        // Act
        RenderComponent<ReserveUsername>();

        // Assert
        _navManager.Uri.Should().EndWith("/login");
    }

    [Fact]
    public void ReserveUsername_WithUsernameParam_ShouldDisplayUsername()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        _navManager.NavigateTo(_navManager.GetUriWithQueryParameter("username", "TestUser"));

        // Act
        var cut = RenderComponent<ReserveUsername>();

        // Assert
        cut.Markup.Should().Contain("TestUser");
        cut.Markup.Should().Contain("Réserver mon pseudo");
    }

    [Fact]
    public void ReserveUsername_ShouldShowOAuthProviders()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        _navManager.NavigateTo(_navManager.GetUriWithQueryParameter("username", "TestUser"));

        // Act
        var cut = RenderComponent<ReserveUsername>();

        // Assert
        cut.Markup.Should().Contain("Google");
        cut.Markup.Should().Contain("Microsoft");
        cut.Markup.Should().Contain("Facebook");
    }

    [Fact]
    public async Task ReserveUsername_ClickGoogleProvider_ShouldSaveAndNavigate()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        JSInterop.SetupVoid("sessionStorage.setItem", _ => true).SetVoidResult();

        _navManager.NavigateTo(_navManager.GetUriWithQueryParameter("username", "TestUser"));

        var cut = RenderComponent<ReserveUsername>();

        // Act
        var googleButton = cut.Find("button.oauth-btn.google");
        await cut.InvokeAsync(() => googleButton.Click());

        // Assert
        JSInterop.VerifyInvoke("sessionStorage.setItem", 1);
        _navManager.Uri.Should().Contain("oauth-login");
        _navManager.Uri.Should().Contain("provider=Google");
        _navManager.Uri.Should().Contain("mode=reserve");
    }

    [Fact]
    public async Task ReserveUsername_ClickMicrosoftProvider_ShouldSaveAndNavigate()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        JSInterop.SetupVoid("sessionStorage.setItem", _ => true).SetVoidResult();

        _navManager.NavigateTo(_navManager.GetUriWithQueryParameter("username", "TestUser"));

        var cut = RenderComponent<ReserveUsername>();

        // Act
        var microsoftButton = cut.Find("button.oauth-btn.microsoft");
        await cut.InvokeAsync(() => microsoftButton.Click());

        // Assert
        JSInterop.VerifyInvoke("sessionStorage.setItem", 1);
        _navManager.Uri.Should().Contain("oauth-login");
        _navManager.Uri.Should().Contain("provider=Microsoft");
        _navManager.Uri.Should().Contain("mode=reserve");
    }

    [Fact]
    public async Task ReserveUsername_ClickFacebookProvider_ShouldSaveAndNavigate()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        JSInterop.SetupVoid("sessionStorage.setItem", _ => true).SetVoidResult();

        _navManager.NavigateTo(_navManager.GetUriWithQueryParameter("username", "TestUser"));

        var cut = RenderComponent<ReserveUsername>();

        // Act
        var facebookButton = cut.Find("button.oauth-btn.facebook");
        await cut.InvokeAsync(() => facebookButton.Click());

        // Assert
        JSInterop.VerifyInvoke("sessionStorage.setItem", 1);
        _navManager.Uri.Should().Contain("oauth-login");
        _navManager.Uri.Should().Contain("provider=Facebook");
        _navManager.Uri.Should().Contain("mode=reserve");
    }

    [Fact]
    public void ReserveUsername_BackLink_ShouldNavigateToLogin()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        _navManager.NavigateTo(_navManager.GetUriWithQueryParameter("username", "TestUser"));

        var cut = RenderComponent<ReserveUsername>();

        // Act
        var backLink = cut.Find("a[href='/login']");

        // Assert
        backLink.Should().NotBeNull();
        backLink.GetAttribute("href").Should().Be("/login");
    }

    [Fact]
    public void ReserveUsername_ShouldShowDivider()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        _navManager.NavigateTo(_navManager.GetUriWithQueryParameter("username", "TestUser"));

        // Act
        var cut = RenderComponent<ReserveUsername>();

        // Assert
        cut.Markup.Should().Contain("divider");
        cut.Markup.Should().Contain("OU");
    }

    [Fact]
    public void ReserveUsername_WithEmptyUsername_ShouldRedirectToLogin()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        _navManager.NavigateTo(_navManager.GetUriWithQueryParameter("username", ""));
        // Act
        RenderComponent<ReserveUsername>();

        // Assert
        _navManager.Uri.Should().EndWith("/login");
    }

    [Fact]
    public async Task ReserveUsername_SessionStorage_ShouldStoreUsername()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        JSInterop.SetupVoid("sessionStorage.setItem", _ => true);

        _navManager.NavigateTo(_navManager.GetUriWithQueryParameter("username", "MyUsername"));
        var cut = RenderComponent<ReserveUsername>();

        // Act
        var googleButton = cut.Find("button.oauth-btn.google");
        await cut.InvokeAsync(() => googleButton.Click());

        // Assert
        JSInterop.Invocations.Should().Contain(inv =>
            inv.Identifier == "sessionStorage.setItem" &&
            inv.Arguments.Count >= 2 &&
            inv.Arguments[0]!.ToString() == "temp_username_to_reserve" &&
            inv.Arguments[1]!.ToString() == "MyUsername");
    }


    [Fact]
    public void ReserveUsername_LoadingState_ShouldNotShowProviders()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync())
            .Returns(async () => await Task.Delay(1000)); // Simulate slow loading

        _navManager.NavigateTo(_navManager.GetUriWithQueryParameter("username", "TestUser"));
        // Act
        var cut = RenderComponent<ReserveUsername>();

        // Assert
        cut.Markup.Should().Contain("Chargement");
    }
}