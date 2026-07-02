using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PosErp.Api.Controllers;
using Xunit;

namespace PosErp.IntegrationTests;

/// <summary>
/// F01 — Gap #4: Verify that the unauthenticated debug invoice endpoint
/// has been removed from PosController.
///
/// Test 1 is written to FAIL against the current (pre-fix) code and
/// PASS once the DebugInvoices method is deleted.
/// </summary>
public class F01_DebugEndpointTests
{
    // -----------------------------------------------------------------------
    // Test 1: No method named DebugInvoices should exist on PosController.
    // -----------------------------------------------------------------------
    [Fact]
    public void PosController_MustNot_HaveDebugInvoicesMethod()
    {
        var controllerType = typeof(PosController);

        var method = controllerType.GetMethod(
            "DebugInvoices",
            BindingFlags.Public | BindingFlags.Instance);

        Assert.Null(method); // Fails pre-fix because the method exists
    }

    // -----------------------------------------------------------------------
    // Test 2: Any method on PosController decorated with [HttpGet("invoices/debug")]
    // must also carry [Authorize]. Belt-and-suspenders check in case the method
    // is renamed but the route is kept.
    // -----------------------------------------------------------------------
    [Fact]
    public void PosController_InvoicesDebugRoute_MustRequireAuthorization()
    {
        var controllerType = typeof(PosController);
        var methods = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance);

        foreach (var method in methods)
        {
            var httpGetAttr = method.GetCustomAttribute<HttpGetAttribute>();
            if (httpGetAttr == null) continue;

            bool isDebugRoute = httpGetAttr.Template != null &&
                httpGetAttr.Template.Equals("invoices/debug", StringComparison.OrdinalIgnoreCase);

            if (!isDebugRoute) continue;

            var hasAuthorize  = method.GetCustomAttribute<AuthorizeAttribute>() != null;
            var hasAnonymous  = method.GetCustomAttribute<AllowAnonymousAttribute>() != null;

            Assert.True(
                hasAuthorize && !hasAnonymous,
                $"Method '{method.Name}' maps to 'invoices/debug' but is not properly authorized. " +
                "Remove this endpoint or add [Authorize].");
        }
        // If route is gone entirely, loop completes with zero assertions => pass.
    }
}
