using ApexPost.Domain.Common;

namespace ApexPost.Domain.Branches;

/// <summary>A postal branch with opening hours, an open/closed status and at least one counter.</summary>
public sealed class Branch : Entity<BranchCode>
{
    private const string CounterRequired = "A branch needs at least one counter.";

    private readonly List<Counter> _counters;

    public Branch(
        BranchCode code,
        string name,
        string region,
        TimeOnly opensAt,
        TimeOnly closesAt,
        IEnumerable<Counter> counters,
        bool isActive = true)
        : base(code)
    {
        Name = Guard.NotBlank(name, nameof(Name), TextLimits.Name);
        Region = Guard.NotBlank(region, nameof(Region), TextLimits.Name);
        OpensAt = opensAt;
        ClosesAt = opensAt < closesAt
            ? closesAt
            : throw new DomainException("A branch must open before it closes.");
        _counters = ValidCounters(Guard.Required(counters, nameof(Counters)));
        IsActive = isActive;
    }

    // Only for persistence frameworks; they fill the properties from stored values.
    private Branch()
    {
        Name = null!;
        Region = null!;
        _counters = [];
    }

    public BranchCode Code => Id;

    public string Name { get; }

    public string Region { get; }

    public TimeOnly OpensAt { get; }

    public TimeOnly ClosesAt { get; }

    /// <summary>True while the branch is open for business; a closed branch accepts no transactions.</summary>
    public bool IsActive { get; private set; }

    public IReadOnlyList<Counter> Counters => _counters.AsReadOnly();

    public void EnsureOpen()
    {
        if (!IsActive)
        {
            throw new DomainException($"Branch {Code} is closed and cannot accept transactions.");
        }
    }

    public void Close()
    {
        if (!IsActive)
        {
            throw new DomainException($"Branch {Code} is already closed.");
        }

        IsActive = false;
    }

    public void Reopen()
    {
        if (IsActive)
        {
            throw new DomainException($"Branch {Code} is already open.");
        }

        IsActive = true;
    }

    /// <summary>Adds a counter with the next free number.</summary>
    public Counter AddCounter(Guid id)
    {
        var counter = new Counter(id, _counters.Max(existing => existing.Number) + 1);
        if (_counters.Contains(counter))
        {
            throw new DomainException("This counter already exists.");
        }

        _counters.Add(counter);
        return counter;
    }

    public void RemoveCounter(Guid id)
    {
        var counter = _counters.Find(existing => existing.Id == id)
            ?? throw new DomainException($"Branch {Code} has no such counter.");
        if (_counters.Count == 1)
        {
            throw new DomainException(CounterRequired);
        }

        _counters.Remove(counter);
    }

    private static List<Counter> ValidCounters(IEnumerable<Counter> counters)
    {
        List<Counter> list = [.. counters];
        if (list.Count == 0)
        {
            throw new DomainException(CounterRequired);
        }

        var isUnique = list.Distinct().Count() == list.Count
            && list.Select(counter => counter.Number).Distinct().Count() == list.Count;
        return isUnique ? list : throw new DomainException("Counters within a branch must be unique.");
    }
}
