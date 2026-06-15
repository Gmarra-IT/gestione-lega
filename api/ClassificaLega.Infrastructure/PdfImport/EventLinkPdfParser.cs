using System.Globalization;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace ClassificaLega.Infrastructure.PdfImport;

/// <summary>One data row of an EventLink "Classifica per posizione" report.</summary>
public record ParsedRow(
    int Position,
    string Name,
    int MatchPoints,
    int? WinPctOpp,
    int? GameWinPct,
    int? GameWinPctOpp);

/// <summary>Result of parsing an EventLink PDF: metadata + data rows.</summary>
public record ParsedPdf(
    string? EventName,
    string? StageName,
    string? EventLinkId,
    DateOnly? EventDate,
    int? StageNumber,
    IReadOnlyList<ParsedRow> Rows);

/// <summary>
/// Parses the EventLink "Classifica per posizione" PDF (cols: Pos | Nome | Punti | %VIA | %VP | %VPA).
/// Pure: takes a stream, returns parsed data. Name matching against players happens in the service layer.
/// </summary>
public static partial class EventLinkPdfParser
{
    public static ParsedPdf Parse(Stream pdf)
    {
        var lines = ExtractLines(pdf);

        string? eventName = null, stageName = null, eventLinkId = null;
        DateOnly? eventDate = null;
        int? stageNumber = null;
        var rows = new List<ParsedRow>();

        foreach (var line in lines)
        {
            var evento = EventoRegex().Match(line);
            if (evento.Success)
            {
                eventName = evento.Groups[1].Value.Trim();
                eventLinkId = evento.Groups[2].Value;
                var tappa = TappaRegex().Match(eventName);
                if (tappa.Success) stageNumber = int.Parse(tappa.Groups[1].Value);
                continue;
            }

            var info = InfoRegex().Match(line);
            if (info.Success) { stageName = info.Groups[1].Value.Trim(); continue; }

            var data = DataRegex().Match(line);
            if (data.Success && DateOnly.TryParseExact(
                    data.Groups[1].Value, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            {
                eventDate = d;
                continue;
            }

            var row = RowRegex().Match(line);
            if (row.Success)
            {
                rows.Add(new ParsedRow(
                    int.Parse(row.Groups[1].Value),
                    CollapseSpaces(row.Groups[2].Value),
                    int.Parse(row.Groups[3].Value),
                    ParseNullableInt(row.Groups[4].Value),
                    ParseNullableInt(row.Groups[5].Value),
                    ParseNullableInt(row.Groups[6].Value)));
            }
        }

        return new ParsedPdf(eventName, stageName, eventLinkId, eventDate, stageNumber, rows);
    }

    /// <summary>Reconstructs visual text lines from PDF words, grouping by baseline Y then sorting by X.</summary>
    private static List<string> ExtractLines(Stream pdf)
    {
        using var doc = PdfDocument.Open(pdf);
        var lines = new List<string>();

        foreach (var page in doc.GetPages())
        {
            var groups = new List<LineGroup>();
            foreach (var w in page.GetWords().Where(w => !string.IsNullOrWhiteSpace(w.Text)))
            {
                var y = w.BoundingBox.Bottom;
                var g = groups.FirstOrDefault(g => Math.Abs(g.Y - y) <= 3.0);
                if (g is null) { g = new LineGroup(y); groups.Add(g); }
                g.Words.Add(w);
            }

            foreach (var g in groups.OrderByDescending(g => g.Y))
                lines.Add(string.Join(' ', g.Words.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)));
        }

        return lines;
    }

    private sealed class LineGroup(double y)
    {
        public double Y { get; } = y;
        public List<Word> Words { get; } = [];
    }

    private static string CollapseSpaces(string s) => WhitespaceRegex().Replace(s, " ").Trim();

    private static int? ParseNullableInt(string s) => int.TryParse(s, out var v) ? v : null;

    [GeneratedRegex(@"^Evento:\s*(.+?)\s*\((\d+)\)\s*$")]
    private static partial Regex EventoRegex();

    [GeneratedRegex(@"^Informazioni evento:\s*(.+)$")]
    private static partial Regex InfoRegex();

    [GeneratedRegex(@"^Data evento:\s*(\d{2}/\d{2}/\d{4})")]
    private static partial Regex DataRegex();

    [GeneratedRegex(@"^(\d+)\s+(.+?)\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)$")]
    private static partial Regex RowRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"Tappa\s+(\d+)")]
    private static partial Regex TappaRegex();
}
