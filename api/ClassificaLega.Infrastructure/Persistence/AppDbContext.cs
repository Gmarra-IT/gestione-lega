using ClassificaLega.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClassificaLega.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Stage> Stages => Set<Stage>();
    public DbSet<Result> Results => Set<Result>();

    protected override void OnModelCreating(ModelBuilder model)
    {
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
