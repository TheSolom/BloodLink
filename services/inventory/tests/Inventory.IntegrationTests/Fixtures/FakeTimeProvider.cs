namespace BloodLink.Inventory.IntegrationTests.Fixtures;

public class FakeTimeProvider(DateTimeOffset initialTime) : TimeProvider
{
    private DateTimeOffset _utcNow = initialTime;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void SetUtcNow(DateTimeOffset time) => _utcNow = time;

    public void Advance(TimeSpan timeSpan) => _utcNow = _utcNow.Add(timeSpan);
}

