using ApexPost.Domain.Common;

namespace ApexPost.Domain.Pricing;

public sealed class PricingRule
{
    public PricingRule(EntityId id, EntityId serviceId, ServicePrice price, DateOnly effectiveFrom, DateOnly? effectiveTo)
    {
        Id = id ?? throw new DomainException("Pricing rule identity is required.");
        ServiceId = serviceId ?? throw new DomainException("Service identity is required.");
        Price = price ?? throw new DomainException("Service price is required.");
        SetEffectivePeriod(effectiveFrom, effectiveTo);
    }

    public EntityId Id { get; }

    public EntityId ServiceId { get; }

    public ServicePrice Price { get; private set; }

    public DateOnly EffectiveFrom { get; private set; }

    public DateOnly? EffectiveTo { get; private set; }

    public bool IsEffectiveOn(DateOnly date)
    {
        return date >= EffectiveFrom && (EffectiveTo is null || date <= EffectiveTo.Value);
    }

    public void ChangePrice(ServicePrice price)
    {
        Price = price ?? throw new DomainException("Service price is required.");
    }

    public void ChangeEffectivePeriod(DateOnly effectiveFrom, DateOnly? effectiveTo)
    {
        SetEffectivePeriod(effectiveFrom, effectiveTo);
    }

    private void SetEffectivePeriod(DateOnly effectiveFrom, DateOnly? effectiveTo)
    {
        if (effectiveTo is not null && effectiveTo.Value < effectiveFrom)
        {
            throw new DomainException("Pricing rule end date cannot be before its start date.");
        }

        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
    }
}
