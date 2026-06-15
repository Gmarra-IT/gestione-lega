namespace ClassificaLega.Api.Dtos;

/// <summary>One reviewed row of an import preview: parsed name + match against existing players.</summary>
public record ImportPreviewRow(
    int Position,
    string Name,
    int MatchPoints,
    int? MatchedPlayerId,
    string? MatchedPlayerName,
    bool IsNew);

/// <summary>Stateless preview: parsed metadata + rows. The client reviews and posts back a commit.</summary>
public record ImportPreviewResponse(
    int? StageNumber,
    string? StageName,
    DateOnly? EventDate,
    string? EventLinkId,
    bool StageExists,
    int ExistingResultCount,
    IReadOnlyList<ImportPreviewRow> Rows);

/// <summary>One row the client confirmed for commit. PlayerId null = create a new player from Name.</summary>
public record ImportCommitRow(string Name, int MatchPoints, int? PlayerId);

public record ImportCommitRequest(
    int StageNumber,
    string? StageName,
    DateOnly? EventDate,
    string? EventLinkId,
    bool Overwrite,
    IReadOnlyList<ImportCommitRow> Rows);

public record ImportCommitResponse(int StageNumber, int Imported, int Replaced, int PlayersCreated);
