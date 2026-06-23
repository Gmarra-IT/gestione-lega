using ClassificaLega.Domain.Entities;
using ClassificaLega.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace ClassificaLega.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    // Match points per player per stage (stageIndex 0..6 = tappe 1..7).
    // Source: workbook "Lega pauper - Gestione classifica.xlsm", foglio Tappe (reverse-engineered
    // dai totali-tappa: total = matchPoints + BonusRisultato + BonusPartecipazione).
    // null = giocatore assente a quella tappa.
    private static readonly (string DisplayName, int?[] MatchPoints)[] PlayerData =
    [
        ("Bruno Barbieri",          [null,  9,  6,  9,  6, null, null]),
        ("Daniel Gemignani",        [null, null,  3, null, null,  6,  3]),
        ("Daniele Gambini",         [null,  9, null, null, null, null, null]),
        ("Dario Tommasi",           [null,  6,  9,  6, null, null,  6]),
        ("Gabriele Marraccini",     [ 9,  3,  7, 10,  9,  9,  9]),
        ("Gianmarco Bina",          [null,  4, null, null, null, null, null]),
        ("Gianmarco Del Bucchia",   [ 7,  0, 12,  6,  6,  9,  4]),
        ("Gianmarco Venturini",     [null, null,  3, null, null, null, null]),
        ("Gianmarco Volpe",         [null, null,  6,  6,  3,  4, null]),
        ("Gioca Turo",              [null, 12, null, null, null, null, null]),
        ("Giulio Bertozzi",         [ 0, null,  0, null, null, null, null]),
        ("Igor Fustini",            [ 9,  4,  6,  9,  6,  3, null]),
        ("Jhonathan Lipparelli",    [ 4,  6, null, null, null, null, null]),
        ("Leonardo Guerra Silicani",[null,  9, null, null, null, null, null]),
        ("Massimiliano Lombardi",   [null, null, null, null, null,  1, null]),
        ("Michele Pardini",         [null, null,  6,  6,  6, null, null]),
        ("Nicola Dalle Mura",       [null,  6,  9,  3, 12,  6,  9]),
        ("Nicola Pardini",          [null,  3,  3,  9, null,  9,  6]),
        ("Paolo Baroni",            [ 6,  6,  7,  4,  6,  9,  6]),
        ("Roberto Randazzo",        [null,  6,  6,  3,  3, null,  0]),
        ("Stefano Ghiara",          [null, null, null, null, null, null,  7]),
        ("Tommaso Duccini",         [null, null, null, null, null,  6,  9]),
    ];

    // Slug della lega creata dal seed (anche backfill nella migration MultiLeague).
    public const string SeedLeagueSlug = "massarosa";

    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Seasons.AnyAsync()) return;

        var league = await db.Leagues.FirstOrDefaultAsync(l => l.Slug == SeedLeagueSlug);
        if (league is null)
        {
            league = new League
            {
                Slug = SeedLeagueSlug,
                Name = "Lega Pauper Massarosa",
                Title = "Lega Pauper · Massarosa",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.Leagues.Add(league);
            await db.SaveChangesAsync();
        }

        var season = new Season
        {
            LeagueId = league.Id,
            Name = "Lega Pauper Massarosa 2026",
            TotalStages = 12,
            CountingStages = 8,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Seasons.Add(season);
        await db.SaveChangesAsync();

        var stages = Enumerable.Range(1, 7).Select(n => new Stage
        {
            SeasonId = season.Id,
            Number = n,
            Name = $"Tappa {n}",
            CreatedAt = DateTimeOffset.UtcNow,
        }).ToList();
        db.Stages.AddRange(stages);
        await db.SaveChangesAsync();

        var players = PlayerData.Select(p => new Player
        {
            SeasonId = season.Id,
            DisplayName = p.DisplayName,
            NormalizedKey = Normalize(p.DisplayName),
            CreatedAt = DateTimeOffset.UtcNow,
        }).ToList();
        db.Players.AddRange(players);
        await db.SaveChangesAsync();

        var rule = season.ScoringRule;
        var results = new List<Result>();
        for (int pi = 0; pi < PlayerData.Length; pi++)
        {
            var (_, matchPointsPerStage) = PlayerData[pi];
            var player = players[pi];

            // tappe giocate, in ordine cronologico (= numero tappa: le tappe seed non hanno data).
            var participatedStages = matchPointsPerStage
                .Select((mp, idx) => (mp, stageId: stages[idx].Id))
                .Where(x => x.mp.HasValue)
                .ToList();

            for (int i = 0; i < participatedStages.Count; i++)
            {
                var (mp, stageId) = participatedStages[i];
                var scoreBonus = ScoringService.ScoreBonusFor(mp!.Value, rule);
                var participation = ScoringService.ParticipationPointsFor(i + 1, rule);
                results.Add(new Result
                {
                    StageId = stageId,
                    PlayerId = player.Id,
                    MatchPoints = mp.Value,
                    ScoreBonus = scoreBonus,
                    PositionBonus = 0,
                    ParticipationPoints = participation,
                    TotalPoints = mp.Value + scoreBonus + participation,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                });
            }
        }

        db.Results.AddRange(results);
        await db.SaveChangesAsync();
    }

    /// <summary>Garantisce (idempotente) il super-admin globale dalle env e l'admin della lega seed,
    /// così le credenziali esistenti continuano a funzionare. Eseguito a ogni avvio.</summary>
    public static async Task EnsureUsersAsync(AppDbContext db, string adminUsername, string adminPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(adminUsername) || string.IsNullOrWhiteSpace(adminPasswordHash))
            return;

        // Super-admin globale (LeagueId null).
        var hasSuperAdmin = await db.Users.AnyAsync(u => u.Username.ToLower() == adminUsername.ToLower() && u.LeagueId == null);
        if (!hasSuperAdmin)
        {
            db.Users.Add(new User
            {
                Username = adminUsername,
                PasswordHash = adminPasswordHash,
                Role = UserRoles.SuperAdmin,
                LeagueId = null,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        // Admin della lega seed (massarosa), se la lega esiste.
        var seedLeague = await db.Leagues.FirstOrDefaultAsync(l => l.Slug == SeedLeagueSlug);
        if (seedLeague is not null)
        {
            var hasLeagueAdmin = await db.Users.AnyAsync(u => u.Username.ToLower() == adminUsername.ToLower() && u.LeagueId == seedLeague.Id);
            if (!hasLeagueAdmin)
            {
                db.Users.Add(new User
                {
                    Username = adminUsername,
                    PasswordHash = adminPasswordHash,
                    Role = UserRoles.LeagueAdmin,
                    LeagueId = seedLeague.Id,
                    CreatedAt = DateTimeOffset.UtcNow,
                });
                await db.SaveChangesAsync();
            }
        }
    }

    public static string Normalize(string name) =>
        System.Text.RegularExpressions.Regex.Replace(
            name.Normalize(System.Text.NormalizationForm.FormD)
                .ToLowerInvariant(),
            @"[\p{M}\s]+", m => m.Value == " " || m.Value.All(char.IsWhiteSpace) ? " " : "")
        .Trim();
}
