using BloodLink.Requests.Domain.BloodRequests;
using Riok.Mapperly.Abstractions;

namespace BloodLink.Requests.Api.Features.Requests.GetRequestStatus;

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
