using System.ComponentModel.DataAnnotations;
using IrcChat.Shared.Validation;

namespace IrcChat.Shared.Tests.Validation;

public class MinimumAgeAttributeTests
{
    [Theory]
    [InlineData(13, true)]
    [InlineData(18, true)]
    [InlineData(50, true)]
    [InlineData(120, true)]
    [InlineData(12, false)]
    [InlineData(5, false)]
    [InlineData(0, false)]
    [InlineData(121, false)]
    [InlineData(150, false)]
    public void IsValid_WithVariousAges_ReturnsExpectedResult(int age, bool expectedValid)
    {
        // Arrange
        var attribute = new MinimumAgeAttribute(13);
        var dateOfBirth = DateTime.UtcNow.AddYears(-age);
        var validationContext = new ValidationContext(new object()) { MemberName = "DateOfBirth" };

        // Act
        var result = attribute.GetValidationResult(dateOfBirth, validationContext);

        // Assert
        if (expectedValid)
        {
            Assert.Equal(ValidationResult.Success, result);
        }
        else
        {
            Assert.NotEqual(ValidationResult.Success, result);
        }
    }

    [Fact]
    public void IsValid_WithExactly13Years_ReturnsSuccess()
    {
        // Arrange
        var attribute = new MinimumAgeAttribute(13);
        var dateOfBirth = DateTime.UtcNow.AddYears(-13);
        var validationContext = new ValidationContext(new object()) { MemberName = "DateOfBirth" };

        // Act
        var result = attribute.GetValidationResult(dateOfBirth, validationContext);

        // Assert
        Assert.Equal(ValidationResult.Success, result);
    }

    [Fact]
    public void IsValid_WithExactly120Years_ReturnsSuccess()
    {
        // Arrange
        var attribute = new MinimumAgeAttribute(13) { MaximumAge = 120 };
        var dateOfBirth = DateTime.UtcNow.AddYears(-120);
        var validationContext = new ValidationContext(new object()) { MemberName = "DateOfBirth" };

        // Act
        var result = attribute.GetValidationResult(dateOfBirth, validationContext);

        // Assert
        Assert.Equal(ValidationResult.Success, result);
    }

    [Fact]
    public void IsValid_WithAlmost13Years_ReturnsError()
    {
        // Arrange
        var attribute = new MinimumAgeAttribute(13);
        var dateOfBirth = DateTime.UtcNow.AddYears(-13).AddDays(1);
        var validationContext = new ValidationContext(new object()) { MemberName = "DateOfBirth" };

        // Act
        var result = attribute.GetValidationResult(dateOfBirth, validationContext);

        // Assert
        Assert.NotEqual(ValidationResult.Success, result);
        Assert.Contains("L'âge minimum requis est de 13 ans", result!.ErrorMessage);
    }

    [Fact]
    public void IsValid_WithAgeLessThan13_ReturnsErrorWithMessage()
    {
        // Arrange
        var attribute = new MinimumAgeAttribute(13);
        var dateOfBirth = DateTime.UtcNow.AddYears(-10);
        var validationContext = new ValidationContext(new object()) { MemberName = "DateOfBirth" };

        // Act
        var result = attribute.GetValidationResult(dateOfBirth, validationContext);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("L'âge minimum requis est de 13 ans", result.ErrorMessage);
        Assert.Contains("DateOfBirth", result.MemberNames);
    }

    [Fact]
    public void IsValid_WithAgeGreaterThan120_ReturnsErrorWithMessage()
    {
        // Arrange
        var attribute = new MinimumAgeAttribute(13) { MaximumAge = 120 };
        var dateOfBirth = DateTime.UtcNow.AddYears(-130);
        var validationContext = new ValidationContext(new object()) { MemberName = "DateOfBirth" };

        // Act
        var result = attribute.GetValidationResult(dateOfBirth, validationContext);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("L'âge ne peut pas dépasser 120 ans", result.ErrorMessage);
        Assert.Contains("DateOfBirth", result.MemberNames);
    }

    [Fact]
    public void IsValid_WithNullValue_ReturnsError()
    {
        // Arrange
        var attribute = new MinimumAgeAttribute(13);
        var validationContext = new ValidationContext(new object()) { MemberName = "DateOfBirth" };

        // Act
        var result = attribute.GetValidationResult(null, validationContext);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("La date de naissance est requise", result.ErrorMessage);
    }

    [Fact]
    public void IsValid_WithInvalidType_ReturnsError()
    {
        // Arrange
        var attribute = new MinimumAgeAttribute(13);
        var validationContext = new ValidationContext(new object()) { MemberName = "DateOfBirth" };

        // Act
        var result = attribute.GetValidationResult("not-a-date", validationContext);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("La date de naissance doit être une date valide", result.ErrorMessage);
    }

