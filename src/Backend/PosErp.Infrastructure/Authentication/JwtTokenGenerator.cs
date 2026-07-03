using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Auth;
using PosErp.Infrastructure.Services;

namespace PosErp.Infrastructure.Authentication;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly string _secret;
    private readonly IConfiguration _configuration;
    private readonly IConnectionStringProvider _connectionStringProvider;
    private readonly ITenantProvider _tenantProvider;
    private const string Issuer = "PosErp";
    private const string Audience = "PosErpClient";

    public JwtTokenGenerator(
        IConfiguration configuration,
        IConnectionStringProvider connectionStringProvider,
        ITenantProvider tenantProvider)
    {
        _configuration = configuration;
        _connectionStringProvider = connectionStringProvider;
        _tenantProvider = tenantProvider;
        _secret = configuration["JWT__Secret"]
            ?? configuration["JWT:Secret"]
            ?? "DevOnlyFallbackKey_ReplaceWithEnvVarInProduction_MinLength64Chars1234567890ABCD";
    }

    public string GenerateToken(User user, string roleName)
    {
        var deploymentMode = _configuration["SystemConfig:DeploymentMode"] ?? "SelfHosted";
        int tokenVersion = 1;

        if (string.Equals(deploymentMode, "SaaS", StringComparison.OrdinalIgnoreCase))
        {
            // SaaS: fetch from Platform DB tenant_environments table
            var platformConn = _configuration.GetConnectionString("DefaultConnection") 
                ?? "Host=localhost;Database=poserp;Username=postgres;Password=postgres";
            try
            {
                using var conn = new Npgsql.NpgsqlConnection(platformConn);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT token_version FROM tenant_environments WHERE tenant_id = @p0";
                var p = cmd.CreateParameter();
                p.ParameterName = "@p0";
                p.Value = _tenantProvider.TenantId;
                cmd.Parameters.Add(p);
                var res = cmd.ExecuteScalar();
                if (res != null && res != DBNull.Value)
                {
                    tokenVersion = Convert.ToInt32(res);
                }
            }
            catch
            {
                // Fallback
            }
        }
        else
        {
            // Self-hosted: fetch from operation_mode.json
            var connProvider = _connectionStringProvider as ConnectionStringProvider;
            if (connProvider != null)
            {
                tokenVersion = connProvider.GetSelfHostedTokenVersion();
            }
        }

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, roleName),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), // Added for Claims.User.FindFirst()
            new Claim("store_id", user.StoreId?.ToString() ?? string.Empty),
            new Claim("full_name", user.FullName),
            new Claim("token_version", tokenVersion.ToString()) // Token version claim for invalidation
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
