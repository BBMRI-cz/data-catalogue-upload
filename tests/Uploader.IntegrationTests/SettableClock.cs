namespace Uploader.IntegrationTests;

/// <summary>A clock a test moves by hand, so a run's start and finish get distinct, known times.</summary>
internal sealed class SettableClock : TimeProvider
{
    public SettableClock(DateTimeOffset now) => Now = now;

    public DateTimeOffset Now { get; set; }

    public override DateTimeOffset GetUtcNow() => Now;
}
