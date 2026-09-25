using System;
using System.Linq;
using System.Security.Claims;

namespace PosErp.Api.Helpers;

public readonly struct StoreScope
{
    public bool IsGlobal { get; }
    public bool IsDenied { get; }
    public Guid? StoreId { get; }

    private StoreScope(bool isGlobal, bool isDenied, Guid? storeId)
    {
        IsGlobal = isGlobal;
        IsDenied = isDenied;
        StoreId = storeId;
    }

    public static StoreScope Global => new(true, false, null);
    public static StoreScope Denied => new(false, true, null);
    public static StoreScope Specific(Guid storeId) => new(false, false, storeId);
}

public static class StoreScopeHelper
{
    private static readonly string[] HeadOfficeRoles = { "Owner", "Admin" };

    /// <summary>
    /// Resolves the store scoping rules for an authenticated user:
    /// 1. If a valid store_id claim is present, access is strictly limited to that specific store.
    /// 2. If store_id claim is absent/empty:
    ///    - If user has a Head-Office role (Owner, Admin), cross-store access is granted (Global).
    ///    - If user has any other role (e.g. Cashier, Supervisor, Manager), access is DENIED (fail-closed).
    /// </summary>
    public static StoreScope GetCallerStoreScope(ClaimsPrincipal? user)
    {
        if (user == null)
            return StoreScope.Denied;

        var storeIdClaim = user.FindFirst("store_id")?.Value;
        if (!string.IsNullOrWhiteSpace(storeIdClaim) && Guid.TryParse(storeIdClaim, out var storeId))
        {
            return StoreScope.Specific(storeId);
        }

        // store_id claim is absent or empty.
        // Explicitly check if caller has a head-office role authorized for enterprise-wide cross-store access:
        var isHeadOffice = HeadOfficeRoles.Any(r => 
            user.IsInRole(r) || 
            user.FindAll(ClaimTypes.Role).Any(c => string.Equals(c.Value, r, StringComparison.OrdinalIgnoreCase)) ||
            user.FindAll("role").Any(c => string.Equals(c.Value, r, StringComparison.OrdinalIgnoreCase))
        );

        if (isHeadOffice)
        {
            return StoreScope.Global;
        }

        // Fail-closed: Any non-head-office role (Cashier, Supervisor, Manager, etc.) with a missing store_id is denied access.
        return StoreScope.Denied;
    }
}
