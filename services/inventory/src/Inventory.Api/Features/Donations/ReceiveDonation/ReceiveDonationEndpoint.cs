using BloodLink.Inventory.Api.Infrastructure.Filters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BloodLink.Inventory.Api.Features.Donations.ReceiveDonation;

public static class ReceiveDonationEndpoint
{
    public static IEndpointRouteBuilder MapReceiveDonation(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/donations", async (
            ReceiveDonationRequest request,
            ReceiveDonationHandler handler,
            CancellationToken cancellationToken) =>
        {
            return await handler.HandleAsync(request, cancellationToken);
        })
        .AddEndpointFilter<ValidationFilter<ReceiveDonationRequest>>()
        .WithName("ReceiveDonation")
        .WithSummary("Records receipt of a donated blood unit")
        .Produces<ReceiveDonationResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return endpoints;
    }
}
