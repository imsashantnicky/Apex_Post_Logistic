namespace ApexPost.Application.Specifications;

/// <summary>A specification built from a delegate; specifications can be combined with <see cref="And"/>.</summary>
public sealed class PredicateSpecification<T>(Func<T, bool> predicate) : ISpecification<T>
{
    public bool IsSatisfiedBy(T candidate) => predicate(candidate);

    public PredicateSpecification<T> And(ISpecification<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new(candidate => predicate(candidate) && other.IsSatisfiedBy(candidate));
    }
}
