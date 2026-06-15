using ClassificaLega.Api.Dtos;
using ClassificaLega.Domain.Entities;
using ClassificaLega.Infrastructure.PdfImport;
using ClassificaLega.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClassificaLega.Api.Services;

/// <summary>Import of EventLink "Classifica per posizione" PDFs: stateless preview + reviewed commit.</summary>
public class LeagueImportService(AppDbContext db, LeagueWriteService write)
{
    private async Task<Season> ActiveSeasonAsync() =>
        await db.Seasons.FirstOrDefaultAsync(s => s.IsActive)
        ?? throw ApiException.NotFound("Nessuna stagione attiva.");

    public async Task<ImportPreviewResponse> PreviewAsync(Stream pdf)
    {
        var season = await ActiveSeasonAsync();
        var parsed = EventLinkPdfParser.Parse(pdf);
        if (parsed.Rows.Count == 0)
            throw ApiException.BadRequest("Nessuna riga riconosciuta nel PDF.");

        var players = await db.Players
            .Where(p => p.SeasonId == season.Id)
            .Select(p => new { p.Id, p.NormalizedKey, p.DisplayName })
            .ToListAsync();
        var byKey = players.ToDictionary(p => p.NormalizedKey, p => p);

        var rows = parsed.Rows.Select(r =>
        {
            var key = DatabaseSeeder.Normalize(r.Name);
            byKey.TryGetValue(key, out var m);
            return new ImportPreviewRow(r.Position, r.Name, r.MatchPoints, m?.Id, m?.DisplayName, m is null);
        }).ToList();

        var stageExists = false;
        var existingCount = 0;
        if (parsed.StageNumber is { } n)
        {
            var stage = await db.Stages.FirstOrDefaultAsync(s => s.SeasonId == season.Id && s.Number == n);
            if (stage is not null)
            {
                stageExists = true;
                existingCount = await db.Results.CountAsync(r => r.StageId == stage.Id);
            }
        }

        return new ImportPreviewResponse(
            parsed.StageNumber, parsed.StageName, parsed.EventDate, parsed.EventLinkId,
            stageExists, existingCount, rows);
    }

    public async Task<ImportCommitResponse> CommitAsync(ImportCommitRequest req)
    {
        var season = await ActiveSeasonAsync();

        if (req.StageNumber < 1 || req.StageNumber > season.TotalStages)
            throw ApiException.BadRequest($"Numero tappa deve essere tra 1 e {season.TotalStages}.");
        if (req.Rows.Count == 0)
            throw ApiException.BadRequest("Nessuna riga da importare.");
        foreach (var r in req.Rows)
            if (r.MatchPoints < 0 || r.MatchPoints > 12)
                throw ApiException.BadRequest($"Punti non validi per '{r.Name}' (0–12).");

        await using var tx = await db.Database.BeginTransactionAsync();

        var stage = await db.Stages.FirstOrDefaultAsync(s => s.SeasonId == season.Id && s.Number == req.StageNumber);
        if (stage is null)
        {
            stage = new Stage
            {
                SeasonId = season.Id,
                Number = req.StageNumber,
                Name = req.StageName ?? $"Tappa {req.StageNumber}",
                Date = req.EventDate,
                EventLinkId = req.EventLinkId,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.Stages.Add(stage);
            await db.SaveChangesAsync();
        }
        else
        {
            if (req.StageName is not null) stage.Name = req.StageName;
            if (req.EventDate is not null) stage.Date = req.EventDate;
            if (req.EventLinkId is not null) stage.EventLinkId = req.EventLinkId;
        }

        var affected = new HashSet<int>();

        var existing = await db.Results.Where(r => r.StageId == stage.Id).ToListAsync();
        var replaced = existing.Count;
        if (existing.Count > 0)
        {
            if (!req.Overwrite)
                throw ApiException.Conflict(
                    $"La tappa {req.StageNumber} ha già {existing.Count} risultati. Conferma la sovrascrittura.");
            foreach (var e in existing) affected.Add(e.PlayerId);
            db.Results.RemoveRange(existing);
            await db.SaveChangesAsync();
        }

        var playersCreated = 0;
        var createdKeys = new Dictionary<string, int>();
        var seen = new HashSet<int>();
        var results = new List<Result>();

        foreach (var row in req.Rows)
        {
            int playerId;
            if (row.PlayerId is { } pid)
            {
                if (!await db.Players.AnyAsync(p => p.Id == pid && p.SeasonId == season.Id))
                    throw ApiException.NotFound($"Giocatore {pid} inesistente.");
                playerId = pid;
            }
            else
            {
                var key = DatabaseSeeder.Normalize(row.Name);
                if (createdKeys.TryGetValue(key, out var cid))
                {
                    playerId = cid;
                }
                else
                {
                    var match = await db.Players.FirstOrDefaultAsync(p => p.SeasonId == season.Id && p.NormalizedKey == key);
                    if (match is not null)
                    {
                        playerId = match.Id;
                    }
                    else
                    {
                        var np = new Player
                        {
                            SeasonId = season.Id,
                            DisplayName = row.Name.Trim(),
                            NormalizedKey = key,
                            CreatedAt = DateTimeOffset.UtcNow,
                        };
                        db.Players.Add(np);
                        await db.SaveChangesAsync();
                        playerId = np.Id;
                        playersCreated++;
                        createdKeys[key] = playerId;
                    }
                }
            }

            if (!seen.Add(playerId))
                throw ApiException.BadRequest($"Giocatore duplicato nelle righe ('{row.Name}').");

            affected.Add(playerId);
            results.Add(new Result
            {
                StageId = stage.Id,
                PlayerId = playerId,
                MatchPoints = row.MatchPoints,
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }

        db.Results.AddRange(results);
        await db.SaveChangesAsync();

        foreach (var pid in affected)
            await write.RecomputePlayerAsync(pid);

        await tx.CommitAsync();
        return new ImportCommitResponse(req.StageNumber, results.Count, replaced, playersCreated);
    }
}
