namespace ApexPost.Application.Specifications;

/// <summary>A reusable filter condition, for example "paid transactions of BR001 in October".</summary>
public interface ISpecification<in T>
{
    bool IsSatisfiedBy(T candidate);
}
