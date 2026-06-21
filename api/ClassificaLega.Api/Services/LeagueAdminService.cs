using System.Text.RegularExpressions;
using ClassificaLega.Api.Dtos;
using ClassificaLega.Domain.Entities;
using ClassificaLega.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClassificaLega.Api.Services;

/// <summary>Gestione delle leghe e dei loro admin (operazioni super-admin) + elenco pubblico.</summary>
public partial class LeagueAdminService(AppDbContext db)
{
    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugRegex();

    // Elenco pubblico: solo leghe attive, per picker e selettore.
    public async Task<IReadOnlyList<LeagueDto>> GetActiveLeaguesAsync() =>
        await db.Leagues.AsNoTracking()
            .Where(l => l.IsActive)
            .OrderBy(l => l.Name)
            .Select(l => new LeagueDto(l.Id, l.Slug, l.Name, l.Title, l.IsActive,
                db.LeagueLogos.Any(x => x.LeagueId == l.Id)))
            .ToListAsync();

    // Elenco completo (super-admin): incluse leghe disattivate.
    public async Task<IReadOnlyList<LeagueDto>> GetAllLeaguesAsync() =>
        await db.Leagues.AsNoTracking()
            .OrderBy(l => l.Name)
            .Select(l => new LeagueDto(l.Id, l.Slug, l.Name, l.Title, l.IsActive,
                db.LeagueLogos.Any(x => x.LeagueId == l.Id)))
            .ToListAsync();

    // --- logo ---

    // Byte del logo (endpoint pubblico). null se la lega non ha logo.
    public async Task<LeagueLogoData?> GetLogoAsync(string slug)
    {
        var logo = await db.LeagueLogos.AsNoTracking()
            .Where(x => x.League.Slug == slug)
            .Select(x => new LeagueLogoData(x.Bytes, x.ContentType, x.ETag))
            .FirstOrDefaultAsync();
        return logo;
    }

    // Tipi immagine ammessi per il logo.
    private static readonly Dictionary<string, string> AllowedLogoTypes = new()
    {
        ["image/png"] = "png",
        ["image/jpeg"] = "jpeg",
        ["image/webp"] = "webp",
        ["image/svg+xml"] = "svg",
    };
    // Backstop: il client comprime/ridimensiona prima dell'upload, ma teniamo un
    // limite lato server (i byte vivono nel DB) per quando la compressione fallisce.
    private const int MaxLogoBytes = 1024 * 1024;

    // Carica/sostituisce il logo della lega. Valida tipo e dimensione.
    public async Task SetLogoAsync(int leagueId, Stream content, string? contentType)
    {
        var type = (contentType ?? string.Empty).Split(';')[0].Trim().ToLowerInvariant();
        if (!AllowedLogoTypes.ContainsKey(type))
            throw ApiException.BadRequest("Formato non valido. Ammessi: PNG, JPEG, WebP, SVG.");
        if (!await db.Leagues.AnyAsync(l => l.Id == leagueId))
            throw ApiException.NotFound($"Lega {leagueId} inesistente.");

        using var ms = new MemoryStream();
        await content.CopyToAsync(ms);
        if (ms.Length == 0)
            throw ApiException.BadRequest("File vuoto.");
        if (ms.Length > MaxLogoBytes)
            throw ApiException.BadRequest("Immagine troppo grande (max 1 MB).");

        var bytes = ms.ToArray();
        var etag = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(bytes))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        var logo = await db.LeagueLogos.FirstOrDefaultAsync(x => x.LeagueId == leagueId);
        if (logo is null)
        {
            logo = new LeagueLogo { LeagueId = leagueId };
            db.LeagueLogos.Add(logo);
        }
        logo.Bytes = bytes;
        logo.ContentType = type;
        logo.ETag = etag;
        logo.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task DeleteLogoAsync(int leagueId)
    {
        var logo = await db.LeagueLogos.FirstOrDefaultAsync(x => x.LeagueId == leagueId);
        if (logo is null) return;
        db.LeagueLogos.Remove(logo);
        await db.SaveChangesAsync();
    }

