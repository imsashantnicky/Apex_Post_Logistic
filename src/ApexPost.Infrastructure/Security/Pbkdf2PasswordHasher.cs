using System.Globalization;
using System.Security.Cryptography;
using ApexPost.Application.Abstractions;
using ApexPost.Application.Settings;
using Microsoft.Extensions.Options;

namespace ApexPost.Infrastructure.Security;

/// <summary>
/// PBKDF2 with SHA-256 and a random 16-byte salt per password. The stored text is
/// "PBKDF2-SHA256$iterations$salt$hash", so the work factor can be raised later without breaking old hashes.
/// </summary>
public sealed class Pbkdf2PasswordHasher(IOptions<PasswordSettings> settings) : IPasswordHasher
{
    private const string Scheme = "PBKDF2-SHA256";
    private const char Separator = '$';
    private const int PartCount = 4;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    private readonly int _iterations = settings.Value.Iterations > 0
        ? settings.Value.Iterations
        : throw new ArgumentOutOfRangeException(nameof(settings), "Passwords:Iterations must be positive.");

    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, _iterations, Algorithm, HashBytes);
        return string.Join(
            Separator,
            Scheme,
            _iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    /// <summary>Returns false for a wrong password and for a damaged or unknown hash format.</summary>
    public bool Verify(string password, string passwordHash)
    {
        ArgumentNullException.ThrowIfNull(password);
        if (!TryParse(passwordHash, out var iterations, out var salt, out var expected))
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, HashBytes);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static bool TryParse(string? passwordHash, out int iterations, out byte[] salt, out byte[] hash)
    {
        iterations = 0;
        salt = [];
        hash = [];
        var parts = passwordHash?.Split(Separator) ?? [];
        return parts.Length == PartCount
            && parts[0] == Scheme
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out iterations)
            && iterations > 0
            && TryDecode(parts[2], SaltBytes, out salt)
            && TryDecode(parts[3], HashBytes, out hash);
    }

    private static bool TryDecode(string text, int length, out byte[] bytes)
    {
        var buffer = new byte[length];
        var isValid = Convert.TryFromBase64String(text, buffer, out var written) && written == length;
        bytes = isValid ? buffer : [];
        return isValid;
    }
}
