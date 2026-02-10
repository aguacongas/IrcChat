using System.ComponentModel.DataAnnotations;

namespace IrcChat.Api.Filter;

public class DataAnnotationsValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        // On récupère le DTO passé à l’endpoint
        var model = context.Arguments.OfType<T>().FirstOrDefault();
        if (model is null)
        {
            return Results.BadRequest("Invalid request payload");
        }

        // Validation DataAnnotations
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model);

        if (!Validator.TryValidateObject(model, validationContext, validationResults, true))
        {
            var errors = validationResults
                .GroupBy(v => v.MemberNames.FirstOrDefault() ?? "")
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(v => v.ErrorMessage).ToArray()
                );

#pragma warning disable CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types.
            return Results.ValidationProblem(errors);
#pragma warning restore CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types.
        }

        return await next(context);
    }
}