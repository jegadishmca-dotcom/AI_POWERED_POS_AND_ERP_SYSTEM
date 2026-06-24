using System;

namespace PosErp.Domain.Entities.Common;

public interface ITenantEntity
{
    Guid TenantId { get; set; }
}
