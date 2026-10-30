using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using QuestPDF.Drawing.Exceptions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SmartInventory.Application.Common;

namespace SmartInventory.Infrastructure.Reports;

internal static class ReportFileRenderer
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    static ReportFileRenderer()
    {
        // This individual student project qualifies under the July 2026 learning/training category.
        // Eligibility must be reviewed before deployment by a different legal entity (docs/A3-EXPORTS.md).
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = false;
        QuestPDF.Settings.ThrowOnMissingTextGlyphs = true;
    }

    internal static string Text(object? value) => value switch
    {
        null => "",
        DateTime timestamp => timestamp.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", Invariant),
        DateTimeOffset timestamp => Text(timestamp.UtcDateTime),
        decimal amount => amount.ToString("0.00", Invariant),
        IFormattable formattable => formattable.ToString(null, Invariant) ?? "",
        _ => value.ToString() ?? ""
    };

    internal static byte[] Csv(ExportTable table, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(',', table.Headers.Select(EscapeCsv)));
        foreach (var row in table.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            builder.AppendLine(string.Join(',', row.Select(value => EscapeCsv(Text(value)))));
        }
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        return [.. encoding.GetPreamble(), .. encoding.GetBytes(builder.ToString())];
    }

    private static string EscapeCsv(string value)
    {
        var trimmed = value.AsSpan().TrimStart();
        if (value.Length > 0 && (value[0] is '\t' or '\r' or '\n' ||
            !trimmed.IsEmpty && trimmed[0] is '=' or '+' or '-' or '@')) value = "'" + value;
        return '"' + value.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
    }

    internal static byte[] Xlsx(ExportTable table, IReadOnlyList<KeyValuePair<string, string>> context,
        CancellationToken cancellationToken)
    {
        using var workbook = new XLWorkbook();
        workbook.Properties.Title = $"Smart Inventory - {context.First(pair => pair.Key == "Report").Value}";
        workbook.Properties.Author = "AI Smart Inventory";
        var sheet = workbook.Worksheets.Add("Data");
        for (var column = 0; column < table.Headers.Count; column++)
            SetText(sheet.Cell(1, column + 1), table.Headers[column]);
        for (var row = 0; row < table.Rows.Count; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var column = 0; column < table.Headers.Count; column++)
                SetCell(sheet.Cell(row + 2, column + 1), table.Rows[row][column]);
        }
        sheet.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#173A57");
        sheet.Row(1).Style.Font.FontColor = XLColor.White;
        sheet.Row(1).Style.Font.Bold = true;
        sheet.Row(1).Height = 30;
        sheet.Columns(1, table.Headers.Count).Width = 24;
        sheet.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        sheet.Range(1, 1, table.Rows.Count + 1, table.Headers.Count).Style.Alignment.WrapText = true;
        sheet.SheetView.FreezeRows(1);
        sheet.Range(1, 1, table.Rows.Count + 1, table.Headers.Count).SetAutoFilter();
        var metadata = workbook.Worksheets.Add("Context");
        SetText(metadata.Cell(1, 1), "Setting");
        SetText(metadata.Cell(1, 2), "Value");
        for (var row = 0; row < context.Count; row++)
        {
            SetText(metadata.Cell(row + 2, 1), context[row].Key);
            SetText(metadata.Cell(row + 2, 2), context[row].Value);
        }
        metadata.Column(1).Width = 24;
        metadata.Column(2).Width = 110;
        metadata.Column(2).Style.Alignment.WrapText = true;
        metadata.Row(1).Style.Font.Bold = true;
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void SetCell(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                return;
            case int number:
                cell.Value = number;
                return;
            case long number when number is >= -999999999999999 and <= 999999999999999:
                cell.Value = number;
                return;
            case decimal amount when FitsExcelNumber(amount):
                cell.Value = (double)amount;
                cell.Style.NumberFormat.Format = "0.00";
                return;
            default:
                // Text values never enter FormulaA1. Oversized decimals/integers and UTC timestamps
                // remain exact strings rather than losing digits or timezone information in Excel.
                SetText(cell, Text(value));
                return;
        }
    }

    private static bool FitsExcelNumber(decimal amount)
    {
        var significant = amount.ToString("0.############################", Invariant)
            .Replace("-", "", StringComparison.Ordinal).Replace(".", "", StringComparison.Ordinal)
            .TrimStart('0').TrimEnd('0');
        return significant.Length <= 15 && Convert.ToDecimal((double)amount, Invariant) == amount;
    }

    private static void SetText(IXLCell cell, string text)
    {
        // ClosedXML interprets an initial apostrophe as an Excel quote prefix. Double it so a
        // literal user-supplied apostrophe also round-trips unchanged; '=' stays plain text.
        cell.Value = text.StartsWith('\'') ? "'" + text : text;
    }

    internal static byte[] Pdf(ExportTable table, IReadOnlyList<KeyValuePair<string, string>> context,
        CancellationToken cancellationToken)
    {
        try
        {
            return GeneratePdf(table, context, cancellationToken);
        }
        catch (DocumentDrawingException exception) when (
            exception.Message.Contains("Missing glyphs:", StringComparison.Ordinal) &&
            exception.Message.Contains("Settings.ThrowOnMissingTextGlyphs", StringComparison.Ordinal))
        {
            // QuestPDF 2026.9.1 documents this exception for missing text glyphs. The additional
            // pinned diagnostics distinguish unsupported content from native/font deployment
            // failures, which must retain their normal infrastructure-error handling.
            throw new AppValidationException("This report contains characters unsupported by the PDF font. Export CSV or XLSX to preserve the complete text.");
        }
    }

    private static byte[] GeneratePdf(ExportTable table, IReadOnlyList<KeyValuePair<string, string>> context,
        CancellationToken cancellationToken)
    {
        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(24);
            // The package includes Lato 2.015 (SIL OFL); no machine-installed fonts are required.
            page.DefaultTextStyle(style => style.FontFamily("Lato").FontSize(8));
            page.Header().PaddingBottom(12).Column(header =>
            {
                header.Item().Text(context.First(pair => pair.Key == "Company").Value).Bold().FontSize(16).FontColor("#173A57");
                header.Item().Text($"Report: {context.First(pair => pair.Key == "Report").Value}").SemiBold().FontSize(12);
                header.Item().Text($"Generated UTC: {context.First(pair => pair.Key == "GeneratedAtUtc").Value}");
            });
            page.Content().Column(content =>
            {
                content.Spacing(6);
                foreach (var pair in context.Where(pair => pair.Key is not ("Company" or "Report" or "GeneratedAtUtc")))
                    content.Item().Text($"{pair.Key}: {pair.Value}").FontSize(7);
                if (table.Rows.Count == 0) content.Item().PaddingVertical(16).Text("No matching records.");
                else if (table.Headers.Count > 10)
                {
                    // Movement reasons and legacy reference metadata need readable space. Record
                    // blocks avoid squeezing fourteen fields into an illegible page-width table.
                    foreach (var row in table.Rows)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        content.Item().BorderBottom(0.5f).BorderColor("#CBD5E1").PaddingVertical(5).Table(record =>
                        {
                            record.ColumnsDefinition(columns => { columns.ConstantColumn(112); columns.RelativeColumn(); });
                            for (var column = 0; column < table.Headers.Count; column++)
                            {
                                record.Cell().Padding(2).Text(table.Headers[column]).SemiBold();
                                record.Cell().Padding(2).Text(Text(row[column]));
                            }
                        });
                    }
                }
                else content.Item().Table(grid =>
                {
                    grid.ColumnsDefinition(columns =>
                    {
                        for (var column = 0; column < table.Headers.Count; column++) columns.RelativeColumn();
                    });
                    grid.Header(header =>
                    {
                        foreach (var heading in table.Headers)
                            header.Cell().Background("#173A57").Padding(4).Text(heading).FontColor(Colors.White).SemiBold().FontSize(7);
                    });
                    foreach (var row in table.Rows)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        foreach (var value in row)
                            grid.Cell().BorderBottom(0.3f).BorderColor("#CBD5E1").Padding(4).Text(Text(value)).FontSize(7);
                    }
                });
            });
            page.Footer().PaddingTop(8).AlignCenter().Text(text =>
            {
                text.Span("AI Smart Inventory | Page ");
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        })).GeneratePdf();
    }
}
