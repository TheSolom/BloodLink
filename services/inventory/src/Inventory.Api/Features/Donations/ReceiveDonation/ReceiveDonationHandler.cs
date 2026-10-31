using BloodLink.Inventory.Api.Infrastructure.Middleware;
using BloodLink.Inventory.Api.Infrastructure.Persistence;
using BloodLink.Inventory.Domain.Model.BloodUnits;
using BloodLink.Inventory.Domain.ValueObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Inventory.Api.Features.Donations.ReceiveDonation;

public class ReceiveDonationHandler(InventoryDbContext dbContext, TimeProvider timeProvider)
{
    public async Task<IResult> HandleAsync(ReceiveDonationRequest request, CancellationToken cancellationToken)
    {
        var bank = await dbContext.BloodBanks.FindAsync([request.BloodBankId], cancellationToken);
        if (bank is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Blood Bank Not Found",
                detail: $"Blood bank with ID '{request.BloodBankId}' does not exist.",
                extensions: new Dictionary<string, object?> { ["code"] = "blood_bank.not_found" }
            );
        }

        var donationNumber = DonationNumber.Create(request.DonationNumber);
        var bloodType = BloodType.Parse(request.BloodType);
        if (!BloodComponentExtensions.TryParse(request.Component, out var component))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid Component",
                detail: $"Component '{request.Component}' is invalid.",
                extensions: new Dictionary<string, object?> { ["code"] = "component.invalid" }
            );
        }

        // Pre-check for duplicate donation number within the same blood bank
        bool isDuplicate = await dbContext.BloodUnits.AnyAsync(
            u => u.BloodBankId == request.BloodBankId && u.DonationNumber == donationNumber,
            cancellationToken
        );

        if (isDuplicate)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Duplicate Donation Number",
                detail: $"Donation number '{donationNumber}' already exists for blood bank '{request.BloodBankId}'.",
                extensions: new Dictionary<string, object?> { ["code"] = "donation.duplicate" }
            );
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        var unit = BloodUnit.ReceiveDonation(
            Guid.NewGuid(),
            donationNumber,
            bloodType,
            component,
            request.CollectedAt,
            request.BloodBankId,
            now
        );

        dbContext.BloodUnits.Add(unit);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (GlobalExceptionHandler.IsUniqueConstraintViolation(ex))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Duplicate Donation Number",
                detail: $"Donation number '{donationNumber}' already exists for blood bank '{request.BloodBankId}'.",
                extensions: new Dictionary<string, object?> { ["code"] = "donation.duplicate" }
            );
        }

        var response = ReceiveDonationMapper.ToResponse(unit);
        return Results.Created($"/api/v1/stock/{response.Id}", response);
    }
}
