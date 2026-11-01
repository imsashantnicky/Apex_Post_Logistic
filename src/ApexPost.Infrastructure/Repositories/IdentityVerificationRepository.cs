using ApexPost.Application.Abstractions;
using ApexPost.Domain.Verification;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Persistence.Mappers;

namespace ApexPost.Infrastructure.Repositories;

public sealed class IdentityVerificationRepository(ApexPostDbContext database) : IIdentityVerificationRepository
{
    public void Add(Guid transactionId, IdentityVerification verification) =>
        database.Add(IdentityVerificationMapper.ToRecord(transactionId, verification));
}
