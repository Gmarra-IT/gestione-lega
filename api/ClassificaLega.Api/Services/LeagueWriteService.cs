using ClassificaLega.Api.Dtos;
using ClassificaLega.Api.Tenancy;
using ClassificaLega.Domain.Entities;
using ClassificaLega.Domain.Services;
using ClassificaLega.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClassificaLega.Api.Services;

public class LeagueWriteService(AppDbContext db, LeagueContext league)
{
    private async Task<Season> ActiveSeasonAsync()
    {
        var leagueId = league.RequireLeagueId();
        return await db.Seasons.FirstOrDefaultAsync(s => s.LeagueId == leagueId && s.IsActive)
            ?? throw ApiException.NotFound("Nessuna stagione attiva.");
    }

    public async Task<SeasonDto> UpdateSeasonAsync(UpdateSeasonRequest req)
    {
        var season = await ActiveSeasonAsync();

        if (req.TotalStages < 1)
            throw ApiException.BadRequest("TotalStages deve essere >= 1.");
        if (req.CountingStages < 1 || req.CountingStages > req.TotalStages)
            throw ApiException.BadRequest("CountingStages deve essere tra 1 e TotalStages.");

        var maxStageNumber = await db.Stages
            .Where(s => s.SeasonId == season.Id)
            .Select(s => (int?)s.Number).MaxAsync() ?? 0;
        if (req.TotalStages < maxStageNumber)
            throw ApiException.BadRequest($"TotalStages non può essere < {maxStageNumber} (tappe già esistenti).");

        season.TotalStages = req.TotalStages;
        season.CountingStages = req.CountingStages;
        await db.SaveChangesAsync();

        return new SeasonDto(season.Id, season.Name, season.TotalStages, season.CountingStages, season.IsActive);
    }

    public async Task<StageDto> UpsertStageAsync(UpsertStageRequest req)
    {
        var season = await ActiveSeasonAsync();

        if (req.Number < 1 || req.Number > season.TotalStages)
            throw ApiException.BadRequest($"Number deve essere tra 1 e {season.TotalStages}.");

        var stage = await db.Stages
            .FirstOrDefaultAsync(s => s.SeasonId == season.Id && s.Number == req.Number);

        if (stage is null)
        {
            stage = new Stage
            {
                SeasonId = season.Id,
                Number = req.Number,
                Name = req.Name ?? $"Tappa {req.Number}",
                Date = req.Date,
                EventLinkId = req.EventLinkId,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.Stages.Add(stage);
        }
        else
        {
            if (req.Name is not null) stage.Name = req.Name;
            if (req.Date is not null) stage.Date = req.Date;
            if (req.EventLinkId is not null) stage.EventLinkId = req.EventLinkId;
        }

        await db.SaveChangesAsync();
        var count = await db.Results.CountAsync(r => r.StageId == stage.Id);
        return new StageDto(stage.Id, stage.Number, stage.Name, stage.Date, stage.EventLinkId, count);
    }

    public async Task<StageResultDto> UpsertResultAsync(UpsertResultRequest req)
    {
        var season = await ActiveSeasonAsync();
        ValidateMatchPoints(req.MatchPoints);

        var stage = await db.Stages
            .FirstOrDefaultAsync(s => s.SeasonId == season.Id && s.Number == req.StageNumber)
            ?? throw ApiException.NotFound($"Tappa {req.StageNumber} inesistente.");

        var player = await ResolvePlayerAsync(season, req.PlayerId, req.NewPlayerName);

        var result = await db.Results
            .FirstOrDefaultAsync(r => r.StageId == stage.Id && r.PlayerId == player.Id);
        if (result is null)
        {
            result = new Result
            {
                StageId = stage.Id,
                PlayerId = player.Id,
                MatchPoints = req.MatchPoints,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.Results.Add(result);
        }
        else
        {
            result.MatchPoints = req.MatchPoints;
        }
        await db.SaveChangesAsync();

        await RecomputePlayerAsync(player.Id);
        return await BuildResultDtoAsync(result.Id);
    }

    public async Task<StageResultDto> UpdateResultAsync(int resultId, UpdateResultRequest req)
    {
        ValidateMatchPoints(req.MatchPoints);
        var result = await LoadActiveResultAsync(resultId);

        result.MatchPoints = req.MatchPoints;
        await db.SaveChangesAsync();

        await RecomputePlayerAsync(result.PlayerId);
        return await BuildResultDtoAsync(result.Id);
    }

    public async Task DeleteResultAsync(int resultId)
    {
        var result = await LoadActiveResultAsync(resultId);
        var playerId = result.PlayerId;
        db.Results.Remove(result);
        await db.SaveChangesAsync();
        await RecomputePlayerAsync(playerId);
    }

    // --- helpers ---

    private static void ValidateMatchPoints(int mp)
    {
        if (mp < 0 || mp > 12)
            throw ApiException.BadRequest("MatchPoints deve essere tra 0 e 12.");
    }

    private async Task<Player> ResolvePlayerAsync(Season season, int? playerId, string? newPlayerName)
    {
        if (playerId is { } id)
        {
            return await db.Players.FirstOrDefaultAsync(p => p.Id == id && p.SeasonId == season.Id)
                ?? throw ApiException.NotFound($"Giocatore {id} inesistente.");
        }

        if (string.IsNullOrWhiteSpace(newPlayerName))
            throw ApiException.BadRequest("Serve PlayerId o NewPlayerName.");

        var key = DatabaseSeeder.Normalize(newPlayerName);
        var exists = await db.Players.AnyAsync(p => p.SeasonId == season.Id && p.NormalizedKey == key);
        if (exists)
            throw ApiException.Conflict($"Giocatore '{newPlayerName}' esiste già.");

        var player = new Player
        {
            SeasonId = season.Id,
            DisplayName = newPlayerName.Trim(),
            NormalizedKey = key,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Players.Add(player);
        await db.SaveChangesAsync();
        return player;
    }

    private async Task<Result> LoadActiveResultAsync(int resultId)
    {
        var season = await ActiveSeasonAsync();
        return await db.Results
            .Include(r => r.Stage)
            .FirstOrDefaultAsync(r => r.Id == resultId && r.Stage.SeasonId == season.Id)
            ?? throw ApiException.NotFound($"Risultato {resultId} inesistente.");
    }

    /// <summary>Recompute bonus risultato/partecipazione and total for every result of a player,
    /// since participation bonus depends on the player's ordered stage history.</summary>
    public async Task RecomputePlayerAsync(int playerId)
    {
        var results = await db.Results
            .Where(r => r.PlayerId == playerId)
            .Select(r => new { Result = r, r.Stage.Number })
            .ToListAsync();

        var bonusByStageId = ScoringService.RecomputePartecipazione(
            results.Select(x => (x.Result.StageId, x.Number)));

        foreach (var x in results)
        {
            var r = x.Result;
            r.BonusRisultato = ScoringService.BonusRisultato(r.MatchPoints);
            r.BonusPartecipazione = bonusByStageId[r.StageId];
            r.TotalPoints = ScoringService.ComputeTotalPoints(r.MatchPoints, r.BonusRisultato, r.BonusPartecipazione);
            r.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync();
    }

    private async Task<StageResultDto> BuildResultDtoAsync(int resultId)
    {
        return await db.Results.AsNoTracking()
            .Where(r => r.Id == resultId)
            .Select(r => new StageResultDto(
                r.Id,
                r.PlayerId,
                r.Player.DisplayName,
                r.MatchPoints,
                r.BonusRisultato,
                r.BonusPartecipazione,
                r.TotalPoints))
            .FirstAsync();
    }
}
