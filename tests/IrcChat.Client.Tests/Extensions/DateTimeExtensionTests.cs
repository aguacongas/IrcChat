// tests/IrcChat.Client.Tests/Extensions/DateTimeExtensionTests.cs
using System.Diagnostics.CodeAnalysis;
using IrcChat.Client.Extensions;
using Xunit;

namespace IrcChat.Client.Tests.Extensions;

[SuppressMessage("Major Code Smell", "S6562:Always set the \"DateTimeKind\" when creating new \"DateTime\" instances", Justification = "Not relevelant for tests")]
public class DateTimeExtensionTests
{
    [Fact]
    public void ToMessageTimeString_WithToday_ShouldReturnTimeOnly()
    {
        // Arrange
        var today = DateTime.Now;
        var time = new DateTime(today.Year, today.Month, today.Day, 14, 30, 0);

        // Act
        var result = time.ToMessageTimeString();

        // Assert
        Assert.Equal("14:30", result);
    }

    [Fact]
    public void ToMessageTimeString_WithYesterday_ShouldReturnHierWithTime()
    {
        // Arrange
        var yesterday = DateTime.Now.AddDays(-1);
        var time = new DateTime(yesterday.Year, yesterday.Month, yesterday.Day, 10, 15, 0);

        // Act
        var result = time.ToMessageTimeString();

        // Assert
        Assert.Equal("Hier 10:15", result);
    }

    [Fact]
    public void ToMessageTimeString_WithOlderDate_ShouldReturnShortDate()
    {
        // Arrange
        var olderDate = DateTime.Now.AddDays(-5);
        var time = new DateTime(olderDate.Year, olderDate.Month, olderDate.Day, 16, 45, 0);

        // Act
        var result = time.ToMessageTimeString();

        // Assert
        Assert.Equal(time.ToShortDateString(), result);
    }

    [Fact]
    public void ToMessageTimeString_WithMidnight_ShouldReturnZeroHourFormat()
    {
        // Arrange
        var today = DateTime.Now;
        var midnight = new DateTime(today.Year, today.Month, today.Day, 0, 0, 0);

        // Act
        var result = midnight.ToMessageTimeString();

        // Assert
        Assert.Equal("00:00", result);
    }

    [Fact]
    public void ToMessageTimeString_WithEndOfDay_ShouldReturnCorrectTime()
    {
        // Arrange
        var today = DateTime.Now;
        var endOfDay = new DateTime(today.Year, today.Month, today.Day, 23, 59, 59);

        // Act
        var result = endOfDay.ToMessageTimeString();

        // Assert
        Assert.Equal("23:59", result);
    }

    [Fact]
    public void ToMessageTimeString_WithSingleDigitHourAndMinute_ShouldReturnPaddedFormat()
    {
        // Arrange
        var today = DateTime.Now;
        var time = new DateTime(today.Year, today.Month, today.Day, 9, 5, 0);

        // Act
        var result = time.ToMessageTimeString();

        // Assert
        Assert.Equal("09:05", result);
    }

    [Fact]
    public void ToMessageTimeString_WithLastYear_ShouldReturnShortDate()
    {
        // Arrange
        var lastYear = DateTime.Now.AddYears(-1);
        var time = new DateTime(lastYear.Year, 12, 25, 15, 30, 0);

        // Act
        var result = time.ToMessageTimeString();

        // Assert
        Assert.Equal(time.ToShortDateString(), result);
    }

    [Fact]
    public void ToMessageTimeString_WithNextMonth_ShouldReturnShortDate()
    {
        // Arrange
        // Créer une date dans 35 jours (au moins un mois plus tard)
        var futureDate = DateTime.Now.AddDays(35);
        var time = new DateTime(futureDate.Year, futureDate.Month, futureDate.Day, 12, 0, 0);

        // Act
        var result = time.ToMessageTimeString();

        // Assert
        Assert.Equal(time.ToShortDateString(), result);
    }

