using System;

namespace PosErp.Domain.Entities.Auth;

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string TokenFamily { get; set; } = string.Empty; // For invalidation
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
