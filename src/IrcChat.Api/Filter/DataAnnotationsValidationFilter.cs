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

            return Results.ValidationProblem(errors);
        }

        return await next(context);
    }
}