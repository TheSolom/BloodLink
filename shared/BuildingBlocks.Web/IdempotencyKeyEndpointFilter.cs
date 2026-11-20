using BloodLink.BuildingBlocks.Domain;
using Microsoft.AspNetCore.Http;

namespace BloodLink.BuildingBlocks.Web;

public class IdempotencyKeyEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        if (
            !context.HttpContext.Request.Headers.TryGetValue(
                IdempotencyKey.HeaderName,
                out var values
            ) || string.IsNullOrWhiteSpace(values)
        )
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Missing Idempotency-Key",
                detail: "The 'Idempotency-Key' header is required for request submission.",
                extensions: new Dictionary<string, object?>
                {
                    ["errorCode"] = IdempotencyKey.RequiredErrorCode,
                }
            );
        }

        string rawKey = values.ToString();
        if (!IdempotencyKey.TryCreate(rawKey, out _, out string? errorCode))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid Idempotency-Key",
                detail: $"The 'Idempotency-Key' header must not exceed {IdempotencyKey.MaxLength} characters.",
                extensions: new Dictionary<string, object?> { ["errorCode"] = errorCode }
            );
        }

        return await next(context);
    }
}
