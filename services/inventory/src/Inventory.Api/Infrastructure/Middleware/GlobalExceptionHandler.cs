using BloodLink.BuildingBlocks.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Inventory.Api.Infrastructure.Middleware;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception occurred: {Message}", exception.Message);

        var (statusCode, title, code) = MapException(exception);

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = exception.Message,
            Instance = httpContext.Request.Path
        };

        problemDetails.Extensions["code"] = code;

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }

    private static (int StatusCode, string Title, string Code) MapException(Exception exception)
    {
        if (exception is DomainException domainException)
        {
            return (
                StatusCodes.Status422UnprocessableEntity,
                "Domain Rule Violation",
                domainException.Code ?? "domain.rule_violation"
            );
        }

        if (exception is DbUpdateException dbUpdateException && IsUniqueConstraintViolation(dbUpdateException))
        {
            return (
                StatusCodes.Status409Conflict,
                "Conflict",
                "donation.duplicate"
            );
        }

        return (
            StatusCodes.Status500InternalServerError,
            "Internal Server Error",
            "server.unexpected_error"
        );
    }

    public static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        if (ex.InnerException is SqlException sqlEx)
        {
            // 2601: Cannot insert duplicate key row in object with unique index
            // 2627: Violation of PRIMARY KEY or UNIQUE KEY constraint
            return sqlEx.Number is 2601 or 2627;
        }

        return false;
    }
}
