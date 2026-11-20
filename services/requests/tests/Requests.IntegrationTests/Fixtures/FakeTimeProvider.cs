namespace BloodLink.Requests.IntegrationTests.Fixtures;

public class FakeTimeProvider(DateTimeOffset initialTime) : TimeProvider
{
    private DateTimeOffset _currentTime = initialTime;

    public override DateTimeOffset GetUtcNow() => _currentTime;

    public void Advance(TimeSpan duration) => _currentTime = _currentTime.Add(duration);

    public void SetUtcNow(DateTimeOffset time) => _currentTime = time;
}