    [Fact]
    public void IsValid_WithBirthdayToday_ReturnsCorrectValidation()
    {
        // Arrange
        var attribute = new MinimumAgeAttribute(25);
        var today = DateTime.UtcNow;
        var dateOfBirth = new DateTime(today.Year - 25, today.Month, today.Day);
        var validationContext = new ValidationContext(new object()) { MemberName = "DateOfBirth" };

        // Act
        var result = attribute.GetValidationResult(dateOfBirth, validationContext);

        // Assert
        Assert.Equal(ValidationResult.Success, result);
    }

    [Fact]
    public void IsValid_WithBirthdayYesterday_ReturnsCorrectValidation()
    {
        // Arrange
        var attribute = new MinimumAgeAttribute(30);
        var yesterday = DateTime.UtcNow.AddDays(-1);
        var dateOfBirth = new DateTime(yesterday.Year - 30, yesterday.Month, yesterday.Day);
        var validationContext = new ValidationContext(new object()) { MemberName = "DateOfBirth" };

        // Act
        var result = attribute.GetValidationResult(dateOfBirth, validationContext);

        // Assert
        Assert.Equal(ValidationResult.Success, result);
    }

    [Fact]
    public void IsValid_WithBirthdayTomorrow_ReturnsCorrectValidation()
    {
        // Arrange
        var attribute = new MinimumAgeAttribute(20);
        var tomorrow = DateTime.UtcNow.AddDays(1);
        var dateOfBirth = new DateTime(tomorrow.Year - 20, tomorrow.Month, tomorrow.Day);
        var validationContext = new ValidationContext(new object()) { MemberName = "DateOfBirth" };

        // Act
        var result = attribute.GetValidationResult(dateOfBirth, validationContext);

        // Assert
        Assert.NotEqual(ValidationResult.Success, result);
    }

    [Fact]
    public void IsValid_WithLeapYearBirthday_ReturnsCorrectValidation()
    {
        // Arrange
        var attribute = new MinimumAgeAttribute(13);
        var dateOfBirth = new DateTime(2000, 2, 29); // Année bissextile
        var validationContext = new ValidationContext(new object()) { MemberName = "DateOfBirth" };

        // Act
        var result = attribute.GetValidationResult(dateOfBirth, validationContext);

        // Assert
        var expectedAge = DateTime.UtcNow.Year - 2000;
        if (DateTime.UtcNow < new DateTime(DateTime.UtcNow.Year, 3, 1))
        {
            expectedAge--;
        }

        if (expectedAge >= 13)
        {
            Assert.Equal(ValidationResult.Success, result);
        }
        else
        {
            Assert.NotEqual(ValidationResult.Success, result);
        }
    }

    [Fact]
    public void MinimumAge_ShouldBeSetCorrectly()
    {
        // Arrange & Act
        var attribute = new MinimumAgeAttribute(18);

        // Assert
        Assert.Equal(18, attribute.MinimumAge);
    }

    [Fact]
    public void MaximumAge_ShouldDefaultTo120()
    {
        // Arrange & Act
        var attribute = new MinimumAgeAttribute(13);

        // Assert
        Assert.Equal(120, attribute.MaximumAge);
    }

    [Fact]
    public void MaximumAge_CanBeCustomized()
    {
        // Arrange & Act
        var attribute = new MinimumAgeAttribute(13) { MaximumAge = 100 };

        // Assert
        Assert.Equal(100, attribute.MaximumAge);
    }

    [Fact]
    public void IsValid_WithCustomMinimumAge_UsesCustomValue()
    {
        // Arrange
        var attribute = new MinimumAgeAttribute(21);
        var dateOfBirth = DateTime.UtcNow.AddYears(-20);
        var validationContext = new ValidationContext(new object()) { MemberName = "DateOfBirth" };

        // Act
        var result = attribute.GetValidationResult(dateOfBirth, validationContext);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("L'âge minimum requis est de 21 ans", result.ErrorMessage);
    }

    [Fact]
    public void IsValid_WithCustomMaximumAge_EnforcesCustomValue()
    {
        // Arrange
        var attribute = new MinimumAgeAttribute(13) { MaximumAge = 80 };
        var dateOfBirth = DateTime.UtcNow.AddYears(-90);
        var validationContext = new ValidationContext(new object()) { MemberName = "DateOfBirth" };

        // Act
        var result = attribute.GetValidationResult(dateOfBirth, validationContext);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("L'âge ne peut pas dépasser 80 ans", result.ErrorMessage);
    }
}