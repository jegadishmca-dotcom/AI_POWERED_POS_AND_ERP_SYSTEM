using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Auth;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Auth.Commands.Refresh;

public record RefreshTokenCommand(string RefreshToken, string DeviceId) : IRequest<RefreshResponse>;
public record RefreshResponse(string AccessToken, string RefreshToken);

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, RefreshResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IJwtTokenGenerator _jwtGenerator;

    public RefreshTokenCommandHandler(IApplicationDbContext context, IJwtTokenGenerator jwtGenerator)
    {
        _context = context;
        _jwtGenerator = jwtGenerator;
    }

    public async Task<RefreshResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var existingToken = await _context.RefreshTokens
            .Include(x => x.User) // Requires adding User nav prop to RefreshToken entity
            .FirstOrDefaultAsync(x => x.Token == request.RefreshToken, cancellationToken);

        if (existingToken == null)
        {
            throw new UnauthorizedAccessException("Invalid token.");
        }

        // Token Family Invalidation Logic
        if (existingToken.IsRevoked)
        {
            // Alert! Reuse of revoked token detected. Revoke entire family.
            var familyTokens = await _context.RefreshTokens
                .Where(x => x.TokenFamily == existingToken.TokenFamily)
                .ToListAsync(cancellationToken);
                
            foreach (var t in familyTokens) t.IsRevoked = true;
            await _context.SaveChangesAsync(cancellationToken);
            
            // TODO: Log security alert to AuditLog
            throw new UnauthorizedAccessException("Token reuse detected. Access revoked.");
        }

        if (existingToken.ExpiresAt < DateTime.UtcNow)
        {
            existingToken.IsRevoked = true;
            await _context.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAccessException("Token expired.");
        }

        // Revoke the old token
        existingToken.IsRevoked = true;

        // Generate new tokens
        var user = await _context.Users.FindAsync(new object[] { existingToken.UserId }, cancellationToken);
        var accessToken = _jwtGenerator.GenerateToken(user, "UserRolePlaceholder");
        var newRefreshTokenStr = _jwtGenerator.GenerateRefreshToken();

        var newRefreshToken = new RefreshToken
        {
            UserId = existingToken.UserId,
            Token = newRefreshTokenStr,
            TokenFamily = existingToken.TokenFamily, // Keep same family
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            DeviceId = request.DeviceId,
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.RefreshTokens.Add(newRefreshToken);
        await _context.SaveChangesAsync(cancellationToken);

        return new RefreshResponse(accessToken, newRefreshTokenStr);
    }
}
