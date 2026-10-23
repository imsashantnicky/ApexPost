namespace ApexPost.Domain.Common;

/// <summary>
/// A non-empty identifier for a domain entity.
/// </summary>
public sealed record EntityId
{
    private EntityId(Guid value)
    {
        Value = value;
    }

    public Guid Value { get; }

    public static EntityId New()
    {
        return new EntityId(Guid.NewGuid());
    }

    public static EntityId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException("An entity identifier cannot be empty.");
        }

        return new EntityId(value);
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}
