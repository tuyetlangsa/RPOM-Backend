using Rpom.Application.Abstraction.Authentication;

namespace Rpom.Infrastructure.Authentication;

internal sealed class PasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string plainPassword) => BCrypt.Net.BCrypt.HashPassword(plainPassword, WorkFactor);

    public bool Verify(string plainPassword, string passwordHash) =>
        BCrypt.Net.BCrypt.Verify(plainPassword, passwordHash);
}
