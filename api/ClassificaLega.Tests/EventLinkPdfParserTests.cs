using ClassificaLega.Infrastructure.PdfImport;

namespace ClassificaLega.Tests;

public class EventLinkPdfParserTests
{
    private static ParsedPdf ParseSample()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "turno1.pdf");
        using var stream = File.OpenRead(path);
        return EventLinkPdfParser.Parse(stream);
    }

    [Fact]
    public void Parse_ExtractsMetadata()
    {
        var p = ParseSample();
        Assert.Equal("11180194", p.EventLinkId);
        Assert.Equal(1, p.StageNumber);
        Assert.Equal(new DateOnly(2026, 6, 9), p.EventDate);
        Assert.Equal("1° Tappa Della Lega Pauper Nakama Store", p.StageName);
    }

    [Fact]
    public void Parse_Extracts12Rows()
    {
        var p = ParseSample();
        Assert.Equal(12, p.Rows.Count);
    }

    [Fact]
    public void Parse_FirstAndLastRowsCorrect()
    {
        var p = ParseSample();

        var first = p.Rows[0];
        Assert.Equal(1, first.Position);
        Assert.Equal("michele pardini", first.Name);
        Assert.Equal(12, first.MatchPoints);
        Assert.Equal(52, first.WinPctOpp);
        Assert.Equal(88, first.GameWinPct);
        Assert.Equal(53, first.GameWinPctOpp);

        var last = p.Rows[^1];
        Assert.Equal(12, last.Position);
        Assert.Equal("Massimiliano Lombardi", last.Name);
        Assert.Equal(1, last.MatchPoints);
    }

    [Fact]
    public void Parse_PreservesNamesWithSpaces()
    {
        var p = ParseSample();
        Assert.Contains(p.Rows, r => r.Name == "Gianmarco Del Bucchia" && r.MatchPoints == 6);
        Assert.Contains(p.Rows, r => r.Name == "Gabriele Marraccini" && r.MatchPoints == 7);
    }
}
