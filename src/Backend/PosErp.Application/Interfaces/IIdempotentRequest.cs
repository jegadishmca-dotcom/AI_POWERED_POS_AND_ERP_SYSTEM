using System;

namespace PosErp.Application.Interfaces
{
    /// <summary>
    /// Interface for requests that support idempotency via a ClientRequestToken.
    /// </summary>
    public interface IIdempotentRequest
    {
        Guid? ClientRequestToken { get; }
    }
}
