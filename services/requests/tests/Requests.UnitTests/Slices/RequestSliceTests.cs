using BloodLink.BuildingBlocks.Domain;
using BloodLink.Requests.Api.Features.Requests.CancelRequest;
using BloodLink.Requests.Api.Features.Requests.GetRequestStatus;
using BloodLink.Requests.Api.Features.Requests.ListRequests;
using BloodLink.Requests.Api.Features.Requests.SubmitRequest;
using BloodLink.Requests.Api.Infrastructure.Auth;
using BloodLink.Requests.Api.Infrastructure.Persistence;
using BloodLink.Requests.Domain.BloodRequests;
using BloodLink.Requests.Domain.Errors;
using BloodLink.Requests.UnitTests.Fixtures;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace BloodLink.Requests.UnitTests.Slices;

public class RequestSliceTests
{
    private static readonly Guid HospitalAId = SeededHospitals.StThomas.Id;
    private static readonly Guid HospitalBId = SeededHospitals.RoyalLondon.Id;

    private static (RequestsDbContext DbContext, FakeTimeProvider TimeProvider) CreateContext()
    {
        DbContextOptions<RequestsDbContext> options =
            new DbContextOptionsBuilder<RequestsDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

        var dbContext = new RequestsDbContext(options);
        dbContext.Hospitals.AddRange(
            SeededHospitals.StThomas,
            SeededHospitals.RoyalLondon,
            SeededHospitals.KingsCollege
        );
        dbContext.SaveChanges();

        var timeProvider = new FakeTimeProvider(
            new DateTimeOffset(2026, 11, 15, 12, 0, 0, TimeSpan.Zero)
        );
        return (dbContext, timeProvider);
    }

    [Fact]
    public async Task SubmitRequest_ValidCommand_CreatesRequest()
    {
        // Arrange
        (RequestsDbContext? dbContext, FakeTimeProvider? timeProvider) = CreateContext();
        var hospitalContext = new TestHospitalContext(HospitalAId);
        var handler = new SubmitRequestHandler(dbContext, hospitalContext, timeProvider);

        var command = new SubmitRequestCommand(
            "O+",
            "RedCells",
            UnitsNeeded: 3,
            "Urgent",
            NeededBy: timeProvider.GetUtcNow().AddHours(4)
        );

        // Act
        SubmitRequestResponse response = await handler.HandleAsync(
            command,
            "key-123",
            CancellationToken.None
        );

        // Assert
        response.ShouldNotBeNull();
        response.Id.ShouldNotBe(Guid.Empty);
        response.HospitalId.ShouldBe(HospitalAId);
        response.BloodType.ShouldBe("O+");
        response.Component.ShouldBe("RedCells");
        response.UnitsNeeded.ShouldBe(3);
        response.Urgency.ShouldBe("Urgent");
        response.Status.ShouldBe("Submitted");
        response.IdempotencyKey.ShouldBe("key-123");

        BloodRequest? stored = await dbContext.BloodRequests.FindAsync(response.Id);
        stored.ShouldNotBeNull();
        stored.Status.ShouldBe(BloodRequestStatus.Submitted);
    }

    [Fact]
    public async Task SubmitRequest_DuplicateIdempotencyKey_ReturnsOriginalRequestWithoutCreatingNew()
    {
        // Arrange
        (RequestsDbContext? dbContext, FakeTimeProvider? timeProvider) = CreateContext();
        var hospitalContext = new TestHospitalContext(HospitalAId);
        var handler = new SubmitRequestHandler(dbContext, hospitalContext, timeProvider);

        var command = new SubmitRequestCommand(
            "A-",
            "Platelets",
            UnitsNeeded: 2,
            "Routine",
            NeededBy: timeProvider.GetUtcNow().AddHours(8)
        );

        // Act: First submission
        SubmitRequestResponse firstResponse = await handler.HandleAsync(
            command,
            "idemp-key-repeat",
            CancellationToken.None
        );

        // Act: Second submission with same idempotency key
        SubmitRequestResponse secondResponse = await handler.HandleAsync(
            command,
            "idemp-key-repeat",
            CancellationToken.None
        );

        // Assert
        secondResponse.Id.ShouldBe(firstResponse.Id);
        secondResponse.IdempotencyKey.ShouldBe(firstResponse.IdempotencyKey);

        var totalForHospital = await dbContext.BloodRequests.CountAsync(r =>
            r.HospitalId == HospitalAId
        );
        totalForHospital.ShouldBe(1);
    }

    [Fact]
    public async Task SubmitRequest_MissingIdempotencyKey_ThrowsDomainException()
    {
        // Arrange
        (RequestsDbContext? dbContext, FakeTimeProvider? timeProvider) = CreateContext();
        var hospitalContext = new TestHospitalContext(HospitalAId);
        var handler = new SubmitRequestHandler(dbContext, hospitalContext, timeProvider);

        var command = new SubmitRequestCommand(
            "O-",
            "RedCells",
            UnitsNeeded: 1,
            "Critical",
            NeededBy: timeProvider.GetUtcNow().AddHours(1)
        );

        // Act & Assert
        DomainException ex = await Should.ThrowAsync<DomainException>(() =>
            handler.HandleAsync(command, "", CancellationToken.None)
        );

        ex.Code.ShouldBe(DomainErrors.IdempotencyKeyRequired);
    }

