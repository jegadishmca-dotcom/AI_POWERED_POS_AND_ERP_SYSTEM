using System;

namespace PosErp.Domain.Entities.Pos;

public class PosSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TerminalId { get; set; }
    public Guid CashierId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    
    public decimal OpeningFloatCash { get; set; }
    public decimal ExpectedClosingCash { get; set; }
    public decimal ActualClosingCash { get; set; }
    public decimal Difference { get; set; }
    
    public string Status { get; set; } = "OPEN"; // OPEN or CLOSED
}
