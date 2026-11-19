using System.Security.Cryptography;

namespace RestaurantManagementSystem.Core;

public static class PasswordSecurity
{
    private const int Iterations = 210_000;

    public static (string Hash, string Salt) Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var derived = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations,
            HashAlgorithmName.SHA256, 32);
        return (Convert.ToBase64String(derived), Convert.ToBase64String(salt));
    }

    public static bool Verify(string password, string expectedHash, string encodedSalt)
    {
        try
        {
            var salt = Convert.FromBase64String(encodedSalt);
            var expected = Convert.FromBase64String(expectedHash);
            var candidate = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations,
                HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(candidate, expected);
        }
        catch (FormatException) { return false; }
    }
}
