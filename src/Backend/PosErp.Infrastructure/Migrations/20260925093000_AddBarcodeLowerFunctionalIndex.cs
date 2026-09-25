using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PosErp.Infrastructure.Migrations
{
    /// <summary>
    /// Adds a functional B-tree index on lower(barcode) to the barcodes table
    /// to support case-insensitive barcode lookups without full table scans.
    /// 
    /// The SearchProductsQuery now uses: b.BarcodeValue.ToLower() == q.ToLower()
    /// which PostgreSQL translates to: lower(barcode) = lower(@p).
    /// Without this index, that WHERE clause triggers a sequential scan.
    /// 
    /// Note: Not using CONCURRENTLY because EF Core's migration runner wraps
    /// migrations in a transaction by default, and no other migration in this
    /// codebase uses SuppressTransaction. The barcodes table is small enough
    /// that the brief exclusive lock during index creation is acceptable.
    /// </summary>
    public partial class AddBarcodeLowerFunctionalIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Functional index: enables index-scan for lower(barcode) = @value queries
            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ix_barcodes_barcode_lower 
                  ON barcodes (lower(barcode));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"DROP INDEX IF EXISTS ix_barcodes_barcode_lower;");
        }
    }
}
