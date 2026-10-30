using ApexPost.Application.Auditing;
using Microsoft.Extensions.Time.Testing;

namespace ApexPost.Web.Tests;

/// <summary>Captures audit entries in memory; the real <see cref="AuditTrail"/> still validates them.</summary>
internal sealed class InMemoryAuditStore : IAuditStore
{
    private readonly List<AuditEntry> entries = [];

    public Exception? Failure { get; init; }

    public IReadOnlyList<AuditEntry> Entries
    {
        get { lock (entries) return entries.ToArray(); }
    }

    public Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        if (Failure is not null) return Task.FromException(Failure);
        lock (entries) entries.Add(entry);
        return Task.CompletedTask;
    }

    public AuditTrail Trail(TimeProvider? timeProvider = null) => new(this, timeProvider ?? new FakeTimeProvider());
}
