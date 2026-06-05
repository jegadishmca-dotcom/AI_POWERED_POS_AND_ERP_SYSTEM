using System;

namespace PosErp.Domain.Entities.Pos;

public class StoreBusinessDate
{
    public Guid StoreId { get; set; } = Guid.Empty;
    public DateTime BusinessDate { get; set; }
    public string Status { get; set; } = "OPEN"; // OPEN, CLOSED
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public Guid? OpenedBy { get; set; }
    public DateTime? ClosedAt { get; set; }
    public Guid? ClosedBy { get; set; }
}