    [Fact]
    public async Task SubmitRequest_IdempotencyKeyTooLong_ThrowsDomainException()
    {
        // Arrange
        (RequestsDbContext? dbContext, FakeTimeProvider? timeProvider) = CreateContext();
        var hospitalContext = new TestHospitalContext(HospitalAId);
        var handler = new SubmitRequestHandler(dbContext, hospitalContext, timeProvider);

        var command = new SubmitRequestCommand(
            "O-",
            "RedCells",
            UnitsNeeded: 1,
            "Critical",
            NeededBy: timeProvider.GetUtcNow().AddHours(1)
        );

        string longKey = new string('x', 129);

        // Act & Assert
        DomainException ex = await Should.ThrowAsync<DomainException>(() =>
            handler.HandleAsync(command, longKey, CancellationToken.None)
        );

        ex.Code.ShouldBe(DomainErrors.IdempotencyKeyTooLong);
    }

    [Fact]
    public void SubmitRequestValidator_RejectsNumericOrUndefinedStrings()
    {
        var validator = new SubmitRequestValidator();

        var invalidCommand = new SubmitRequestCommand(
            "O-",
            "0", // numeric string
            UnitsNeeded: 2,
            "0", // numeric string
            NeededBy: DateTimeOffset.UtcNow.AddHours(2)
        );

        ValidationResult result = validator.Validate(invalidCommand);
        result.IsValid.ShouldBeFalse();
        result
            .Errors.Any(e => e.PropertyName == nameof(SubmitRequestCommand.Component))
            .ShouldBeTrue();
        result
            .Errors.Any(e => e.PropertyName == nameof(SubmitRequestCommand.Urgency))
            .ShouldBeTrue();
    }

    [Fact]
    public async Task CancelRequest_BeforeInTransit_SuccessfullyCancels()
    {
        // Arrange
        (RequestsDbContext? dbContext, FakeTimeProvider? timeProvider) = CreateContext();
        var hospitalContext = new TestHospitalContext(HospitalAId);
        var submitHandler = new SubmitRequestHandler(dbContext, hospitalContext, timeProvider);
        var cancelHandler = new CancelRequestHandler(dbContext, hospitalContext);

        var command = new SubmitRequestCommand(
            "B+",
            "RedCells",
            UnitsNeeded: 1,
            "Routine",
            NeededBy: timeProvider.GetUtcNow().AddHours(5)
        );

        SubmitRequestResponse submitted = await submitHandler.HandleAsync(
            command,
            "cancel-key-1",
            CancellationToken.None
        );

        // Act
        await cancelHandler.HandleAsync(
            submitted.Id,
            new CancelRequestCommand("Patient surgery rescheduled"),
            CancellationToken.None
        );

        // Assert
        BloodRequest? stored = await dbContext.BloodRequests.FindAsync(submitted.Id);
        stored.ShouldNotBeNull();
        stored.Status.ShouldBe(BloodRequestStatus.Cancelled);
    }

    [Fact]
    public async Task CancelRequest_OnceInTransit_ThrowsDomainException()
    {
        // Arrange
        (RequestsDbContext? dbContext, FakeTimeProvider? timeProvider) = CreateContext();
        var hospitalContext = new TestHospitalContext(HospitalAId);
        var submitHandler = new SubmitRequestHandler(dbContext, hospitalContext, timeProvider);
        var cancelHandler = new CancelRequestHandler(dbContext, hospitalContext);

        var command = new SubmitRequestCommand(
            "AB-",
            "RedCells",
            UnitsNeeded: 1,
            "Routine",
            NeededBy: timeProvider.GetUtcNow().AddHours(5)
        );

        SubmitRequestResponse submitted = await submitHandler.HandleAsync(
            command,
            "cancel-transit-key",
            CancellationToken.None
        );

        // Advance aggregate to InTransit
        BloodRequest? request = await dbContext.BloodRequests.FindAsync(submitted.Id);
        request.ShouldNotBeNull();
        request.StartMatching();
        request.StartReserving([Guid.NewGuid()]);
        request.StartDispatching();
        request.MarkInTransit();
        await dbContext.SaveChangesAsync();

        // Act & Assert
        DomainException ex = await Should.ThrowAsync<DomainException>(() =>
            cancelHandler.HandleAsync(
                submitted.Id,
                new CancelRequestCommand("Try to cancel"),
                CancellationToken.None
            )
        );

        ex.Code.ShouldBe(RequestsDomainErrors.BloodRequest.CannotCancelInTransit);
    }

