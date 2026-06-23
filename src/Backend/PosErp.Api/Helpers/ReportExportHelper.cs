using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace PosErp.Api.Helpers;

public static class ReportExportHelper
{
    static ReportExportHelper()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    #region CSV Exporter

    public static byte[] ExportToCsv<T>(List<T> data)
    {
        if (data == null || !data.Any())
            return Encoding.UTF8.GetBytes(string.Empty);

        var builder = new StringBuilder();
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // Header
        builder.AppendLine(string.Join(",", properties.Select(p => EscapeCsv(p.Name))));

        // Rows
        foreach (var item in data)
        {
            var line = properties.Select(p =>
            {
                var val = p.GetValue(item);
                return EscapeCsv(val?.ToString() ?? string.Empty);
            });
            builder.AppendLine(string.Join(",", line));
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static string EscapeCsv(string str)
    {
        if (str.Contains(",") || str.Contains("\"") || str.Contains("\r") || str.Contains("\n"))
        {
            return $"\"{str.Replace("\"", "\"\"")}\"";
        }
        return str;
    }

    #endregion

    #region Excel Exporter

    public static byte[] ExportToExcel<T>(string reportName, string storeCode, string dateRange, List<T> data)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add(reportName.Length > 30 ? reportName.Substring(0, 30) : reportName);

        // 1. Header Information
        ws.Cell("A1").Value = $"Apple Super Market - {reportName}";
        ws.Cell("A1").Style.Font.FontSize = 16;
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontColor = XLColor.FromHtml("#4f46e5");

