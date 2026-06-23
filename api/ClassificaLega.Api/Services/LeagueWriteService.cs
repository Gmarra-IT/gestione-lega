using ClassificaLega.Api.Dtos;
using ClassificaLega.Api.Tenancy;
using ClassificaLega.Domain.Entities;
using ClassificaLega.Domain.Services;
using ClassificaLega.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClassificaLega.Api.Services;

public class LeagueWriteService(AppDbContext db, LeagueContext league)
{
    // Stagione su cui operano le scritture: quella richiesta (X-Season-Id, se appartiene alla
    // lega) oppure, in mancanza, quella attiva. Permette di gestire anche stagioni precedenti.
    private async Task<Season> CurrentSeasonAsync()
    {
        var leagueId = league.RequireLeagueId();
        if (league.RequestedSeasonId is int sid)
        {
            var requested = await db.Seasons.FirstOrDefaultAsync(s => s.Id == sid && s.LeagueId == leagueId);
            if (requested is not null) return requested;
        }
        return await db.Seasons.FirstOrDefaultAsync(s => s.LeagueId == leagueId && s.IsActive)
            ?? throw ApiException.NotFound("Nessuna stagione attiva.");
    }

    public async Task<SeasonDto> CreateSeasonAsync(CreateSeasonRequest req)
    {
        var leagueId = league.RequireLeagueId();
        if (string.IsNullOrWhiteSpace(req.Name))
            throw ApiException.BadRequest("Nome stagione obbligatorio.");
        if (req.TotalStages < 1)
            throw ApiException.BadRequest("TotalStages deve essere >= 1.");
        if (req.CountingStages < 1 || req.CountingStages > req.TotalStages)
            throw ApiException.BadRequest("CountingStages deve essere tra 1 e TotalStages.");

        // Archivia le stagioni attive correnti: una sola attiva per lega.
        var actives = await db.Seasons.Where(s => s.LeagueId == leagueId && s.IsActive).ToListAsync();
        foreach (var a in actives) a.IsActive = false;

        var season = new Season
        {
            LeagueId = leagueId,
            Name = req.Name.Trim(),
            TotalStages = req.TotalStages,
            CountingStages = req.CountingStages,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Seasons.Add(season);
        await db.SaveChangesAsync();

        return ToSeasonDto(season);
    }

    public async Task<SeasonDto> UpdateSeasonAsync(UpdateSeasonRequest req)
    {
        var season = await CurrentSeasonAsync();

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

        return ToSeasonDto(season);
    }

    /// <summary>Sostituisce l'intera ScoringRule della stagione e ricalcola tutti i risultati.</summary>
    public async Task<SeasonDto> UpdateScoringRuleAsync(UpdateScoringRuleRequest req)
    {
        var season = await CurrentSeasonAsync();
        var rule = req.ScoringRule ?? throw ApiException.BadRequest("ScoringRule obbligatoria.");
        if (rule.Validate() is { } err) throw ApiException.BadRequest(err);

        season.ScoringRule = rule;
        await db.SaveChangesAsync();

        // I bonus dipendono dalla regola → ricalcola tutti i giocatori della stagione.
        var playerIds = await db.Players.Where(p => p.SeasonId == season.Id).Select(p => p.Id).ToListAsync();
        foreach (var pid in playerIds)
            await RecomputePlayerAsync(pid);

        return ToSeasonDto(season);
    }

    public async Task<StageDto> UpsertStageAsync(UpsertStageRequest req)
    {
        var season = await CurrentSeasonAsync();

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
        var season = await CurrentSeasonAsync();
        var matchPoints = ResolveMatchPoints(season.ScoringRule, req.MatchPoints, req.Wins, req.Draws, req.Losses);

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
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.Results.Add(result);
        }
        ApplyInputs(result, matchPoints, req.Wins, req.Draws, req.Losses, req.Position);
        await db.SaveChangesAsync();

        await RecomputePlayerAsync(player.Id);
        return await BuildResultDtoAsync(result.Id);
    }

    public async Task<StageResultDto> UpdateResultAsync(int resultId, UpdateResultRequest req)
    {
        var season = await CurrentSeasonAsync();
        var matchPoints = ResolveMatchPoints(season.ScoringRule, req.MatchPoints, req.Wins, req.Draws, req.Losses);
        var result = await LoadActiveResultAsync(resultId);

        ApplyInputs(result, matchPoints, req.Wins, req.Draws, req.Losses, req.Position);
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

    private static SeasonDto ToSeasonDto(Season s) =>
        new(s.Id, s.Name, s.TotalStages, s.CountingStages, s.IsActive, s.ScoringRule);

    // Deriva i match points da W/D/L se tutti valorizzati, altrimenti usa MatchPoints diretto.
    private static int ResolveMatchPoints(ScoringRule rule, int? matchPoints, int? wins, int? draws, int? losses)
    {
        if (wins is { } w && draws is { } d && losses is { } l)
        {
            if (w < 0 || d < 0 || l < 0)
                throw ApiException.BadRequest("Wins/Draws/Losses devono essere >= 0.");
            return ScoringService.MatchPoints(w, d, l, rule);
        }
        if (matchPoints is { } mp)
        {
            if (mp < 0) throw ApiException.BadRequest("MatchPoints deve essere >= 0.");
            return mp;
        }
        throw ApiException.BadRequest("Serve MatchPoints oppure Wins/Draws/Losses.");
    }

    private static void ApplyInputs(Result result, int matchPoints, int? wins, int? draws, int? losses, int? position)
    {
        if (position is <= 0)
            throw ApiException.BadRequest("Position deve essere > 0.");
        result.MatchPoints = matchPoints;
        result.Wins = wins;
        result.Draws = draws;
        result.Losses = losses;
        result.Position = position;
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
        var season = await CurrentSeasonAsync();
        return await db.Results
            .Include(r => r.Stage)
            .FirstOrDefaultAsync(r => r.Id == resultId && r.Stage.SeasonId == season.Id)
            ?? throw ApiException.NotFound($"Risultato {resultId} inesistente.");
    }

    /// <summary>Ricalcola scoreBonus/positionBonus/participationPoints e totale per ogni result del
    /// giocatore: la fascia presenza dipende dalla storia ordinata (data, poi numero tappa, poi id).</summary>
    public async Task RecomputePlayerAsync(int playerId)
    {
        var results = await db.Results
            .Where(r => r.PlayerId == playerId)
            .Include(r => r.Stage)
            .ToListAsync();
        if (results.Count == 0) return;

        var seasonId = results[0].Stage.SeasonId;
        var rule = (await db.Seasons.AsNoTracking().FirstAsync(s => s.Id == seasonId)).ScoringRule;

        var ordered = results
            .OrderBy(r => r.Stage.Date ?? DateOnly.MaxValue)
            .ThenBy(r => r.Stage.Number)
            .ThenBy(r => r.StageId)
            .ToList();

        for (int i = 0; i < ordered.Count; i++)
        {
            var r = ordered[i];
            r.ScoreBonus = ScoringService.ScoreBonusFor(r.MatchPoints, rule);
            r.PositionBonus = ScoringService.PositionBonusFor(r.Position, rule);
            r.ParticipationPoints = ScoringService.ParticipationPointsFor(i + 1, rule);
            r.TotalPoints = r.MatchPoints + r.ScoreBonus + r.PositionBonus + r.ParticipationPoints;
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
                r.Wins,
                r.Draws,
                r.Losses,
                r.Position,
                r.MatchPoints,
                r.ScoreBonus,
                r.PositionBonus,
                r.ParticipationPoints,
                r.TotalPoints))
            .FirstAsync();
    }
}
