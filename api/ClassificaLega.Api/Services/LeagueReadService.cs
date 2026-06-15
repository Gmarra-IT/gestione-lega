using ClassificaLega.Api.Dtos;
using ClassificaLega.Domain.Entities;
using ClassificaLega.Domain.Services;
using ClassificaLega.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClassificaLega.Api.Services;

public class LeagueReadService(AppDbContext db)
{
    private Task<Season?> ActiveSeasonAsync() =>
        db.Seasons.AsNoTracking().FirstOrDefaultAsync(s => s.IsActive);

    public async Task<SeasonDto?> GetSeasonAsync()
    {
        var s = await ActiveSeasonAsync();
        return s is null ? null : new SeasonDto(s.Id, s.Name, s.TotalStages, s.CountingStages, s.IsActive);
    }

    public async Task<IReadOnlyList<StandingRowDto>?> GetStandingsAsync()
    {
        var season = await ActiveSeasonAsync();
        if (season is null) return null;

        var players = await db.Players.AsNoTracking()
            .Where(p => p.SeasonId == season.Id)
            .Select(p => new
            {
                p.Id,
                p.DisplayName,
                Totals = p.Results.Select(r => r.TotalPoints).ToList(),
            })
            .ToListAsync();

        var standings = ScoringService.ComputeStandings(
            players.Select(p => new PlayerScoreData(p.Id, p.DisplayName, p.Totals)),
            season.CountingStages);

        return standings
            .Select(s => new StandingRowDto(s.Position, s.PlayerId, s.DisplayName, s.BestN, s.AbsoluteTotal))
            .ToList();
    }

    public async Task<IReadOnlyList<StageDto>?> GetStagesAsync()
    {
        var season = await ActiveSeasonAsync();
        if (season is null) return null;

        return await db.Stages.AsNoTracking()
            .Where(s => s.SeasonId == season.Id)
            .OrderBy(s => s.Number)
            .Select(s => new StageDto(s.Id, s.Number, s.Name, s.Date, s.EventLinkId, s.Results.Count))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<StageResultDto>?> GetStageResultsAsync(int stageNumber)
    {
        var season = await ActiveSeasonAsync();
        if (season is null) return null;

        var stage = await db.Stages.AsNoTracking()
            .FirstOrDefaultAsync(s => s.SeasonId == season.Id && s.Number == stageNumber);
        if (stage is null) return null;

        return await db.Results.AsNoTracking()
            .Where(r => r.StageId == stage.Id)
            .OrderByDescending(r => r.TotalPoints)
            .ThenBy(r => r.Player.DisplayName)
            .Select(r => new StageResultDto(
                r.Id,
                r.PlayerId,
                r.Player.DisplayName,
                r.MatchPoints,
                r.BonusRisultato,
                r.BonusPartecipazione,
                r.TotalPoints))
            .ToListAsync();
    }

    public async Task<ProgressionDto?> GetProgressionAsync(int playerId)
    {
        var season = await ActiveSeasonAsync();
        if (season is null) return null;

        var player = await db.Players.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == playerId && p.SeasonId == season.Id);
        if (player is null) return null;

        // map stageNumber -> stage total for this player
        var byStage = await db.Results.AsNoTracking()
            .Where(r => r.PlayerId == playerId)
            .Select(r => new { r.Stage.Number, r.TotalPoints })
            .ToListAsync();
        var totalByNumber = byStage.ToDictionary(x => x.Number, x => x.TotalPoints);

        var points = new List<ProgressionPointDto>();
        int cumulative = 0;
        for (int n = 1; n <= season.TotalStages; n++)
        {
            int? stageTotal = totalByNumber.TryGetValue(n, out var t) ? t : null;
            cumulative += stageTotal ?? 0;
            points.Add(new ProgressionPointDto(n, stageTotal, cumulative));
        }

        return new ProgressionDto(player.Id, player.DisplayName, points);
    }

    public async Task<MatrixDto?> GetMatrixAsync()
    {
        var season = await ActiveSeasonAsync();
        if (season is null) return null;

        var players = await db.Players.AsNoTracking()
            .Where(p => p.SeasonId == season.Id)
            .Select(p => new
            {
                p.Id,
                p.DisplayName,
                Cells = p.Results.Select(r => new { r.Stage.Number, r.TotalPoints }).ToList(),
            })
            .ToListAsync();

        var standings = ScoringService.ComputeStandings(
            players.Select(p => new PlayerScoreData(p.Id, p.DisplayName, p.Cells.Select(c => c.TotalPoints).ToList())),
            season.CountingStages);

        var stageNumbers = Enumerable.Range(1, season.TotalStages).ToList();
        var playerById = players.ToDictionary(p => p.Id);

        var rows = standings.Select(s =>
        {
            var totalByNumber = playerById[s.PlayerId].Cells.ToDictionary(c => c.Number, c => c.TotalPoints);
            var cells = stageNumbers
                .Select(n => new MatrixCellDto(n, totalByNumber.TryGetValue(n, out var t) ? t : null))
                .ToList();
            return new MatrixRowDto(s.Position, s.PlayerId, s.DisplayName, cells, s.BestN, s.AbsoluteTotal);
        }).ToList();

        return new MatrixDto(season.TotalStages, season.CountingStages, stageNumbers, rows);
    }
}
