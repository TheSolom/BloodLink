using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace BloodLink.BuildingBlocks.Web;

public static class IdempotencyKeyExtensions
{
    public static RouteHandlerBuilder RequireIdempotencyKey(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter<IdempotencyKeyEndpointFilter>();
}
