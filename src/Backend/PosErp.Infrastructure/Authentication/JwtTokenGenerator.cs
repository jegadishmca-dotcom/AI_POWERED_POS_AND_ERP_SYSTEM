using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Auth;

namespace PosErp.Infrastructure.Authentication;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private const string Secret = "SuperSecretKeyForDevelopmentPurposesOnlyReplaceInProdSuperSecretKeyForDevelopmentPurposesOnlyReplaceInProd";
    private const string Issuer = "PosErp";
    private const string Audience = "PosErpClient";

    public string GenerateToken(User user, string roleName)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, roleName),
            new Claim("store_id", user.StoreId?.ToString() ?? string.Empty),
            new Claim("full_name", user.FullName)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
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
