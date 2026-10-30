using Microsoft.Extensions.Time.Testing;

namespace ApexPost.Application.Tests;

/// <summary>A fixed instant for test data and a <see cref="FakeTimeProvider"/> starting there.</summary>
internal static class TestClock
{
    public static readonly DateTimeOffset Now = new(2026, 6, 15, 9, 30, 0, TimeSpan.Zero);

    public static FakeTimeProvider Create() => new(Now);
}
