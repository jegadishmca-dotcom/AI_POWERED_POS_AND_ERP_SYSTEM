using System;

namespace PosErp.Domain.Entities.Common
{
    public class IdempotentRequest : ITenantEntity
    {
        public Guid ClientRequestToken { get; set; }
        public string Status { get; set; } = "PENDING"; // PENDING, COMPLETED, FAILED
        public string? ResponsePayload { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Guid TenantId { get; set; }
    }
}