    [Fact]
    public async Task CancelRequest_ByDifferentHospital_ThrowsForbiddenException()
    {
        // Arrange
        (RequestsDbContext? dbContext, FakeTimeProvider? timeProvider) = CreateContext();
        var hospitalAContext = new TestHospitalContext(HospitalAId);
        var hospitalBContext = new TestHospitalContext(HospitalBId);
        var submitHandler = new SubmitRequestHandler(dbContext, hospitalAContext, timeProvider);
        var cancelHandler = new CancelRequestHandler(dbContext, hospitalBContext);

        var command = new SubmitRequestCommand(
            "O+",
            "RedCells",
            UnitsNeeded: 1,
            "Routine",
            NeededBy: timeProvider.GetUtcNow().AddHours(5)
        );

        SubmitRequestResponse submitted = await submitHandler.HandleAsync(
            command,
            "hospital-isolation-key",
            CancellationToken.None
        );

        // Act & Assert
        DomainException ex = await Should.ThrowAsync<DomainException>(() =>
            cancelHandler.HandleAsync(
                submitted.Id,
                new CancelRequestCommand("Unauthorized attempt"),
                CancellationToken.None
            )
        );

        ex.Code.ShouldBe(RequestsDomainErrors.BloodRequest.Forbidden);
    }

    [Fact]
    public async Task GetRequestStatus_ReturnsDetailsForOwner_AndForbidsOtherHospital()
    {
        // Arrange
        (RequestsDbContext? dbContext, FakeTimeProvider? timeProvider) = CreateContext();
        var hospitalAContext = new TestHospitalContext(HospitalAId);
        var hospitalBContext = new TestHospitalContext(HospitalBId);
        var submitHandler = new SubmitRequestHandler(dbContext, hospitalAContext, timeProvider);
        var statusHandlerA = new GetRequestStatusHandler(dbContext, hospitalAContext);
        var statusHandlerB = new GetRequestStatusHandler(dbContext, hospitalBContext);

        var command = new SubmitRequestCommand(
            "O-",
            "RedCells",
            UnitsNeeded: 2,
            "Critical",
            NeededBy: timeProvider.GetUtcNow().AddHours(1)
        );

        SubmitRequestResponse submitted = await submitHandler.HandleAsync(
            command,
            "status-check-key",
            CancellationToken.None
        );

        // Act: Owner gets status
        RequestStatusResponse? status = await statusHandlerA.HandleAsync(
            submitted.Id,
            CancellationToken.None
        );

        // Assert
        status.ShouldNotBeNull();
        status.Id.ShouldBe(submitted.Id);
        status.HospitalId.ShouldBe(HospitalAId);
        status.BloodType.ShouldBe("O-");
        status.Status.ShouldBe("Submitted");

        // Act & Assert: Missing request returns null
        RequestStatusResponse? missingStatus = await statusHandlerA.HandleAsync(
            Guid.NewGuid(),
            CancellationToken.None
        );
        missingStatus.ShouldBeNull();

        // Act & Assert: Other hospital forbidden
        DomainException ex = await Should.ThrowAsync<DomainException>(() =>
            statusHandlerB.HandleAsync(submitted.Id, CancellationToken.None)
        );

        ex.Code.ShouldBe(RequestsDomainErrors.BloodRequest.Forbidden);
    }

    [Fact]
    public async Task ListRequests_ReturnsOnlyHospitalOwnRequests_WithPagination()
    {
        // Arrange
        (RequestsDbContext? dbContext, FakeTimeProvider? timeProvider) = CreateContext();
        var hospitalAContext = new TestHospitalContext(HospitalAId);
        var hospitalBContext = new TestHospitalContext(HospitalBId);
        var submitHandlerA = new SubmitRequestHandler(dbContext, hospitalAContext, timeProvider);
        var submitHandlerB = new SubmitRequestHandler(dbContext, hospitalBContext, timeProvider);
        var listHandlerA = new ListRequestsHandler(dbContext, hospitalAContext);

        // Submit 3 requests for Hospital A
        for (int i = 1; i <= 3; i++)
        {
            await submitHandlerA.HandleAsync(
                new SubmitRequestCommand(
                    "O+",
                    "RedCells",
                    i,
                    "Routine",
                    timeProvider.GetUtcNow().AddHours(i + 1)
                ),
                $"hospA-req-{i}",
                CancellationToken.None
            );
        }

        // Submit 2 requests for Hospital B
        for (int i = 1; i <= 2; i++)
        {
            await submitHandlerB.HandleAsync(
                new SubmitRequestCommand(
                    "A+",
                    "RedCells",
                    i,
                    "Urgent",
                    timeProvider.GetUtcNow().AddHours(i + 1)
                ),
                $"hospB-req-{i}",
                CancellationToken.None
            );
        }

        // Act: List page 1 with pageSize 2 for Hospital A
        ListRequestsResponse response = await listHandlerA.HandleAsync(
            new ListRequestsQuery(Page: 1, PageSize: 2),
            CancellationToken.None
        );

        // Assert
        response.TotalCount.ShouldBe(3);
        response.Items.Count.ShouldBe(2);
        response.Items.All(r => r.HospitalId == HospitalAId).ShouldBeTrue();
    }
}