        ws.Cell("A2").Value = $"Store: {storeCode} | Date/Period: {dateRange}";
        ws.Cell("A2").Style.Font.FontSize = 10;
        ws.Cell("A2").Style.Font.Italic = true;

        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => !typeof(IEnumerable).IsAssignableFrom(p.PropertyType) || p.PropertyType == typeof(string))
            .ToList();

        // 2. Column Headers
        int colIdx = 1;
        foreach (var prop in properties)
        {
            var cell = ws.Cell(4, colIdx);
            cell.Value = prop.Name;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#e2e8f0");
            cell.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
            colIdx++;
        }

        // 3. Data Rows
        int rowIdx = 5;
        foreach (var item in data)
        {
            colIdx = 1;
            foreach (var prop in properties)
            {
                var cell = ws.Cell(rowIdx, colIdx);
                var val = prop.GetValue(item);

                if (val is decimal decVal)
                {
                    cell.Value = decVal;
                    if (prop.Name.Contains("Cost") || prop.Name.Contains("Price") || prop.Name.Contains("Value") || prop.Name.Contains("Tax") || prop.Name.Contains("Sales") || prop.Name.Contains("Revenue") || prop.Name.Contains("Discounts") || prop.Name.Contains("Gst") || prop.Name.Contains("Cgst") || prop.Name.Contains("Sgst") || prop.Name.Contains("Igst") || prop.Name.Contains("Liability") || prop.Name.Contains("Balance") || prop.Name.Contains("Total") || prop.Name.Contains("Debit") || prop.Name.Contains("Credit") || prop.Name.Contains("Amount"))
                    {
                        cell.Style.NumberFormat.Format = "₹#,##0.00";
                    }
                    else
                    {
                        cell.Style.NumberFormat.Format = "#,##0.00";
                    }
                }
                else if (val is int intVal)
                {
                    cell.Value = intVal;
                    cell.Style.NumberFormat.Format = "#,##0";
                }
                else if (val is DateTime dtVal)
                {
                    cell.Value = dtVal;
                    cell.Style.DateFormat.Format = "yyyy-MM-dd";
                }
                else
                {
                    cell.Value = val?.ToString() ?? string.Empty;
                }

                // Alternating row background
                if (rowIdx % 2 == 0)
                {
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#f8fafc");
                }

                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#f1f5f9");
                colIdx++;
            }
            rowIdx++;
        }

        // 4. Totals Row for decimal columns
        colIdx = 1;
        bool hasTotals = false;
        foreach (var prop in properties)
        {
            if (prop.PropertyType == typeof(decimal))
            {
                var cell = ws.Cell(rowIdx, colIdx);
                var colLetter = ws.Column(colIdx).ColumnLetter;
                cell.FormulaA1 = $"SUM({colLetter}5:{colLetter}{rowIdx - 1})";
                cell.Style.Font.Bold = true;
                cell.Style.Border.TopBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Double;
                
                if (prop.Name.Contains("Cost") || prop.Name.Contains("Price") || prop.Name.Contains("Value") || prop.Name.Contains("Tax") || prop.Name.Contains("Sales") || prop.Name.Contains("Revenue") || prop.Name.Contains("Discounts") || prop.Name.Contains("Gst") || prop.Name.Contains("Cgst") || prop.Name.Contains("Sgst") || prop.Name.Contains("Igst") || prop.Name.Contains("Liability") || prop.Name.Contains("Balance") || prop.Name.Contains("Total") || prop.Name.Contains("Debit") || prop.Name.Contains("Credit") || prop.Name.Contains("Amount"))
                {
                    cell.Style.NumberFormat.Format = "₹#,##0.00";
                }
                else
                {
                    cell.Style.NumberFormat.Format = "#,##0.00";
                }
                hasTotals = true;
            }
            colIdx++;
        }

        if (hasTotals)
        {
            ws.Cell(rowIdx, 1).Value = "Total";
            ws.Cell(rowIdx, 1).Style.Font.Bold = true;
        }

        // Auto-fit columns
        ws.Columns(1, properties.Count).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    #endregion

    #region QuestPDF Report Exporter

    public static byte[] ExportToPdf<T>(string reportName, string storeCode, string dateRange, List<T> data)
    {
        var document = new GenericReportDocument<T>(reportName, storeCode, dateRange, data);
        return document.GeneratePdf();
    }

    private class GenericReportDocument<T> : IDocument
    {
        private readonly string _reportName;
        private readonly string _storeCode;
        private readonly string _dateRange;
        private readonly List<T> _data;

        public GenericReportDocument(string reportName, string storeCode, string dateRange, List<T> data)
        {
            _reportName = reportName;
            _storeCode = storeCode;
            _dateRange = dateRange;
            _data = data ?? new List<T>();
        }

        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.Size(PageSizes.A4);
                page.Header().Element(ComposeHeader);
                page.Content().Element(ComposeContent);
                page.Footer().AlignCenter().Text(text =>
                {
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        }

        private void ComposeHeader(IContainer container)
        {
            container.Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text("Apple Super Market").FontSize(20).Bold().FontColor("#4f46e5");
                    column.Item().Text(_reportName).FontSize(14).Bold().FontColor(Colors.Grey.Darken3);
                    column.Item().Text($"Store: {_storeCode} | Date: {_dateRange}").FontSize(9).Italic().FontColor(Colors.Grey.Medium);
                    column.Item().Text($"Generated: {DateTime.Now:yyyy-MM-dd hh:mm tt}").FontSize(8).Italic().FontColor(Colors.Grey.Lighten1);
                });
            });
        }

        private void ComposeContent(IContainer container)
        {
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => !typeof(IEnumerable).IsAssignableFrom(p.PropertyType) || p.PropertyType == typeof(string))
                .ToList();

            container.PaddingVertical(10).Column(column =>
            {
                column.Spacing(10);

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        foreach (var prop in properties)
                        {
                            if (prop.PropertyType == typeof(string))
                                columns.RelativeColumn(2);
                            else
                                columns.RelativeColumn();
                        }
                    });

                    table.Header(header =>
                    {
                        foreach (var prop in properties)
                        {
                            header.Cell().Element(CellStyle).Text(prop.Name).Bold().FontSize(9);
                        }

                        IContainer CellStyle(IContainer c) => c.DefaultTextStyle(x => x.SemiBold()).BorderBottom(1).BorderColor(Colors.Grey.Lighten1).PaddingVertical(4).Background(Colors.Grey.Lighten4).PaddingHorizontal(4);
                    });

                    foreach (var item in _data)
                    {
                        foreach (var prop in properties)
                        {
                            var val = prop.GetValue(item);
                            string formatted = val?.ToString() ?? string.Empty;

                            if (val is decimal decVal)
                            {
                                if (prop.Name.Contains("Cost") || prop.Name.Contains("Price") || prop.Name.Contains("Value") || prop.Name.Contains("Tax") || prop.Name.Contains("Sales") || prop.Name.Contains("Revenue") || prop.Name.Contains("Discounts") || prop.Name.Contains("Gst") || prop.Name.Contains("Cgst") || prop.Name.Contains("Sgst") || prop.Name.Contains("Igst") || prop.Name.Contains("Liability") || prop.Name.Contains("Balance") || prop.Name.Contains("Total") || prop.Name.Contains("Debit") || prop.Name.Contains("Credit") || prop.Name.Contains("Amount"))
                                {
                                    formatted = $"₹{decVal:N2}";
                                }
                                else
                                {
                                    formatted = $"{decVal:N2}";
                                }
                            }
                            else if (val is int intVal)
                            {
                                formatted = $"{intVal:N0}";
                            }
                            else if (val is DateTime dtVal)
                            {
                                formatted = $"{dtVal:yyyy-MM-dd}";
                            }

                            table.Cell().Element(CellStyle).Text(formatted).FontSize(8);
                        }

                        IContainer CellStyle(IContainer c) => c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).PaddingVertical(4).PaddingHorizontal(4);
                    }
                });
            });
        }
    }

    #endregion
}
