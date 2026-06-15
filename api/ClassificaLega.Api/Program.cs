using System.Text;
using ClassificaLega.Api.Auth;
using ClassificaLega.Api.Dtos;
using ClassificaLega.Api.Services;
using ClassificaLega.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection("Admin"));

builder.Services.AddScoped<LeagueReadService>();
builder.Services.AddScoped<LeagueWriteService>();
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
    await db.Database.MigrateAsync();
    await DatabaseSeeder.SeedAsync(db);
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

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

var api = app.MapGroup("/api");

// --- auth ---
api.MapPost("/auth/login", (LoginRequest req, AuthService auth) =>
    auth.Login(req) is { } r ? Results.Ok(r) : Results.Unauthorized());

// --- read (public) ---
api.MapGet("/season", async (LeagueReadService svc) =>
    await svc.GetSeasonAsync() is { } s ? Results.Ok(s) : Results.NotFound());

api.MapGet("/standings", async (LeagueReadService svc) =>
    await svc.GetStandingsAsync() is { } r ? Results.Ok(r) : Results.NotFound());

api.MapGet("/stages", async (LeagueReadService svc) =>
    await svc.GetStagesAsync() is { } r ? Results.Ok(r) : Results.NotFound());

api.MapGet("/stages/{number:int}/results", async (int number, LeagueReadService svc) =>
    await svc.GetStageResultsAsync(number) is { } r ? Results.Ok(r) : Results.NotFound());

api.MapGet("/players/{id:int}/progression", async (int id, LeagueReadService svc) =>
    await svc.GetProgressionAsync(id) is { } r ? Results.Ok(r) : Results.NotFound());

api.MapGet("/matrix", async (LeagueReadService svc) =>
    await svc.GetMatrixAsync() is { } r ? Results.Ok(r) : Results.NotFound());

// --- write (admin) ---
var admin = api.MapGroup("").RequireAuthorization();

admin.MapPut("/season", async (UpdateSeasonRequest req, LeagueWriteService svc) =>
    Results.Ok(await svc.UpdateSeasonAsync(req)));

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

app.Run();

public partial class Program;
