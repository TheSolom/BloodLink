using BloodLink.BuildingBlocks.Domain;
using BloodLink.Requests.Api.Infrastructure.Auth;
using BloodLink.Requests.Api.Infrastructure.Persistence;
using BloodLink.Requests.Domain.BloodRequests;
using BloodLink.Requests.Domain.Hospitals;
using BloodLink.Requests.Domain.ValueObjects;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Requests.Api.Features.Requests.SubmitRequest;

public class SubmitRequestHandler(
    RequestsDbContext dbContext,
    IHospitalContext hospitalContext,
    TimeProvider timeProvider
)
{
    public async Task<SubmitRequestResponse> HandleAsync(
        SubmitRequestCommand command,
        string idempotencyKey,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new DomainException(
                "Idempotency-Key header is required.",
                "request.idempotency_key_required"
            );
        }

        Guid hospitalId = hospitalContext.GetHospitalId();
        string cleanKey = idempotencyKey.Trim();

        // 1. Idempotency check: if request already exists for this hospital & key, return original request
        BloodRequest? existing = await dbContext
            .BloodRequests.AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.HospitalId == hospitalId && r.IdempotencyKey == cleanKey,
                cancellationToken
            );

        if (existing != null)
        {
            return SubmitRequestMapper.ToResponse(existing);
        }

        // 2. Fetch hospital reference data
        Hospital? hospital = await dbContext
            .Hospitals.AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == hospitalId, cancellationToken);

        if (hospital == null)
        {
            throw new DomainException(
                $"Hospital '{hospitalId}' was not found in reference data.",
                "hospital.not_found"
            );
        }

        var bloodType = BloodType.Parse(command.BloodType);
        BloodComponent component = Enum.Parse<BloodComponent>(command.Component, ignoreCase: true);
        RequestUrgency urgency = Enum.Parse<RequestUrgency>(command.Urgency, ignoreCase: true);

        var details = new BloodRequestDetails(
            bloodType,
            component,
            command.UnitsNeeded,
            urgency,
            command.NeededBy
        );

        var request = BloodRequest.Submit(
            Guid.NewGuid(),
            hospitalId,
            hospital.Location,
            details,
            cleanKey,
            timeProvider
        );

        try
        {
            dbContext.BloodRequests.Add(request);
            await dbContext.SaveChangesAsync(cancellationToken);
            return SubmitRequestMapper.ToResponse(request);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // 3. Concurrent-duplicate race condition: another request with the same idempotency key committed first.
            // Query the winning record and return it.
            BloodRequest winner = await dbContext
                .BloodRequests.AsNoTracking()
                .FirstAsync(
                    r => r.HospitalId == hospitalId && r.IdempotencyKey == cleanKey,
                    cancellationToken
                );

            return SubmitRequestMapper.ToResponse(winner);
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        if (
            ex.InnerException is SqlException sqlEx
            && (sqlEx.Number == 2601 || sqlEx.Number == 2627)
        )
        {
            return true;
        }

        return ex.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase);
    }
}
