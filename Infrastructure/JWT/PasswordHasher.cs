using System.Security.Cryptography;
using System.Text;
using AspNetProject.Application.Interfaces;

namespace AspNetProject.Infrastructure;

public class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16; // 128 бит
    private const int KeySize = 32; // 256 бит
    private const int Iterations = 100_000; // 100k итераций для замедления brute-force

    public string Hash(string password)
    {
        using var rng = RandomNumberGenerator.Create();
        var salt = new byte[SaltSize];
        rng.GetBytes(salt);

        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);

        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string hashedPassword)
    {
        try
        {
            var parts = hashedPassword.Split('.');
            if (parts.Length != 3) return false;

            var iterations = int.Parse(parts[0]);
            var salt = Convert.FromBase64String(parts[1]);
            var hash = Convert.FromBase64String(parts[2]);

            var inputHash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                KeySize);

            return CryptographicOperations.FixedTimeEquals(inputHash, hash);
        }
        catch
        {
            return false;
        }
    }
}