    [Fact]
    public void ToMessageTimeString_WithTwoDaysAgo_ShouldReturnShortDate()
    {
        // Arrange
        var twoDaysAgo = DateTime.Now.AddDays(-2);
        var time = new DateTime(twoDaysAgo.Year, twoDaysAgo.Month, twoDaysAgo.Day, 8, 20, 0);

        // Act
        var result = time.ToMessageTimeString();

        // Assert
        Assert.Equal(time.ToShortDateString(), result);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(6, 30)]
    [InlineData(12, 0)]
    [InlineData(18, 45)]
    [InlineData(23, 59)]
    public void ToMessageTimeString_WithVariousTimes_ShouldReturnCorrectFormat(int hour, int minute)
    {
        // Arrange
        var today = DateTime.Now;
        var time = new DateTime(today.Year, today.Month, today.Day, hour, minute, 0);

        // Act
        var result = time.ToMessageTimeString();

        // Assert
        Assert.Equal($"{hour:D2}:{minute:D2}", result);
    }

    [Fact]
    public void ToMessageTimeString_WithYesterdayAtMidnight_ShouldReturnHierWithZeroTime()
    {
        // Arrange
        var yesterday = DateTime.Now.AddDays(-1);
        var midnight = new DateTime(yesterday.Year, yesterday.Month, yesterday.Day, 0, 0, 0);

        // Act
        var result = midnight.ToMessageTimeString();

        // Assert
        Assert.Equal("Hier 00:00", result);
    }

    [Fact]
    public void ToMessageTimeString_WithYesterdayAtEndOfDay_ShouldReturnHierWithTime()
    {
        // Arrange
        var yesterday = DateTime.Now.AddDays(-1);
        var endOfDay = new DateTime(yesterday.Year, yesterday.Month, yesterday.Day, 23, 59, 59);

        // Act
        var result = endOfDay.ToMessageTimeString();

        // Assert
        Assert.Equal("Hier 23:59", result);
    }

    [Fact]
    public void ToMessageTimeString_WithMinDateTime_ShouldReturnShortDate()
    {
        // Arrange
        var minDate = DateTime.MinValue;

        // Act
        var result = minDate.ToMessageTimeString();

        // Assert
        Assert.Equal(minDate.ToShortDateString(), result);
    }

    [Fact]
    public void ToMessageTimeString_WithMaxDateTime_ShouldReturnShortDate()
    {
        // Arrange
        var maxDate = DateTime.MaxValue;

        // Act
        var result = maxDate.ToMessageTimeString();

        // Assert
        Assert.Equal(maxDate.ToShortDateString(), result);
    }

    [Fact]
    public void ToMessageTimeString_MultipleCalls_ShouldReturnConsistentResults()
    {
        // Arrange
        var today = DateTime.Now;
        var time = new DateTime(today.Year, today.Month, today.Day, 15, 20, 0);

        // Act
        var result1 = time.ToMessageTimeString();
        var result2 = time.ToMessageTimeString();
        var result3 = time.ToMessageTimeString();

        // Assert
        Assert.Equal(result1, result2);
        Assert.Equal(result2, result3);
    }

    [Fact]
    public void ToMessageTimeString_WithLeapYearDate_ShouldHandleCorrectly()
    {
        // Arrange
        var leapYearDate = new DateTime(2024, 2, 29, 12, 30, 0);

        // Act
        var result = leapYearDate.ToMessageTimeString();

        // Assert
        // Si la date n'est pas aujourd'hui ou hier, retourne la date courte
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void ToMessageTimeString_WithFirstDayOfMonth_ShouldWorkCorrectly()
    {
        // Arrange
        var today = DateTime.Now;
        var firstDay = new DateTime(today.Year, today.Month, 1, 10, 0, 0);

        // Act
        var result = firstDay.ToMessageTimeString();

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void ToMessageTimeString_WithLastDayOfMonth_ShouldWorkCorrectly()
    {
        // Arrange
        var today = DateTime.Now;
        var lastDay = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month), 18, 0, 0);

        // Act
        var result = lastDay.ToMessageTimeString();

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }
}