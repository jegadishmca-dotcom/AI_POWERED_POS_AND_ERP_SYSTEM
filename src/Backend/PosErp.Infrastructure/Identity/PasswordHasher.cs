using System;
using PosErp.Application.Interfaces;

namespace PosErp.Infrastructure.Identity;

public class PasswordHasher : IPasswordHasher
{
    // Simple BCrypt wrapper stub for production.
    // In actual project, use BCrypt.Net-Next: return BCrypt.Net.BCrypt.HashPassword(password);
    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    public bool VerifyPassword(string password, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
}
