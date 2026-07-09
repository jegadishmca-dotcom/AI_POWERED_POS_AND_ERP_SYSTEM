namespace PosErp.Application.Interfaces;

/// <summary>
/// Marker interface to opt-in MediatR commands/requests for automatic retry on transient PostgreSQL failures.
/// </summary>
public interface IRetryableRequest
{
}
