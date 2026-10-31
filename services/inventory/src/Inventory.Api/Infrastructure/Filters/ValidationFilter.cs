using FluentValidation;
using FluentValidation.Results;

namespace BloodLink.Inventory.Api.Infrastructure.Filters;

public class ValidationFilter<TRequest> : IEndpointFilter
    where TRequest : class
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        IValidator<TRequest>? validator = context.HttpContext.RequestServices.GetService<
            IValidator<TRequest>
        >();
        if (validator is null)
        {
            return await next(context);
        }

        TRequest? request = context.Arguments.OfType<TRequest>().FirstOrDefault();
        if (request is null)
        {
            return Results.Problem(
                title: "Invalid Request Payload",
                detail: "Request payload could not be deserialized or was null.",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?> { ["code"] = "validation.null_payload" }
            );
        }

        ValidationResult validationResult = await validator.ValidateAsync(
            request,
            context.HttpContext.RequestAborted
        );
        if (!validationResult.IsValid)
        {
            var problemDetails = new HttpValidationProblemDetails(validationResult.ToDictionary())
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred.",
                Instance = context.HttpContext.Request.Path,
            };

            problemDetails.Extensions["code"] = "validation.failed";

            return Results.Problem(problemDetails);
        }

        return await next(context);
    }
}