    public async Task<LeagueDto> CreateLeagueAsync(CreateLeagueRequest req)
    {
        var slug = (req.Slug ?? string.Empty).Trim().ToLowerInvariant();
        if (!SlugRegex().IsMatch(slug) || slug.Length > 60)
            throw ApiException.BadRequest("Slug non valido (minuscole, numeri e trattini, es. 'pisa').");
        if (string.IsNullOrWhiteSpace(req.Name))
            throw ApiException.BadRequest("Nome lega obbligatorio.");
        if (await db.Leagues.AnyAsync(l => l.Slug == slug))
            throw ApiException.Conflict($"Slug '{slug}' già in uso.");

        var league = new League
        {
            Slug = slug,
            Name = req.Name.Trim(),
            Title = string.IsNullOrWhiteSpace(req.Title) ? null : req.Title.Trim(),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Leagues.Add(league);
        await db.SaveChangesAsync();

        // Stagione attiva iniziale così la lega è subito utilizzabile.
        db.Seasons.Add(new Season
        {
            LeagueId = league.Id,
            Name = league.Name,
            TotalStages = 12,
            CountingStages = 8,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        // Lega appena creata: nessun logo.
        return new LeagueDto(league.Id, league.Slug, league.Name, league.Title, league.IsActive, false);
    }

    public async Task<LeagueDto> UpdateLeagueAsync(int id, UpdateLeagueRequest req)
    {
        var league = await db.Leagues.FirstOrDefaultAsync(l => l.Id == id)
            ?? throw ApiException.NotFound($"Lega {id} inesistente.");

        if (req.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                throw ApiException.BadRequest("Nome lega non può essere vuoto.");
            league.Name = req.Name.Trim();
        }
        if (req.Title is not null)
            league.Title = string.IsNullOrWhiteSpace(req.Title) ? null : req.Title.Trim();
        if (req.IsActive is { } active)
            league.IsActive = active;

        await db.SaveChangesAsync();
        var hasLogo = await db.LeagueLogos.AnyAsync(x => x.LeagueId == league.Id);
        return new LeagueDto(league.Id, league.Slug, league.Name, league.Title, league.IsActive, hasLogo);
    }

    public async Task<LeagueAdminDto> CreateLeagueAdminAsync(int leagueId, CreateLeagueAdminRequest req)
    {
        if (!await db.Leagues.AnyAsync(l => l.Id == leagueId))
            throw ApiException.NotFound($"Lega {leagueId} inesistente.");
        if (string.IsNullOrWhiteSpace(req.Username))
            throw ApiException.BadRequest("Username obbligatorio.");
        if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 6)
            throw ApiException.BadRequest("Password obbligatoria (min 6 caratteri).");

        var username = req.Username.Trim();
        if (await db.Users.AnyAsync(u => u.LeagueId == leagueId && u.Username == username))
            throw ApiException.Conflict($"Admin '{username}' già presente per questa lega.");

        var user = new User
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Role = UserRoles.LeagueAdmin,
            LeagueId = leagueId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return new LeagueAdminDto(user.Id, user.Username);
    }

    public async Task<LeagueAdminDto> UpdateLeagueAdminAsync(int leagueId, int userId, UpdateLeagueAdminRequest req)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.LeagueId == leagueId)
            ?? throw ApiException.NotFound($"Admin {userId} inesistente per questa lega.");

        if (req.Username is not null)
        {
            var username = req.Username.Trim();
            if (string.IsNullOrWhiteSpace(username))
                throw ApiException.BadRequest("Username non può essere vuoto.");
            if (await db.Users.AnyAsync(u => u.LeagueId == leagueId && u.Id != userId && u.Username == username))
                throw ApiException.Conflict($"Admin '{username}' già presente per questa lega.");
            user.Username = username;
        }
        if (req.Password is not null)
        {
            if (req.Password.Length < 6)
                throw ApiException.BadRequest("Password troppo corta (min 6 caratteri).");
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);
        }

        await db.SaveChangesAsync();
        return new LeagueAdminDto(user.Id, user.Username);
    }

    public async Task DeleteLeagueAdminAsync(int leagueId, int userId)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.LeagueId == leagueId)
            ?? throw ApiException.NotFound($"Admin {userId} inesistente per questa lega.");
        db.Users.Remove(user);
        await db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<LeagueAdminDto>> GetLeagueAdminsAsync(int leagueId) =>
        await db.Users.AsNoTracking()
            .Where(u => u.LeagueId == leagueId)
            .OrderBy(u => u.Username)
            .Select(u => new LeagueAdminDto(u.Id, u.Username))
            .ToListAsync();
}
