using ApexPost.Application.Abstractions;
using ApexPost.Infrastructure.Persistence.Json;
using ApexPost.Infrastructure.Persistence.Json.Records;

namespace ApexPost.Infrastructure.Security;

/// <summary>Password hashes in credentials.json, apart from the staff records.</summary>
public sealed class JsonCredentialStore(JsonFileStore files) : ICredentialStore
{
    private const int FirstVersion = 1;

    public async Task<string?> GetHashAsync(Guid staffId, CancellationToken cancellationToken)
    {
        var records = await files.ReadAsync<CredentialRecord>(DataFiles.Credentials, cancellationToken)
            .ConfigureAwait(false);
        return records.Find(record => record.StaffId == staffId)?.PasswordHash;
    }

    public Task SetHashAsync(Guid staffId, string passwordHash, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        return files.ChangeAsync<CredentialRecord>(
            DataFiles.Credentials,
            records =>
            {
                var index = records.FindIndex(record => record.StaffId == staffId);
                var version = index < 0 ? FirstVersion : records[index].Version + 1;
                var updated = new CredentialRecord
                {
                    StaffId = staffId,
                    PasswordHash = passwordHash,
                    Version = version,
                };
                if (index < 0)
                {
                    records.Add(updated);
                }
                else
                {
                    records[index] = updated;
                }
            },
            cancellationToken);
    }
}
