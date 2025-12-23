using System.ComponentModel.DataAnnotations;

namespace IrcChat.Shared.Validation;

/// <summary>
/// Validates that a date of birth represents an age greater than or equal to the minimum age.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class MinimumAgeAttribute : ValidationAttribute
{
    /// <summary>
    /// Gets the minimum age required.
    /// </summary>
    public int MinimumAge { get; }

    /// <summary>
    /// Gets the maximum age allowed (optional, default is 120).
    /// </summary>
    public int MaximumAge { get; set; } = 120;

    /// <summary>
    /// Initializes a new instance of the <see cref="MinimumAgeAttribute"/> class.
    /// </summary>
    /// <param name="minimumAge">The minimum age required.</param>
    public MinimumAgeAttribute(int minimumAge)
    {
        MinimumAge = minimumAge;
        ErrorMessage = $"L'âge minimum requis est de {minimumAge} ans";
    }

    /// <summary>
    /// Validates the specified value with respect to the current validation attribute.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="validationContext">The context information about the validation operation.</param>
    /// <returns>An instance of the <see cref="ValidationResult"/> class.</returns>
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null)
        {
            return new ValidationResult("La date de naissance est requise");
        }

        if (value is not DateTime dateOfBirth)
        {
            return new ValidationResult("La date de naissance doit être une date valide");
        }

        var age = CalculateAge(dateOfBirth);

        if (age < MinimumAge)
        {
            return new ValidationResult(
                ErrorMessage ?? $"L'âge minimum requis est de {MinimumAge} ans",
                validationContext.MemberName != null ? [validationContext.MemberName] : null);
        }

        if (age > MaximumAge)
        {
            return new ValidationResult(
                $"L'âge ne peut pas dépasser {MaximumAge} ans",
                validationContext.MemberName != null ? [validationContext.MemberName] : null);
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// Calculates the age in years from a date of birth.
    /// </summary>
    /// <param name="dateOfBirth">The date of birth.</param>
    /// <returns>The age in years.</returns>
    private static int CalculateAge(DateTime dateOfBirth)
    {
        var today = DateTime.UtcNow;
        var age = today.Year - dateOfBirth.Year;

        if (dateOfBirth.Date > today.AddYears(-age))
        {
            age--;
        }

        return age;
    }
}