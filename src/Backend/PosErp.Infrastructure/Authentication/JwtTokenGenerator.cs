using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Auth;

namespace PosErp.Infrastructure.Authentication;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    // S1 FIX: Secret read from IConfiguration (environment variable JWT__Secret on Render).
    // The hardcoded fallback string is only used in Development when no env var is set.
    // In Production, Program.cs throws at startup if JWT__Secret is missing.
    private readonly string _secret;
    private const string Issuer = "PosErp";
    private const string Audience = "PosErpClient";

    public JwtTokenGenerator(IConfiguration configuration)
    {
        _secret = configuration["JWT__Secret"]
            ?? configuration["JWT:Secret"]
            ?? "DevOnlyFallbackKey_ReplaceWithEnvVarInProduction_MinLength64Chars1234567890ABCD";
    }

    public string GenerateToken(User user, string roleName)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, roleName),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), // Added for Claims.User.FindFirst()
            new Claim("store_id", user.StoreId?.ToString() ?? string.Empty),
            new Claim("full_name", user.FullName)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(12),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }
}
