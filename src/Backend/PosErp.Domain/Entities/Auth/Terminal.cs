using System;

namespace PosErp.Domain.Entities.Auth;

public class Terminal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TerminalCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
