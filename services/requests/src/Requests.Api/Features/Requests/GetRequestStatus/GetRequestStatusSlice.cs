using BloodLink.BuildingBlocks.Domain;
using BloodLink.Requests.Api.Infrastructure.Auth;
using BloodLink.Requests.Api.Infrastructure.Persistence;
using BloodLink.Requests.Domain.BloodRequests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Riok.Mapperly.Abstractions;

namespace BloodLink.Requests.Api.Features.Requests.GetRequestStatus;

public record RequestStatusResponse(
    Guid Id,
    Guid HospitalId,
    string BloodType,
    string Component,
    int UnitsNeeded,
    string Urgency,
    DateTimeOffset NeededBy,
    string Status,
    string IdempotencyKey,
    IReadOnlyList<Guid> AssignedUnitIds
);

[Mapper]
public static partial class GetRequestStatusMapper
{
    [MapProperty(
        nameof(BloodRequest.BloodType),
        nameof(RequestStatusResponse.BloodType),
        Use = nameof(MapBloodTypeToString)
    )]
    [MapProperty(
        nameof(BloodRequest.Component),
        nameof(RequestStatusResponse.Component),
        Use = nameof(MapComponentToString)
    )]
    [MapProperty(
        nameof(BloodRequest.Urgency),
        nameof(RequestStatusResponse.Urgency),
        Use = nameof(MapUrgencyToString)
    )]
    [MapProperty(
        nameof(BloodRequest.Status),
        nameof(RequestStatusResponse.Status),
        Use = nameof(MapStatusToString)
    )]
    public static partial RequestStatusResponse ToResponse(BloodRequest request);

    private static string MapBloodTypeToString(Domain.ValueObjects.BloodType bt) => bt.ToString();

    private static string MapComponentToString(Domain.ValueObjects.BloodComponent c) =>
        c.ToString();

    private static string MapUrgencyToString(RequestUrgency u) => u.ToString();

    private static string MapStatusToString(BloodRequestStatus s) => s.ToString();
}

public class GetRequestStatusHandler(RequestsDbContext dbContext, IHospitalContext hospitalContext)
{
    public async Task<RequestStatusResponse> HandleAsync(
        Guid requestId,
        CancellationToken cancellationToken
    )
    {
        Guid hospitalId = hospitalContext.GetHospitalId();

        BloodRequest? request = await dbContext
            .BloodRequests.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (request == null)
        {
            throw new DomainException(
                $"Blood request '{requestId}' was not found.",
                "request.not_found"
            );
        }

        if (request.HospitalId != hospitalId)
        {
            throw new DomainException(
                "You do not have permission to view this request.",
                "request.forbidden"
            );
        }

        return GetRequestStatusMapper.ToResponse(request);
    }
}

public static class GetRequestStatusEndpoint
{
    public static IEndpointRouteBuilder MapGetRequestStatus(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet(
                "/api/v1/requests/{id:guid}",
                async (
                    [FromRoute] Guid id,
                    GetRequestStatusHandler handler,
                    CancellationToken cancellationToken
                ) =>
                {
                    RequestStatusResponse response = await handler.HandleAsync(
                        id,
                        cancellationToken
                    );
                    return Results.Ok(response);
                }
            )
            .WithName("GetRequestStatus")
            .WithSummary("Retrieves status and details of a blood request")
            .Produces<RequestStatusResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }
}
