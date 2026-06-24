using System;

namespace PosErp.Domain.Entities.Offers;

public class OfferVersion
{
    public Guid Id { get; set; }
    public Guid OfferId { get; set; }
    public int VersionNumber { get; set; }
    public Guid? ModifiedBy { get; set; }
    public DateTime ModifiedDate { get; set; }
    public string PreviousConfiguration { get; set; } = string.Empty; // JSON snapshot of the offer rules
    public string ChangeReason { get; set; } = string.Empty;
}
