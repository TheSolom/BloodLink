using BloodLink.Inventory.Api.Infrastructure.Filters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BloodLink.Inventory.Api.Features.BloodBanks.RegisterBloodBank;

public static class RegisterBloodBankEndpoint
{
    public static IEndpointRouteBuilder MapRegisterBloodBank(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/banks", async (
            RegisterBloodBankRequest request,
            RegisterBloodBankHandler handler,
            CancellationToken cancellationToken) =>
        {
            var response = await handler.HandleAsync(request, cancellationToken);
            return Results.Created($"/api/v1/banks/{response.Id}", response);
        })
        .AddEndpointFilter<ValidationFilter<RegisterBloodBankRequest>>()
        .WithName("RegisterBloodBank")
        .WithSummary("Registers a new blood bank")
        .Produces<RegisterBloodBankResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        return endpoints;
    }
}
