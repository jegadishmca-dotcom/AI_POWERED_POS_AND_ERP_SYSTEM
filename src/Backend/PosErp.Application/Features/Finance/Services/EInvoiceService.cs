using Microsoft.Extensions.Configuration;
using Npgsql;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Finance.Services;

public interface IEInvoiceService
{
    Task<EInvoiceResult> GenerateInvoiceIrnAsync(Guid invoiceId, CancellationToken cancellationToken);
}

public class EInvoiceResult
{
    public bool Success { get; set; }
    public string? Irn { get; set; }
    public string? AckNo { get; set; }
    public DateTime? AckDate { get; set; }
    public string? SignedQrCodeUrl { get; set; }
    public string? ErrorMessage { get; set; }
}

public class EInvoiceService : IEInvoiceService
{
    private readonly IConfiguration _configuration;

    public EInvoiceService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Reads the einvoice_enabled flag from database_metadata (persisted across
    /// Docker rebuilds).
    ///
    /// FAIL-CLOSED GUARANTEE: any exception (connection failure, timeout, column
    /// not yet added, null row) returns FALSE — e-invoicing is never activated
    /// by accident. There is no fallback to appsettings.json; enabling e-invoicing
    /// requires the database flag to be explicitly set to TRUE.
    /// </summary>
    private async Task<bool> IsEInvoiceEnabledAsync(CancellationToken cancellationToken)
    {
        var connStr = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connStr)) return false; // no connection string — fail closed

        try
        {
            await using var conn = new NpgsqlConnection(connStr);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT einvoice_enabled
                FROM database_metadata
                LIMIT 1";
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            // result is bool true only when the column exists AND is explicitly TRUE.
            // null (no row), DBNull, or false all return false.
            return result is bool b && b;
        }
        catch
        {
            // FAIL CLOSED — any exception (connection refused, timeout, column missing
            // pre-migration-46, or any other DB error) disables e-invoicing.
            // Do NOT fall back to appsettings.json; that file is baked into the Docker
            // image and could be stale, causing a fail-open situation on rebuild.
            return false;
        }
    }

    public async Task<EInvoiceResult> GenerateInvoiceIrnAsync(Guid invoiceId, CancellationToken cancellationToken)
    {
        // Feature gate: reads einvoice_enabled from database_metadata.
        // FAIL-CLOSED: any DB error, missing column, or null row disables e-invoicing.
        if (!await IsEInvoiceEnabledAsync(cancellationToken))
        {
            return new EInvoiceResult
            {
                Success = false,
                ErrorMessage = "E-Invoicing is not enabled. Set einvoice_enabled = true in database_metadata to activate."
            };
        }

        // In a real implementation, this would format the payload according to the Indian NIC e-Invoice schema
        // and send an HTTP request to a GSP (GST Suvidha Provider) like ClearTax or directly to NIC.

        await Task.Delay(500, cancellationToken); // Simulate API Call

        // Mock Success Response
        return new EInvoiceResult
        {
            Success = true,
            Irn = Guid.NewGuid().ToString().Replace("-", "") + Guid.NewGuid().ToString().Replace("-", ""),
            AckNo = new Random().Next(10000000, 99999999).ToString(),
            AckDate = DateTime.UtcNow,
            SignedQrCodeUrl = $"https://einvoice.gst.gov.in/qr/{invoiceId}"
        };
    }
}

