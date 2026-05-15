$backendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Backend"
$frontendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Frontend"

New-Item -ItemType Directory -Force -Path "$frontendDir\src\components\ui"
New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Application\Interfaces"
New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Infrastructure\Identity"

# ----------------- BACKEND -----------------

# PasswordHasher
@"
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
"@ | Out-File -FilePath "$backendDir\PosErp.Infrastructure\Identity\PasswordHasher.cs" -Encoding utf8

# IPasswordHasher
@"
namespace PosErp.Application.Interfaces;

public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string hash);
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Interfaces\IPasswordHasher.cs" -Encoding utf8

# IApplicationDbContext
@"
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PosErp.Domain.Entities.Auth;

namespace PosErp.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    // DbSet<Terminal> Terminals { get; }
    // DbSet<AuditLog> AuditLogs { get; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Interfaces\IApplicationDbContext.cs" -Encoding utf8

# LoginCommandHandler.cs
@"
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Auth;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Auth.Commands.Login;

public record LoginCommand(string Username, string Password, string TerminalCode) : IRequest<LoginResponse>;

public record LoginResponse(string AccessToken, string RefreshToken, UserDto User);
public record UserDto(Guid Id, string Username, string FullName, Guid RoleId, Guid? StoreId);

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MinimumLength(3);
        RuleFor(x => x.Password).NotEmpty()
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one number.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.");
        RuleFor(x => x.TerminalCode).NotEmpty();
    }
}

public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IJwtTokenGenerator _jwtGenerator;
    private readonly IPasswordHasher _passwordHasher;

    public LoginCommandHandler(IApplicationDbContext context, IJwtTokenGenerator jwtGenerator, IPasswordHasher passwordHasher)
    {
        _context = context;
        _jwtGenerator = jwtGenerator;
        _passwordHasher = passwordHasher;
    }

    public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.Username && !u.IsDeleted, cancellationToken);
        
        if (user == null || !user.IsActive || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            // TODO: Log failed attempt to AuditLog
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        // TODO: Validate TerminalCode exists and is active
        
        var accessToken = _jwtGenerator.GenerateToken(user, "UserRolePlaceholder");
        var refreshTokenStr = _jwtGenerator.GenerateRefreshToken();

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            Token = refreshTokenStr,
            TokenFamily = Guid.NewGuid().ToString(),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            DeviceId = request.TerminalCode,
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.RefreshTokens.Add(refreshToken);
        
        // TODO: Log successful login to AuditLog

        await _context.SaveChangesAsync(cancellationToken);

        return new LoginResponse(
            accessToken, 
            refreshTokenStr, 
            new UserDto(user.Id, user.Username, user.FullName, user.RoleId, user.StoreId));
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Features\Auth\Commands\Login\LoginCommand.cs" -Encoding utf8

# RefreshTokenCommandHandler.cs
@"
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
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Features\Auth\Commands\Refresh\RefreshTokenCommand.cs" -Encoding utf8

# JwtTokenGenerator.cs
@"
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
"@ | Out-File -FilePath "$backendDir\PosErp.Infrastructure\Authentication\JwtTokenGenerator.cs" -Encoding utf8


# ----------------- FRONTEND -----------------

# auth.store.ts
@"
import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import { User } from '../types';

interface AuthState {
  user: User | null;
  accessToken: string | null;
  isAuthenticated: boolean;
  setAuth: (user: User, token: string) => void;
  clearAuth: () => void;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      user: null,
      accessToken: null,
      isAuthenticated: false,
      setAuth: (user, token) => set({ user, accessToken: token, isAuthenticated: true }),
      clearAuth: () => set({ user: null, accessToken: null, isAuthenticated: false }),
    }),
    {
      name: 'pos-auth-storage', // saves to localStorage
      partialize: (state) => ({ user: state.user }), // Don't persist token in localStorage, only in memory & HttpOnly
    }
  )
);
"@ | Out-File -FilePath "$frontendDir\src\features\auth\store\auth.store.ts" -Encoding utf8

# LoginForm.tsx
@"
import React, { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2 } from 'lucide-react';

const loginSchema = z.object({
  username: z.string().min(3, 'Username must be at least 3 characters'),
  password: z.string().min(8, 'Password must be at least 8 characters'),
  terminalCode: z.string().min(1, 'Terminal Code is required'),
});

type LoginFormData = z.infer<typeof loginSchema>;

interface LoginFormProps {
  onSubmit: (data: LoginFormData) => Promise<void>;
  isLoading: boolean;
  error?: string | null;
}

