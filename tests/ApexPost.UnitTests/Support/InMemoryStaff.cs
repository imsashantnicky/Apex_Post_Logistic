using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Specifications;
using ApexPost.Domain.Personnel;

namespace ApexPost.UnitTests.Support;

/// <summary>The staff ports on the in-memory store; emails ignore case, as in the database.</summary>
internal sealed class InMemoryStaff(InMemoryRepository<Staff, Guid> store, FakeUnitOfWork unitOfWork)
    : IStaffRepository, IStaffQueries
{
    public Task<Staff?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        store.GetAsync(id, cancellationToken);

    public async Task<Staff?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var matches = await store.ListAsync(
            new PredicateSpecification<Staff>(member => HasEmail(member, email)),
            cancellationToken);
        return matches is [var first, ..] ? first : null;
    }

    public async Task<bool> EmailTakenAsync(string email, Guid? ownId, CancellationToken cancellationToken)
    {
        var taken = new PredicateSpecification<Staff>(member => member.Id != ownId && HasEmail(member, email));
        return (await store.ListAsync(taken, cancellationToken)).Count > 0;
    }

    public void Add(Staff member) => unitOfWork.Stage(token => store.AddAsync(member, token));

    public void Update(Staff member, int expectedVersion) =>
        unitOfWork.Stage(token => store.UpdateAsync(member, expectedVersion, token));

    public async Task<IReadOnlyList<StaffSummary>> ListAsync(CancellationToken cancellationToken)
    {
        var all = await store.ListAsync(cancellationToken);
        return
        [
            .. all
                .OrderBy(member => member.HomeBranch.Value, StringComparer.Ordinal)
                .ThenBy(member => member.Name, StringComparer.OrdinalIgnoreCase)
                .Select(StaffSummary.From),
        ];
    }

    public async Task<StaffSummary?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await store.GetAsync(id, cancellationToken) is { } member ? StaffSummary.From(member) : null;

    private static bool HasEmail(Staff member, string email) =>
        string.Equals(member.Email, email, StringComparison.OrdinalIgnoreCase);
}
