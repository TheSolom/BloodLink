using BloodLink.Requests.Domain.BloodRequests;
using Riok.Mapperly.Abstractions;

namespace BloodLink.Requests.Api.Features.Requests.SubmitRequest;

[Mapper]
public static partial class SubmitRequestMapper
{
    [MapProperty(
        nameof(BloodRequest.BloodType),
        nameof(SubmitRequestResponse.BloodType),
        Use = nameof(MapBloodTypeToString)
    )]
    [MapProperty(
        nameof(BloodRequest.Component),
        nameof(SubmitRequestResponse.Component),
        Use = nameof(MapComponentToString)
    )]
    [MapProperty(
        nameof(BloodRequest.Urgency),
        nameof(SubmitRequestResponse.Urgency),
        Use = nameof(MapUrgencyToString)
    )]
    [MapProperty(
        nameof(BloodRequest.Status),
        nameof(SubmitRequestResponse.Status),
        Use = nameof(MapStatusToString)
    )]
    public static partial SubmitRequestResponse ToResponse(BloodRequest request);

    private static string MapBloodTypeToString(Domain.ValueObjects.BloodType bloodType) =>
        bloodType.ToString();

    private static string MapComponentToString(Domain.ValueObjects.BloodComponent component) =>
        component.ToString();

    private static string MapUrgencyToString(RequestUrgency urgency) => urgency.ToString();

    private static string MapStatusToString(BloodRequestStatus status) => status.ToString();
}
