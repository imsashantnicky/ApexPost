using ApexPost.Domain.Common;

namespace ApexPost.Domain.Branches;

public sealed class Branch
{
    public Branch(EntityId id, BranchCode code, string name, BranchAddress address, int counterCount)
    {
        Id = id ?? throw new DomainException("Branch identity is required.");
        Code = code ?? throw new DomainException("Branch code is required.");
        Name = RequiredName(name);
        Address = address ?? throw new DomainException("Branch address is required.");
        CounterCount = ValidCounterCount(counterCount);
        Status = BranchStatus.Active;
    }

    public EntityId Id { get; }

    public BranchCode Code { get; }

    public string Name { get; private set; }

    public BranchAddress Address { get; private set; }

    public int CounterCount { get; private set; }

    public BranchStatus Status { get; private set; }

    public void Rename(string name)
    {
        EnsureNotClosed();
        Name = RequiredName(name);
    }

    public void ChangeAddress(BranchAddress address)
    {
        EnsureNotClosed();
        Address = address ?? throw new DomainException("Branch address is required.");
    }

    public void SetCounterCount(int counterCount)
    {
        EnsureNotClosed();
        CounterCount = ValidCounterCount(counterCount);
    }

    public void Suspend()
    {
        if (Status != BranchStatus.Active)
        {
            throw new DomainException("Only an active branch can be suspended.");
        }

        Status = BranchStatus.Suspended;
    }

    public void Activate()
    {
        if (Status != BranchStatus.Suspended)
        {
            throw new DomainException("Only a suspended branch can be activated.");
        }

        Status = BranchStatus.Active;
    }

    public void Close()
    {
        EnsureNotClosed();
        Status = BranchStatus.Closed;
    }

    private static string RequiredName(string name)
    {
        var normalizedName = name?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedName) || normalizedName.Length > 120)
        {
            throw new DomainException("Branch name is required and must not exceed 120 characters.");
        }

        return normalizedName;
    }

    private static int ValidCounterCount(int counterCount)
    {
        if (counterCount < 1)
        {
            throw new DomainException("A branch must have at least one counter.");
        }

        return counterCount;
    }

    private void EnsureNotClosed()
    {
        if (Status == BranchStatus.Closed)
        {
            throw new DomainException("A closed branch cannot be changed.");
        }
    }
}
