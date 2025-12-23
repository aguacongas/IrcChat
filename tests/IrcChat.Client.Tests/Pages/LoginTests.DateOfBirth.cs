using System.Net;
using System.Net.Http.Json;
using IrcChat.Client.Pages;
using IrcChat.Shared.Models;
using RichardSzalay.MockHttp;

namespace IrcChat.Client.Tests.Pages;

public partial class LoginTests
{
    [Fact]
    public void Login_ShouldDisplayDateOfBirthDropdowns()
    {
        // Arrange
        var checkUsernameRequest = _mockHttp
            .When(HttpMethod.Post, "*/api/oauth/check-username")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new UsernameCheckResponse
            {
                Available = true,
                IsReserved = false,
                IsCurrentlyUsed = false
            }));

        // Act
        var cut = Render<Login>();
        var usernameInput = cut.Find("input[placeholder='Votre pseudo...']");
        usernameInput.Input("testuser");

        cut.WaitForState(() => cut.Markup.Contains("Date de naissance"), TimeSpan.FromSeconds(2));

        // Assert
        Assert.Contains("Date de naissance", cut.Markup);
        Assert.NotNull(cut.Find("select#day"));
        Assert.NotNull(cut.Find("select#month"));
        Assert.NotNull(cut.Find("select#year"));
    }

    [Fact]
    public void Login_DayDropdown_ShouldHave31Options()
    {
        // Arrange
        var checkUsernameRequest = _mockHttp
            .When(HttpMethod.Post, "*/api/oauth/check-username")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new UsernameCheckResponse
            {
                Available = true,
                IsReserved = false,
                IsCurrentlyUsed = false
            }));

        // Act
        var cut = Render<Login>();
        var usernameInput = cut.Find("input[placeholder='Votre pseudo...']");
        usernameInput.Input("testuser");

        cut.WaitForState(() => cut.Markup.Contains("Date de naissance"), TimeSpan.FromSeconds(2));

        var dayDropdown = cut.Find("select#day");
        var dayOptions = dayDropdown.QuerySelectorAll("option");

        // Assert
        Assert.Equal(32, dayOptions.Length); // 1 placeholder + 31 jours
        Assert.Equal("Jour", dayOptions[0].TextContent);
        Assert.Equal("1", dayOptions[1].TextContent);
        Assert.Equal("31", dayOptions[31].TextContent);
    }

    [Fact]
    public void Login_MonthDropdown_ShouldHave12Options()
    {
        // Arrange
        var checkUsernameRequest = _mockHttp
            .When(HttpMethod.Post, "*/api/oauth/check-username")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new UsernameCheckResponse
            {
                Available = true,
                IsReserved = false,
                IsCurrentlyUsed = false
            }));

        // Act
        var cut = Render<Login>();
        var usernameInput = cut.Find("input[placeholder='Votre pseudo...']");
        usernameInput.Input("testuser");

        cut.WaitForState(() => cut.Markup.Contains("Date de naissance"), TimeSpan.FromSeconds(2));

        var monthDropdown = cut.Find("select#month");
        var monthOptions = monthDropdown.QuerySelectorAll("option");

        // Assert
        Assert.Equal(13, monthOptions.Length); // 1 placeholder + 12 mois
        Assert.Equal("Mois", monthOptions[0].TextContent);
        Assert.Equal("1", monthOptions[1].TextContent);
        Assert.Equal("12", monthOptions[12].TextContent);
    }

    [Fact]
    public void Login_YearDropdown_ShouldHave120YearsInDescendingOrder()
    {
        // Arrange
        var checkUsernameRequest = _mockHttp
            .When(HttpMethod.Post, "*/api/oauth/check-username")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new UsernameCheckResponse
            {
                Available = true,
                IsReserved = false,
                IsCurrentlyUsed = false
            }));

        // Act
        var cut = Render<Login>();
        var usernameInput = cut.Find("input[placeholder='Votre pseudo...']");
        usernameInput.Input("testuser");

        cut.WaitForState(() => cut.Markup.Contains("Date de naissance"), TimeSpan.FromSeconds(2));

        var yearDropdown = cut.Find("select#year");
        var yearOptions = yearDropdown.QuerySelectorAll("option");

        // Assert
        Assert.Equal(122, yearOptions.Length); // 1 placeholder + 121 années
        Assert.Equal("Année", yearOptions[0].TextContent);
        Assert.Equal(DateTime.UtcNow.Year.ToString(), yearOptions[1].TextContent); // Année courante
        Assert.Equal((DateTime.UtcNow.Year - 120).ToString(), yearOptions[121].TextContent); // Année min
    }

    [Fact]
    public async Task Login_WithValidAge_ShouldAllowEntry()
    {
        // Arrange
        var checkUsernameRequest = _mockHttp
            .When(HttpMethod.Post, "*/api/oauth/check-username")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new UsernameCheckResponse
            {
                Available = true,
                IsReserved = false,
                IsCurrentlyUsed = false
            }));

        var cut = Render<Login>();
        var usernameInput = cut.Find("input[placeholder='Votre pseudo...']");
        usernameInput.Input("testuser");

        cut.WaitForState(() => cut.Markup.Contains("Date de naissance"), TimeSpan.FromSeconds(2));

        var dayDropdown = cut.Find("select#day");
        var monthDropdown = cut.Find("select#month");
        var yearDropdown = cut.Find("select#year");

        // Act
        await cut.InvokeAsync(() => dayDropdown.Change("15"));
        await cut.InvokeAsync(() => monthDropdown.Change("6"));
        await cut.InvokeAsync(() => yearDropdown.Change("2000"));

        var button = cut.Find("button.btn-primary");
        await cut.InvokeAsync(() => button.Click());

        // Assert
        Assert.DoesNotContain("Âge insuffisant", cut.Markup);
        _authServiceMock.Verify(x => x.SetDateOfBirthAsync(It.IsAny<DateTime>()), Times.Once);
        _authServiceMock.Verify(x => x.SetUsernameAsync("testuser", false, null), Times.Once);
    }

    [Fact]
    public async Task Login_WithAgeLessThan13_ShouldShowAgeBlockedMessage()
    {
        // Arrange
        var checkUsernameRequest = _mockHttp
            .When(HttpMethod.Post, "*/api/oauth/check-username")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new UsernameCheckResponse
            {
                Available = true,
                IsReserved = false,
                IsCurrentlyUsed = false
            }));

        var cut = Render<Login>();
        var usernameInput = cut.Find("input[placeholder='Votre pseudo...']");
        usernameInput.Input("testuser");

        cut.WaitForState(() => cut.Markup.Contains("Date de naissance"), TimeSpan.FromSeconds(2));

        var dayDropdown = cut.Find("select#day");
        var monthDropdown = cut.Find("select#month");
        var yearDropdown = cut.Find("select#year");

        // Act
        await cut.InvokeAsync(() => dayDropdown.Change("15"));
        await cut.InvokeAsync(() => monthDropdown.Change("6"));
        await cut.InvokeAsync(() => yearDropdown.Change("2015")); // 10 ans

        var button = cut.Find("button.btn-primary");
        await cut.InvokeAsync(() => button.Click());

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("Âge insuffisant"), TimeSpan.FromSeconds(2));
        Assert.Contains("Âge insuffisant", cut.Markup);
        Assert.Contains("Tu dois avoir au moins 13 ans pour utiliser ce service", cut.Markup);
        _authServiceMock.Verify(x => x.SetDateOfBirthAsync(It.IsAny<DateTime>()), Times.Never);
        _authServiceMock.Verify(x => x.SetUsernameAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<ExternalAuthProvider?>()), Times.Never);
    }

    [Fact]
    public async Task Login_WithAge13_ShouldAllowEntry()
    {
        // Arrange
        var checkUsernameRequest = _mockHttp
            .When(HttpMethod.Post, "*/api/oauth/check-username")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new UsernameCheckResponse
            {
                Available = true,
                IsReserved = false,
                IsCurrentlyUsed = false
            }));

        var cut = Render<Login>();
        var usernameInput = cut.Find("input[placeholder='Votre pseudo...']");
        usernameInput.Input("testuser");

        cut.WaitForState(() => cut.Markup.Contains("Date de naissance"), TimeSpan.FromSeconds(2));

        var dayDropdown = cut.Find("select#day");
        var monthDropdown = cut.Find("select#month");
        var yearDropdown = cut.Find("select#year");

        var year13 = DateTime.UtcNow.AddYears(-13).Year;

        // Act
        await cut.InvokeAsync(() => dayDropdown.Change(DateTime.UtcNow.Day.ToString()));
        await cut.InvokeAsync(() => monthDropdown.Change(DateTime.UtcNow.Month.ToString()));
        await cut.InvokeAsync(() => yearDropdown.Change(year13.ToString()));

        var button = cut.Find("button.btn-primary");
        await cut.InvokeAsync(() => button.Click());

        // Assert
        Assert.DoesNotContain("Âge insuffisant", cut.Markup);
        _authServiceMock.Verify(x => x.SetDateOfBirthAsync(It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task Login_WithAgeGreaterThan120_ShouldShowError()
    {
        // Arrange
        var checkUsernameRequest = _mockHttp
            .When(HttpMethod.Post, "*/api/oauth/check-username")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new UsernameCheckResponse
            {
                Available = true,
                IsReserved = false,
                IsCurrentlyUsed = false
            }));

        var cut = Render<Login>();
        var usernameInput = cut.Find("input[placeholder='Votre pseudo...']");
        usernameInput.Input("testuser");

        cut.WaitForState(() => cut.Markup.Contains("Date de naissance"), TimeSpan.FromSeconds(2));

        var dayDropdown = cut.Find("select#day");
        var monthDropdown = cut.Find("select#month");
        var yearDropdown = cut.Find("select#year");

        // Act
        await cut.InvokeAsync(() => dayDropdown.Change("15"));
        await cut.InvokeAsync(() => monthDropdown.Change("6"));
        await cut.InvokeAsync(() => yearDropdown.Change("1800")); // 225 ans

        var button = cut.Find("button.btn-primary");
        await cut.InvokeAsync(() => button.Click());

        // Assert
        Assert.Contains("La date de naissance saisie n'est pas valide", cut.Markup);
        _authServiceMock.Verify(x => x.SetDateOfBirthAsync(It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task Login_WithIncompleteDateOfBirth_ShouldShowError()
    {
        // Arrange
        var checkUsernameRequest = _mockHttp
            .When(HttpMethod.Post, "*/api/oauth/check-username")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new UsernameCheckResponse
            {
                Available = true,
                IsReserved = false,
                IsCurrentlyUsed = false
            }));

        var cut = Render<Login>();
        var usernameInput = cut.Find("input[placeholder='Votre pseudo...']");
        usernameInput.Input("testuser");

        cut.WaitForState(() => cut.Markup.Contains("Date de naissance"), TimeSpan.FromSeconds(2));

        var dayDropdown = cut.Find("select#day");

        // Act - Ne sélectionner que le jour
        await cut.InvokeAsync(() => dayDropdown.Change("15"));

        var button = cut.Find("button.btn-primary");
        await cut.InvokeAsync(() => button.Click());

        // Assert
        Assert.Contains("Veuillez sélectionner votre date de naissance complète", cut.Markup);
        _authServiceMock.Verify(x => x.SetDateOfBirthAsync(It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task Login_WithInvalidDate_ShouldShowError()
    {
        // Arrange
        var checkUsernameRequest = _mockHttp
            .When(HttpMethod.Post, "*/api/oauth/check-username")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new UsernameCheckResponse
            {
                Available = true,
                IsReserved = false,
                IsCurrentlyUsed = false
            }));

        var cut = Render<Login>();
        var usernameInput = cut.Find("input[placeholder='Votre pseudo...']");
        usernameInput.Input("testuser");

        cut.WaitForState(() => cut.Markup.Contains("Date de naissance"), TimeSpan.FromSeconds(2));

        var dayDropdown = cut.Find("select#day");
        var monthDropdown = cut.Find("select#month");
        var yearDropdown = cut.Find("select#year");

        // Act - 31 février (date invalide)
        await cut.InvokeAsync(() => dayDropdown.Change("31"));
        await cut.InvokeAsync(() => monthDropdown.Change("2"));
        await cut.InvokeAsync(() => yearDropdown.Change("2000"));

        var button = cut.Find("button.btn-primary");
        await cut.InvokeAsync(() => button.Click());

        // Assert
        Assert.Contains("La date saisie n'est pas valide", cut.Markup);
        _authServiceMock.Verify(x => x.SetDateOfBirthAsync(It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task Login_AgeBlockedMessage_ClickRetour_ShouldResetForm()
    {
        // Arrange
        var checkUsernameRequest = _mockHttp
            .When(HttpMethod.Post, "*/api/oauth/check-username")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new UsernameCheckResponse
            {
                Available = true,
                IsReserved = false,
                IsCurrentlyUsed = false
            }));

        var cut = Render<Login>();
        var usernameInput = cut.Find("input[placeholder='Votre pseudo...']");
        usernameInput.Input("testuser");

        cut.WaitForState(() => cut.Markup.Contains("Date de naissance"), TimeSpan.FromSeconds(2));

        var dayDropdown = cut.Find("select#day");
        var monthDropdown = cut.Find("select#month");
        var yearDropdown = cut.Find("select#year");

        await cut.InvokeAsync(() => dayDropdown.Change("15"));
        await cut.InvokeAsync(() => monthDropdown.Change("6"));
        await cut.InvokeAsync(() => yearDropdown.Change("2015"));

        var button = cut.Find("button.btn-primary");
        await cut.InvokeAsync(() => button.Click());

        cut.WaitForState(() => cut.Markup.Contains("Âge insuffisant"), TimeSpan.FromSeconds(2));

        // Act
        var retourButton = cut.Find("button.btn-secondary");
        await cut.InvokeAsync(() => retourButton.Click());

        // Assert
        Assert.DoesNotContain("Âge insuffisant", cut.Markup);
        Assert.Contains("Date de naissance", cut.Markup);
    }

    [Fact]
    public void Login_WithStoredDateOfBirth_ShouldPreFillDropdowns()
    {
        // Arrange
        var storedDob = new DateTime(2000, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        _authServiceMock.Setup(x => x.DateOfBirth).Returns(storedDob);

        var checkUsernameRequest = _mockHttp
            .When(HttpMethod.Post, "*/api/oauth/check-username")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new UsernameCheckResponse
            {
                Available = true,
                IsReserved = false,
                IsCurrentlyUsed = false
            }));

        // Act
        var cut = Render<Login>();
        var usernameInput = cut.Find("input[placeholder='Votre pseudo...']");
        usernameInput.Input("testuser");

        cut.WaitForState(() => cut.Markup.Contains("Date de naissance"), TimeSpan.FromSeconds(2));

        var dayDropdown = cut.Find("select#day");
        var monthDropdown = cut.Find("select#month");
        var yearDropdown = cut.Find("select#year");

        // Assert
        Assert.Equal("15", dayDropdown.GetAttribute("value"));
        Assert.Equal("6", monthDropdown.GetAttribute("value"));
        Assert.Equal("2000", yearDropdown.GetAttribute("value"));
    }
}