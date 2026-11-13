using BloodLink.Inventory.Api.Features.Reservations.ReserveUnits;
using BloodLink.Inventory.Api.Infrastructure.Messaging.Consumers;
using BloodLink.Inventory.Contracts;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace BloodLink.Inventory.UnitTests.Messaging;

public class ReserveUnitsConsumerTests
{
    private sealed class FakeReserveUnitsHandler : ReserveUnitsHandler
    {
        public ReserveUnitsRequest? HandledRequest { get; private set; }
        public int CallCount { get; private set; }

        public FakeReserveUnitsHandler()
            : base(null!, TimeProvider.System, null!, null!) { }

        public override Task<IResult> HandleAsync(
            ReserveUnitsRequest request,
            CancellationToken cancellationToken
        )
        {
            HandledRequest = request;
            CallCount++;
            return Task.FromResult(Results.Ok());
        }
    }

    [Fact]
    public async Task Consume_ShouldReceiveCommand_AndDelegateToReserveUnitsHandler()
    {
        var fakeHandler = new FakeReserveUnitsHandler();

        var services = new ServiceCollection();
        services.AddSingleton<ReserveUnitsHandler>(fakeHandler);
        services.AddMassTransitTestHarness(x =>
        {
            x.AddConsumer<ReserveUnitsConsumer>();
        });

        await using ServiceProvider provider = services.BuildServiceProvider(true);
        ITestHarness harness = provider.GetRequiredService<ITestHarness>();

        await harness.Start();

        var requestId = Guid.NewGuid();
        List<Guid> unitIds = [Guid.NewGuid(), Guid.NewGuid()];
        var command = new ReserveUnitsCommand(requestId, unitIds);

        await harness.Bus.Publish(command);

        IConsumerTestHarness<ReserveUnitsConsumer> consumerHarness =
            harness.GetConsumerHarness<ReserveUnitsConsumer>();

        (await consumerHarness.Consumed.Any<ReserveUnitsCommand>()).ShouldBeTrue();
        fakeHandler.CallCount.ShouldBe(1);
        fakeHandler.HandledRequest.ShouldNotBeNull();
        fakeHandler.HandledRequest.RequestId.ShouldBe(requestId);
        fakeHandler.HandledRequest.UnitIds.ShouldBe(unitIds);
    }
}
