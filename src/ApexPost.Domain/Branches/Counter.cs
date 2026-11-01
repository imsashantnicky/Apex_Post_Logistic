using ApexPost.Domain.Common;

namespace ApexPost.Domain.Branches;

/// <summary>A counter terminal inside a branch, numbered from 1.</summary>
public sealed class Counter : Entity<Guid>
{
    public Counter(Guid id, int number)
        : base(Guard.NotEmpty(id, "Counter id"))
    {
        Number = Guard.Positive(number, "Counter number");
    }

    public int Number { get; }
}
