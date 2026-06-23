using System.Security.Claims;
using System.Text;
using ClassificaLega.Api.Auth;
using ClassificaLega.Api.Dtos;
using ClassificaLega.Api.Services;
using ClassificaLega.Api.Tenancy;
using ClassificaLega.Domain.Entities;
using ClassificaLega.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection("Admin"));

builder.Services.AddScoped<LeagueContext>();
builder.Services.AddScoped<LeagueReadService>();
builder.Services.AddScoped<LeagueWriteService>();
builder.Services.AddScoped<LeagueImportService>();
builder.Services.AddScoped<LeagueAdminService>();
builder.Services.AddScoped<AuthService>();

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var adminCfg = app.Configuration.GetSection("Admin").Get<AdminOptions>() ?? new AdminOptions();
    await db.Database.MigrateAsync();
    await DatabaseSeeder.SeedAsync(db);
    // Garantisce sempre il super-admin (da env) e l'admin della lega seed, anche su DB già popolati.
    await DatabaseSeeder.EnsureUsersAsync(db, adminCfg.Username, adminCfg.PasswordHash);
}

// Translate ApiException into problem responses.
app.Use(async (ctx, next) =>
{
    try
    {
        await next();
    }
    catch (ApiException ex)
    {
        ctx.Response.StatusCode = ex.StatusCode;
        await ctx.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
});

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Risolve la lega corrente dall'header X-League-Slug nel LeagueContext (scoped).
app.Use(async (ctx, next) =>
{
    var slug = ctx.Request.Headers["X-League-Slug"].FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(slug))
    {
        var db = ctx.RequestServices.GetRequiredService<AppDbContext>();
        var league = await db.Leagues.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Slug == slug && l.IsActive);
        var leagueCtx = ctx.RequestServices.GetRequiredService<LeagueContext>();
        leagueCtx.Current = league;
        // Stagione richiesta (opzionale): null/non-numerica → stagione attiva.
        if (int.TryParse(ctx.Request.Headers["X-Season-Id"].FirstOrDefault(), out var seasonId))
            leagueCtx.RequestedSeasonId = seasonId;
    }
    await next();
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

var api = app.MapGroup("/api");

// --- auth ---
api.MapPost("/auth/login", async (LoginRequest req, AuthService auth) =>
    await auth.LoginAsync(req) is { } r ? Results.Ok(r) : Results.Unauthorized());

// --- leghe (elenco pubblico per picker/selettore) ---
api.MapGet("/leagues", async (LeagueAdminService svc) =>
    Results.Ok(await svc.GetActiveLeaguesAsync()));

// Logo lega (pubblico, slug nel path → niente header). Cache-friendly con ETag.
api.MapGet("/leagues/{slug}/logo", async (string slug, LeagueAdminService svc, HttpContext http) =>
{
    var logo = await svc.GetLogoAsync(slug);
    if (logo is null) return Results.NotFound();

    var etag = $"\"{logo.ETag}\"";
    if (http.Request.Headers.IfNoneMatch == etag)
        return Results.StatusCode(StatusCodes.Status304NotModified);

    http.Response.Headers.ETag = etag;
    http.Response.Headers.CacheControl = "public, max-age=86400";
    return Results.File(logo.Bytes, logo.ContentType);
});

// --- read (public) ---
api.MapGet("/season", async (LeagueReadService svc) =>
    await svc.GetSeasonAsync() is { } s ? Results.Ok(s) : Results.NotFound());

// Elenco stagioni della lega (per il selettore: attiva + precedenti).
api.MapGet("/seasons", async (LeagueReadService svc) =>
    Results.Ok(await svc.GetSeasonsAsync()));

api.MapGet("/standings", async (LeagueReadService svc) =>
    await svc.GetStandingsAsync() is { } r ? Results.Ok(r) : Results.NotFound());

api.MapGet("/stages", async (LeagueReadService svc) =>
    await svc.GetStagesAsync() is { } r ? Results.Ok(r) : Results.NotFound());

api.MapGet("/stages/{number:int}/results", async (int number, LeagueReadService svc) =>
    await svc.GetStageResultsAsync(number) is { } r ? Results.Ok(r) : Results.NotFound());

// Picker giocatori: ricerca + paginazione lato server (scala con l'arrivo di nuovi player).
api.MapGet("/players", async (string? search, int? skip, int? take, LeagueReadService svc) =>
    await svc.GetPlayersAsync(search, skip ?? 0, take ?? 20) is { } page ? Results.Ok(page) : Results.NotFound());

api.MapGet("/players/{id:int}/progression", async (int id, LeagueReadService svc) =>
    await svc.GetProgressionAsync(id) is { } r ? Results.Ok(r) : Results.NotFound());

api.MapGet("/matrix", async (LeagueReadService svc) =>
    await svc.GetMatrixAsync() is { } r ? Results.Ok(r) : Results.NotFound());

// --- write (admin di lega) ---
// Le scritture richiedono che il token corrisponda alla lega del contesto; il super-admin passa sempre.
var admin = api.MapGroup("").RequireAuthorization().AddEndpointFilter(async (ctx, next) =>
{
    var http = ctx.HttpContext;
    var role = http.User.FindFirstValue(ClaimTypes.Role);
    if (role == UserRoles.SuperAdmin)
        return await next(ctx);

    var leagueCtx = http.RequestServices.GetRequiredService<LeagueContext>();
    if (leagueCtx.Current is null)
        return Results.NotFound(new { error = "Lega non specificata o inesistente." });

    var tokenLeague = http.User.FindFirstValue("leagueId");
    if (tokenLeague != leagueCtx.Current.Id.ToString())
        return Results.Json(new { error = "Non autorizzato per questa lega." }, statusCode: 403);

    return await next(ctx);
});

admin.MapPut("/season", async (UpdateSeasonRequest req, LeagueWriteService svc) =>
    Results.Ok(await svc.UpdateSeasonAsync(req)));

// Sostituisce la regola di scoring della stagione corrente e ricalcola i risultati.
admin.MapPut("/season/scoring-rule", async (UpdateScoringRuleRequest req, LeagueWriteService svc) =>
    Results.Ok(await svc.UpdateScoringRuleAsync(req)));

// Crea una nuova stagione (diventa attiva, archivia la precedente).
admin.MapPost("/seasons", async (CreateSeasonRequest req, LeagueWriteService svc) =>
    Results.Ok(await svc.CreateSeasonAsync(req)));

admin.MapPost("/stages", async (UpsertStageRequest req, LeagueWriteService svc) =>
    Results.Ok(await svc.UpsertStageAsync(req)));

admin.MapPost("/results", async (UpsertResultRequest req, LeagueWriteService svc) =>
    Results.Ok(await svc.UpsertResultAsync(req)));

admin.MapPut("/results/{id:int}", async (int id, UpdateResultRequest req, LeagueWriteService svc) =>
    Results.Ok(await svc.UpdateResultAsync(id, req)));

admin.MapDelete("/results/{id:int}", async (int id, LeagueWriteService svc) =>
{
    await svc.DeleteResultAsync(id);
    return Results.NoContent();
});

admin.MapPost("/import/pdf", async (IFormFile file, LeagueImportService svc) =>
{
    await using var stream = file.OpenReadStream();
    return Results.Ok(await svc.PreviewAsync(stream));
}).DisableAntiforgery();

admin.MapPost("/import/commit", async (ImportCommitRequest req, LeagueImportService svc) =>
    Results.Ok(await svc.CommitAsync(req)));

// Logo della lega corrente (admin di lega o super-admin).
admin.MapPost("/logo", async (IFormFile file, LeagueAdminService svc, LeagueContext ctx) =>
{
    await using var stream = file.OpenReadStream();
    await svc.SetLogoAsync(ctx.RequireLeagueId(), stream, file.ContentType);
    return Results.NoContent();
}).DisableAntiforgery();

admin.MapDelete("/logo", async (LeagueAdminService svc, LeagueContext ctx) =>
{
    await svc.DeleteLogoAsync(ctx.RequireLeagueId());
    return Results.NoContent();
});

// --- gestione leghe (solo super-admin) ---
var superAdmin = api.MapGroup("/leagues").RequireAuthorization().AddEndpointFilter(async (ctx, next) =>
{
    if (ctx.HttpContext.User.FindFirstValue(ClaimTypes.Role) != UserRoles.SuperAdmin)
        return Results.Json(new { error = "Solo super-admin." }, statusCode: 403);
    return await next(ctx);
});

superAdmin.MapGet("/all", async (LeagueAdminService svc) =>
    Results.Ok(await svc.GetAllLeaguesAsync()));

superAdmin.MapPost("", async (CreateLeagueRequest req, LeagueAdminService svc) =>
    Results.Ok(await svc.CreateLeagueAsync(req)));

superAdmin.MapPut("/{id:int}", async (int id, UpdateLeagueRequest req, LeagueAdminService svc) =>
    Results.Ok(await svc.UpdateLeagueAsync(id, req)));

superAdmin.MapGet("/{id:int}/admins", async (int id, LeagueAdminService svc) =>
    Results.Ok(await svc.GetLeagueAdminsAsync(id)));

superAdmin.MapPost("/{id:int}/logo", async (int id, IFormFile file, LeagueAdminService svc) =>
{
    await using var stream = file.OpenReadStream();
    await svc.SetLogoAsync(id, stream, file.ContentType);
    return Results.NoContent();
}).DisableAntiforgery();

superAdmin.MapDelete("/{id:int}/logo", async (int id, LeagueAdminService svc) =>
{
    await svc.DeleteLogoAsync(id);
    return Results.NoContent();
});

superAdmin.MapPost("/{id:int}/admins", async (int id, CreateLeagueAdminRequest req, LeagueAdminService svc) =>
    Results.Ok(await svc.CreateLeagueAdminAsync(id, req)));

superAdmin.MapPut("/{id:int}/admins/{userId:int}", async (int id, int userId, UpdateLeagueAdminRequest req, LeagueAdminService svc) =>
    Results.Ok(await svc.UpdateLeagueAdminAsync(id, userId, req)));

superAdmin.MapDelete("/{id:int}/admins/{userId:int}", async (int id, int userId, LeagueAdminService svc) =>
{
    await svc.DeleteLeagueAdminAsync(id, userId);
    return Results.NoContent();
});

app.Run();

public partial class Program;
