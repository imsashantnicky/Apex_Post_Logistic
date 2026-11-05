using ApexPost.Domain.Verification;

namespace ApexPost.Application.Abstractions;

/// <summary>Stores identity checks, which keep only the masked document number.</summary>
public interface IIdentityVerificationRepository
{
    void Add(Guid transactionId, IdentityVerification verification);
}
