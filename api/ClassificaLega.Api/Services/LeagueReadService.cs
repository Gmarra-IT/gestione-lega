using ClassificaLega.Api.Dtos;
using ClassificaLega.Api.Tenancy;
using ClassificaLega.Domain.Entities;
using ClassificaLega.Domain.Services;
using ClassificaLega.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClassificaLega.Api.Services;

public class LeagueReadService(AppDbContext db, LeagueContext league)
{
    // Stagione su cui pivotano le query: quella richiesta (X-Season-Id, se appartiene alla
    // lega) oppure, in mancanza, quella attiva.
    private async Task<Season?> CurrentSeasonAsync()
    {
        var leagueId = league.RequireLeagueId();
        if (league.RequestedSeasonId is int sid)
        {
            var requested = await db.Seasons.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == sid && s.LeagueId == leagueId);
            if (requested is not null) return requested;
        }
        return await db.Seasons.AsNoTracking().FirstOrDefaultAsync(s => s.LeagueId == leagueId && s.IsActive);
    }

    public async Task<SeasonDto?> GetSeasonAsync()
    {
        var s = await CurrentSeasonAsync();
        return s is null ? null : new SeasonDto(s.Id, s.Name, s.TotalStages, s.CountingStages, s.IsActive, s.ScoringRule);
    }

    // Tutte le stagioni della lega, più recenti prima (per il selettore).
    public async Task<IReadOnlyList<SeasonDto>> GetSeasonsAsync()
    {
        var leagueId = league.RequireLeagueId();
        return await db.Seasons.AsNoTracking()
            .Where(s => s.LeagueId == leagueId)
            .OrderByDescending(s => s.IsActive).ThenByDescending(s => s.CreatedAt).ThenByDescending(s => s.Id)
            .Select(s => new SeasonDto(s.Id, s.Name, s.TotalStages, s.CountingStages, s.IsActive, s.ScoringRule))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<StandingRowDto>?> GetStandingsAsync()
    {
        var season = await CurrentSeasonAsync();
        if (season is null) return null;

        var players = await db.Players.AsNoTracking()
            .Where(p => p.SeasonId == season.Id)
            .Select(p => new
            {
                p.Id,
                p.DisplayName,
                Tournaments = p.Results.Select(r => new
                {
                    r.StageId,
                    r.Stage.Number,
                    r.Stage.Name,
                    r.Stage.Date,
                    r.MatchPoints,
                    r.Position,
                }).ToList(),
            })
            .ToListAsync();

        var standings = ScoringService.ComputeStandings(
            players.Select(p => ToScoreData(p.Id, p.DisplayName,
                p.Tournaments.Select(t => new TournamentScore(
                    t.StageId, t.Name ?? $"Tappa {t.Number}", t.Date, t.Number, t.MatchPoints, t.Position)))),
            season.ScoringRule, season.CountingStages);

        return standings.Select(ToStandingRowDto).ToList();
    }

    private static PlayerScoreData ToScoreData(int id, string name, IEnumerable<TournamentScore> tournaments) =>
        new(id, name, tournaments.ToList());

    private static StandingRowDto ToStandingRowDto(StandingEntry s) =>
        new(s.Rank, s.PlayerId, s.DisplayName, s.TotalPoints, s.AbsoluteTotal,
            s.TournamentsPlayed, s.TournamentsCountedForTotal,
            s.BestResults.Select(b => new TournamentBreakdownDto(
                b.TournamentId, b.TournamentName, b.Date, b.MatchPoints,
                b.PositionBonus, b.ScoreBonus, b.ParticipationPoints, b.Total)).ToList());

    // Giocatori della stagione corrente, filtrati e paginati (per il picker server-side).
    // Filtro su NormalizedKey (accent/case-insensitive, stessa logica del match import).
    public async Task<PlayerPageDto?> GetPlayersAsync(string? search, int skip, int take)
    {
        var season = await CurrentSeasonAsync();
        if (season is null) return null;

        take = Math.Clamp(take, 1, 100);
        skip = Math.Max(skip, 0);

        var query = db.Players.AsNoTracking().Where(p => p.SeasonId == season.Id);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var key = DatabaseSeeder.Normalize(search);
            query = query.Where(p => p.NormalizedKey.Contains(key));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(p => p.DisplayName)
            .Skip(skip).Take(take)
            .Select(p => new PlayerLiteDto(p.Id, p.DisplayName))
            .ToListAsync();

        return new PlayerPageDto(items, total);
    }

    public async Task<IReadOnlyList<StageDto>?> GetStagesAsync()
    {
        var season = await CurrentSeasonAsync();
        if (season is null) return null;

        return await db.Stages.AsNoTracking()
            .Where(s => s.SeasonId == season.Id)
            .OrderBy(s => s.Number)
            .Select(s => new StageDto(s.Id, s.Number, s.Name, s.Date, s.EventLinkId, s.Results.Count))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<StageResultDto>?> GetStageResultsAsync(int stageNumber)
    {
        var season = await CurrentSeasonAsync();
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
                r.Wins,
                r.Draws,
                r.Losses,
                r.Position,
                r.MatchPoints,
                r.ScoreBonus,
                r.PositionBonus,
                r.ParticipationPoints,
                r.TotalPoints))
            .ToListAsync();
    }

    public async Task<ProgressionDto?> GetProgressionAsync(int playerId)
    {
        var season = await CurrentSeasonAsync();
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
        var season = await CurrentSeasonAsync();
        if (season is null) return null;

        var players = await db.Players.AsNoTracking()
            .Where(p => p.SeasonId == season.Id)
            .Select(p => new
            {
                p.Id,
                p.DisplayName,
                Cells = p.Results.Select(r => new
                {
                    r.StageId,
                    r.Stage.Number,
                    r.Stage.Name,
                    r.Stage.Date,
                    r.MatchPoints,
                    r.Position,
                    r.TotalPoints,
                }).ToList(),
            })
            .ToListAsync();

        var standings = ScoringService.ComputeStandings(
            players.Select(p => ToScoreData(p.Id, p.DisplayName,
                p.Cells.Select(c => new TournamentScore(
                    c.StageId, c.Name ?? $"Tappa {c.Number}", c.Date, c.Number, c.MatchPoints, c.Position)))),
            season.ScoringRule, season.CountingStages);

        var stageNumbers = Enumerable.Range(1, season.TotalStages).ToList();
        var playerById = players.ToDictionary(p => p.Id);

        var rows = standings.Select(s =>
        {
            var totalByNumber = playerById[s.PlayerId].Cells.ToDictionary(c => c.Number, c => c.TotalPoints);
            var cells = stageNumbers
                .Select(n => new MatrixCellDto(n, totalByNumber.TryGetValue(n, out var t) ? t : null))
                .ToList();
            return new MatrixRowDto(s.Rank, s.PlayerId, s.DisplayName, cells, s.TotalPoints, s.AbsoluteTotal);
        }).ToList();

        return new MatrixDto(season.TotalStages, season.CountingStages, stageNumbers, rows);
    }
}
