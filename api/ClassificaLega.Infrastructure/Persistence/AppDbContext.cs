using ClassificaLega.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClassificaLega.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<League> Leagues => Set<League>();
    public DbSet<LeagueLogo> LeagueLogos => Set<LeagueLogo>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Stage> Stages => Set<Stage>();
    public DbSet<Result> Results => Set<Result>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<League>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Slug).IsRequired().HasMaxLength(60);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Title).HasMaxLength(200);
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasMany(x => x.Seasons).WithOne(x => x.League).HasForeignKey(x => x.LeagueId);
        });

        model.Entity<LeagueLogo>(e =>
        {
            // PK = LeagueId: relazione 1-1, una riga per lega. Cascade alla cancellazione lega.
            e.HasKey(x => x.LeagueId);
            e.Property(x => x.Bytes).IsRequired();
            e.Property(x => x.ContentType).IsRequired().HasMaxLength(100);
            e.Property(x => x.ETag).IsRequired().HasMaxLength(100);
            e.HasOne(x => x.League).WithOne()
                .HasForeignKey<LeagueLogo>(x => x.LeagueId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Username).IsRequired().HasMaxLength(100);
            e.Property(x => x.PasswordHash).IsRequired();
            e.Property(x => x.Role).IsRequired().HasMaxLength(40);
            e.HasIndex(x => new { x.LeagueId, x.Username }).IsUnique();
            e.HasOne(x => x.League).WithMany().HasForeignKey(x => x.LeagueId);
        });

        model.Entity<Season>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.HasMany(x => x.Stages).WithOne(x => x.Season).HasForeignKey(x => x.SeasonId);
            e.HasMany(x => x.Players).WithOne(x => x.Season).HasForeignKey(x => x.SeasonId);
        });

        model.Entity<Player>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.DisplayName).IsRequired().HasMaxLength(200);
            e.Property(x => x.NormalizedKey).IsRequired().HasMaxLength(200);
            e.HasIndex(x => new { x.SeasonId, x.NormalizedKey }).IsUnique();
        });

        model.Entity<Stage>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.SeasonId, x.Number }).IsUnique();
            e.HasMany(x => x.Results).WithOne(x => x.Stage).HasForeignKey(x => x.StageId);
        });

        model.Entity<Result>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.StageId, x.PlayerId }).IsUnique();
        });
    }
}
