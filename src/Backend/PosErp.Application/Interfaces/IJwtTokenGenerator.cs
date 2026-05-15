using System.Threading.Tasks;
using PosErp.Domain.Entities.Auth;

namespace PosErp.Application.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user, string roleName);
    string GenerateRefreshToken();
}
