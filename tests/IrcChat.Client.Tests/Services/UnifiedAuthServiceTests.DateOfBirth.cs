using System.Text.Json;

namespace IrcChat.Client.Tests.Services;

public partial class UnifiedAuthServiceTests
{
    [Fact]
    public async Task SetDateOfBirthAsync_ShouldStoreDateInLocalStorage()
    {
        // Arrange
        var dateOfBirth = new DateTime(2000, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        string? capturedJson = null;

        _localStorageMock
            .Setup(x => x.SetItemAsync("ircchat_unified_auth", It.IsAny<string>()))
            .Callback<string, string>((key, value) => capturedJson = value)
            .Returns(Task.CompletedTask);

        await _service.InitializeAsync();

        // Act
        await _service.SetDateOfBirthAsync(dateOfBirth);

        // Assert
        Assert.NotNull(capturedJson);
        Assert.Contains("2000-06-15", capturedJson);
        Assert.Equal(dateOfBirth, _service.DateOfBirth);
    }

    [Fact]
    public async Task SetDateOfBirthAsync_ShouldConvertToUtc()
    {
        // Arrange
        var dateOfBirth = new DateTime(2000, 6, 15, 0, 0, 0, DateTimeKind.Local);

        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _service.InitializeAsync();

        // Act
        await _service.SetDateOfBirthAsync(dateOfBirth);

        // Assert
        Assert.NotNull(_service.DateOfBirth);
        Assert.Equal(DateTimeKind.Utc, _service.DateOfBirth.Value.Kind);
    }

    [Fact]
    public async Task SetDateOfBirthAsync_ShouldTriggerOnAuthStateChanged()
    {
        // Arrange
        var dateOfBirth = new DateTime(2000, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        var eventTriggered = false;

        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _service.OnAuthStateChanged += () => eventTriggered = true;

        await _service.InitializeAsync();

        // Act
        await _service.SetDateOfBirthAsync(dateOfBirth);

        // Assert
        Assert.True(eventTriggered);
    }

    [Fact]
    public async Task InitializeAsync_ShouldRestoreDateOfBirthFromLocalStorage()
    {
        // Arrange
        var dateOfBirth = new DateTime(2000, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        var authData = new
        {
            Username = "testuser",
            DateOfBirth = dateOfBirth
        };
        var json = JsonSerializer.Serialize(authData);

        _localStorageMock
            .Setup(x => x.GetItemAsync("ircchat_unified_auth"))
            .ReturnsAsync(json);

        // Act
        await _service.InitializeAsync();

        // Assert
        Assert.NotNull(_service.DateOfBirth);
        Assert.Equal(dateOfBirth.Date, _service.DateOfBirth.Value.Date);
    }

    [Fact]
    public async Task InitializeAsync_WithNullDateOfBirth_ShouldSetToNull()
    {
        // Arrange
        var authData = new
        {
            Username = "testuser",
            DateOfBirth = (DateTime?)null
        };
        var json = JsonSerializer.Serialize(authData);

        _localStorageMock
            .Setup(x => x.GetItemAsync("ircchat_unified_auth"))
            .ReturnsAsync(json);

        // Act
        await _service.InitializeAsync();

        // Assert
        Assert.Null(_service.DateOfBirth);
    }

    [Fact]
    public async Task InitializeAsync_WithoutDateOfBirth_ShouldSetToNull()
    {
        // Arrange
        var authData = new
        {
            Username = "testuser"
        };
        var json = JsonSerializer.Serialize(authData);

        _localStorageMock
            .Setup(x => x.GetItemAsync("ircchat_unified_auth"))
            .ReturnsAsync(json);

        // Act
        await _service.InitializeAsync();

        // Assert
        Assert.Null(_service.DateOfBirth);
    }

    [Fact]
    public async Task ClearAllAsync_ShouldClearDateOfBirth()
    {
        // Arrange
        var dateOfBirth = new DateTime(2000, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _localStorageMock
            .Setup(x => x.RemoveItemAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _service.InitializeAsync();
        await _service.SetDateOfBirthAsync(dateOfBirth);

        // Act
        await _service.ClearAllAsync();

        // Assert
        Assert.Null(_service.DateOfBirth);
    }

    [Fact]
    public async Task ForgetUsernameAndLogoutAsync_ShouldClearDateOfBirth()
    {
        // Arrange
        var dateOfBirth = new DateTime(2000, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _localStorageMock
            .Setup(x => x.RemoveItemAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _service.InitializeAsync();
        await _service.SetDateOfBirthAsync(dateOfBirth);

        // Act
        await _service.ForgetUsernameAndLogoutAsync();

        // Assert
        Assert.Null(_service.DateOfBirth);
    }

    [Fact]
    public async Task SetDateOfBirthAsync_WithMultipleCalls_ShouldOverwritePreviousValue()
    {
        // Arrange
        var firstDate = new DateTime(2000, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        var secondDate = new DateTime(1995, 3, 10, 0, 0, 0, DateTimeKind.Utc);

        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _service.InitializeAsync();

        // Act
        await _service.SetDateOfBirthAsync(firstDate);
        await _service.SetDateOfBirthAsync(secondDate);

        // Assert
        Assert.Equal(secondDate, _service.DateOfBirth);
    }

    [Fact]
    public async Task SetDateOfBirthAsync_ShouldPersistWithOtherAuthData()
    {
        // Arrange
        var dateOfBirth = new DateTime(2000, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        string? capturedJson = null;

        _localStorageMock
            .Setup(x => x.SetItemAsync("ircchat_unified_auth", It.IsAny<string>()))
            .Callback<string, string>((key, value) => capturedJson = value)
            .Returns(Task.CompletedTask);

        await _service.InitializeAsync();
        await _service.SetUsernameAsync("testuser", isReserved: false);

        // Act
        await _service.SetDateOfBirthAsync(dateOfBirth);

        // Assert
        Assert.NotNull(capturedJson);
        Assert.Contains("testuser", capturedJson);
        Assert.Contains("2000-06-15", capturedJson);
    }

    [Fact]
    public void DateOfBirth_BeforeInitialize_ShouldBeNull() =>
        // Act & Assert
        Assert.Null(_service.DateOfBirth);

    [Fact]
    public async Task DateOfBirth_AfterInitializeWithoutData_ShouldBeNull()
    {
        // Arrange
        _localStorageMock
            .Setup(x => x.GetItemAsync("ircchat_unified_auth"))
            .ReturnsAsync((string?)null);

        // Act
        await _service.InitializeAsync();

        // Assert
        Assert.Null(_service.DateOfBirth);
    }
}