export const LoginForm: React.FC<LoginFormProps> = ({ onSubmit, isLoading, error }) => {
  const { register, handleSubmit, formState: { errors } } = useForm<LoginFormData>({
    resolver: zodResolver(loginSchema),
    defaultValues: {
      terminalCode: localStorage.getItem('pos_terminal_code') || '',
    }
  });

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
      {error && (
        <div className="bg-red-50 dark:bg-red-900/30 border border-red-200 dark:border-red-800 text-red-600 dark:text-red-400 p-3 rounded-md text-sm">
          {error}
        </div>
      )}

      <div>
        <label className="block text-sm font-medium text-slate-700 dark:text-slate-300">Terminal Code</label>
        <input 
          {...register('terminalCode')}
          type="text" 
          className="mt-1 block w-full rounded-md border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-900 dark:text-white shadow-sm focus:border-blue-500 focus:ring-blue-500 sm:text-sm p-2.5 border" 
          placeholder="TERM-01" 
          onKeyDown={(e) => { if (e.key === 'Enter') e.currentTarget.blur(); }}
        />
        {errors.terminalCode && <p className="mt-1 text-sm text-red-600 dark:text-red-400">{errors.terminalCode.message}</p>}
      </div>

      <div>
        <label className="block text-sm font-medium text-slate-700 dark:text-slate-300">Username</label>
        <input 
          {...register('username')}
          type="text" 
          className="mt-1 block w-full rounded-md border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-900 dark:text-white shadow-sm focus:border-blue-500 focus:ring-blue-500 sm:text-sm p-2.5 border" 
          placeholder="Enter username" 
          autoComplete="username"
        />
        {errors.username && <p className="mt-1 text-sm text-red-600 dark:text-red-400">{errors.username.message}</p>}
      </div>

      <div>
        <label className="block text-sm font-medium text-slate-700 dark:text-slate-300">Password</label>
        <input 
          {...register('password')}
          type="password" 
          className="mt-1 block w-full rounded-md border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-900 dark:text-white shadow-sm focus:border-blue-500 focus:ring-blue-500 sm:text-sm p-2.5 border" 
          placeholder="••••••••" 
          autoComplete="current-password"
        />
        {errors.password && <p className="mt-1 text-sm text-red-600 dark:text-red-400">{errors.password.message}</p>}
      </div>

      <button 
        type="submit" 
        disabled={isLoading}
        className="w-full flex justify-center items-center py-2.5 px-4 border border-transparent rounded-md shadow-sm text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
      >
        {isLoading ? <Loader2 className="animate-spin h-5 w-5 mr-2" /> : 'Sign In'}
      </button>
    </form>
  );
};
"@ | Out-File -FilePath "$frontendDir\src\features\auth\components\LoginForm.tsx" -Encoding utf8

# ProtectedRoute.tsx
@"
import React from 'react';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuthStore } from '../store/auth.store';

export const ProtectedRoute: React.FC = () => {
  const { isAuthenticated } = useAuthStore();
  const location = useLocation();

  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  return <Outlet />;
};
"@ | Out-File -FilePath "$frontendDir\src\features\auth\components\ProtectedRoute.tsx" -Encoding utf8

# Login.tsx (Updated)
@"
import React, { useState } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { LoginForm } from '../components/LoginForm';
import { login } from '../api/auth.api';
import { useAuthStore } from '../store/auth.store';

export const Login = () => {
  const setAuth = useAuthStore((state) => state.setAuth);
  const navigate = useNavigate();
  const location = useLocation();
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const from = location.state?.from?.pathname || '/dashboard';

  const handleLogin = async (data: any) => {
    setIsLoading(true);
    setError(null);
    try {
      // Save terminal code for convenience
      localStorage.setItem('pos_terminal_code', data.terminalCode);
      
      const response = await login(data);
      setAuth(response.user, response.accessToken);
      navigate(from, { replace: true });
    } catch (err: any) {
      setError(err.response?.data?.message || 'Invalid username or password. Please try again.');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-slate-50 dark:bg-slate-900 flex flex-col justify-center py-12 sm:px-6 lg:px-8 transition-colors duration-200">
      <div className="sm:mx-auto sm:w-full sm:max-w-md">
        <h2 className="mt-6 text-center text-3xl font-extrabold text-slate-900 dark:text-white">
          Enterprise POS
        </h2>
        <p className="mt-2 text-center text-sm text-slate-600 dark:text-slate-400">
          Sign in to your terminal
        </p>
      </div>

      <div className="mt-8 sm:mx-auto sm:w-full sm:max-w-md">
        <div className="bg-white dark:bg-slate-800 py-8 px-4 shadow sm:rounded-lg sm:px-10 border border-slate-200 dark:border-slate-700">
          <LoginForm onSubmit={handleLogin} isLoading={isLoading} error={error} />
        </div>
      </div>
    </div>
  );
};
"@ | Out-File -FilePath "$frontendDir\src\features\auth\routes\Login.tsx" -Encoding utf8

Write-Host "Auth Enhanced"
