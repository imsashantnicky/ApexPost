
namespace ApexPost.Application.Persistence;

public sealed record Versioned<T>
{
    public T Value { get; }
    public long Version { get; }

    public Versioned(T value, long version)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(version),
                "Version must be positive.");
        }

        Value = value;
        Version = version;
    }
}
