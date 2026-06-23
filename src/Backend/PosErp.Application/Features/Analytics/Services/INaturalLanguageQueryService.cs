using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Analytics.Services;

public interface INaturalLanguageQueryService
{
    Task<NlQueryResultDto> ParseAndExecuteQueryAsync(string queryText, Guid? storeId, CancellationToken cancellationToken);
}

public class NlQueryResultDto
{
    public string Query { get; set; } = string.Empty;
    public bool IsParsedSuccessfully { get; set; }
    public string SummaryText { get; set; } = string.Empty;
    public string ReportType { get; set; } = string.Empty; // P_AND_L, BALANCE_SHEET, TRIAL_BALANCE, CASH_FLOW, INVENTORY_VALUATION, GST
    public string TimePeriod { get; set; } = string.Empty;
    public string VisualType { get; set; } = "TABLE"; // CARD, TABLE, CHART
    
    public List<string> Columns { get; set; } = new();
    public List<Dictionary<string, object>> DataRows { get; set; } = new();
}
