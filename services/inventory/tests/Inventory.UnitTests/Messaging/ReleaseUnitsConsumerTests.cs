using BloodLink.Inventory.Api.Features.Reservations.ReleaseUnits;
using BloodLink.Inventory.Api.Infrastructure.Messaging.Consumers;
using BloodLink.Inventory.Contracts;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace BloodLink.Inventory.UnitTests.Messaging;

public class ReleaseUnitsConsumerTests
{
    private sealed class FakeReleaseUnitsHandler : ReleaseUnitsHandler
    {
        public ReleaseUnitsRequest? HandledRequest { get; private set; }
        public int CallCount { get; private set; }

        public FakeReleaseUnitsHandler()
            : base(null!, null!, null!) { }

        public override Task<IResult> HandleAsync(
            ReleaseUnitsRequest request,
            CancellationToken cancellationToken
        )
        {
            HandledRequest = request;
            CallCount++;
            return Task.FromResult(Results.Ok());
        }
    }

    [Fact]
    public async Task Consume_ShouldReceiveCommand_AndDelegateToReleaseUnitsHandler()
    {
        var fakeHandler = new FakeReleaseUnitsHandler();

        var services = new ServiceCollection();
        services.AddSingleton<ReleaseUnitsHandler>(fakeHandler);
        services.AddMassTransitTestHarness(x =>
        {
            x.AddConsumer<ReleaseUnitsConsumer>();
        });

        await using ServiceProvider provider = services.BuildServiceProvider(true);
        ITestHarness harness = provider.GetRequiredService<ITestHarness>();

        await harness.Start();

        var requestId = Guid.NewGuid();
        List<Guid> unitIds = [Guid.NewGuid(), Guid.NewGuid()];
        var command = new ReleaseUnitsCommand(requestId, unitIds);

        await harness.Bus.Publish(command);

        IConsumerTestHarness<ReleaseUnitsConsumer> consumerHarness =
            harness.GetConsumerHarness<ReleaseUnitsConsumer>();

        (await consumerHarness.Consumed.Any<ReleaseUnitsCommand>()).ShouldBeTrue();
        fakeHandler.CallCount.ShouldBe(1);
        fakeHandler.HandledRequest.ShouldNotBeNull();
        fakeHandler.HandledRequest.RequestId.ShouldBe(requestId);
        fakeHandler.HandledRequest.UnitIds.ShouldBe(unitIds);
    }

    [Fact]
    public async Task Consume_ShouldFault_WhenHandlerReturnsProblemResult()
    {
        var fakeHandler = new FakeFailingReleaseUnitsHandler();

        var services = new ServiceCollection();
        services.AddSingleton<ReleaseUnitsHandler>(fakeHandler);
        services.AddMassTransitTestHarness(x =>
        {
            x.AddConsumer<ReleaseUnitsConsumer>();
        });

        await using ServiceProvider provider = services.BuildServiceProvider(true);
        ITestHarness harness = provider.GetRequiredService<ITestHarness>();

        await harness.Start();

        var requestId = Guid.NewGuid();
        List<Guid> unitIds = [Guid.NewGuid()];
        var command = new ReleaseUnitsCommand(requestId, unitIds);

        await harness.Bus.Publish(command);

        IConsumerTestHarness<ReleaseUnitsConsumer> consumerHarness =
            harness.GetConsumerHarness<ReleaseUnitsConsumer>();

        (await consumerHarness.Consumed.Any<ReleaseUnitsCommand>()).ShouldBeTrue();
        consumerHarness.Consumed.Select<ReleaseUnitsCommand>().Any(x => x.Exception != null).ShouldBeTrue();
        fakeHandler.CallCount.ShouldBe(1);
    }

    private sealed class FakeFailingReleaseUnitsHandler : ReleaseUnitsHandler
    {
        public int CallCount { get; private set; }

        public FakeFailingReleaseUnitsHandler()
            : base(null!, null!, null!) { }

        public override Task<IResult> HandleAsync(
            ReleaseUnitsRequest request,
            CancellationToken cancellationToken
        )
        {
            CallCount++;
            return Task.FromResult(Results.Problem(statusCode: StatusCodes.Status404NotFound));
        }
    }
}
