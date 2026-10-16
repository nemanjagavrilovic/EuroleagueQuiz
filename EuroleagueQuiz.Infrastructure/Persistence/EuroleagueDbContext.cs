using EuroleagueQuiz.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EuroleagueQuiz.Infrastructure.Persistence;

public sealed class EuroleagueDbContext : DbContext
{
    public EuroleagueDbContext(DbContextOptions<EuroleagueDbContext> options) : base(options) { }

    public DbSet<Player> Players => Set<Player>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<PlayerTeamSeason> PlayerTeamSeasons => Set<PlayerTeamSeason>();
    public DbSet<DataSyncRun> DataSyncRuns => Set<DataSyncRun>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Player>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ExternalId).IsUnique();
            entity.Property(x => x.ExternalId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Nationality).HasMaxLength(100);
            entity.Property(x => x.ProviderExternalId).HasMaxLength(100);
            entity.Property(x => x.NationalityCode).HasMaxLength(10);
            entity.Property(x => x.ImageUrl).HasMaxLength(500);
            entity.HasIndex(x => x.ProviderExternalId);
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ExternalId).IsUnique();
            entity.Property(x => x.ExternalId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ShortName).HasMaxLength(100);
            entity.Property(x => x.TvCode).HasMaxLength(20);
            entity.Property(x => x.PermanentName).HasMaxLength(200);
            entity.Property(x => x.PermanentAlias).HasMaxLength(100);
            entity.Property(x => x.Country).HasMaxLength(100);
            entity.Property(x => x.CountryCode).HasMaxLength(10);
            entity.Property(x => x.ImageUrl).HasMaxLength(500);
        });

        modelBuilder.Entity<Season>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ExternalId).IsUnique();
            entity.Property(x => x.ExternalId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(50).IsRequired();
            entity.Property(x => x.CompetitionCode).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Alias).HasMaxLength(20);
        });

        modelBuilder.Entity<PlayerTeamSeason>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.PlayerId, x.TeamId, x.SeasonId }).IsUnique();
            // Game queries start from a club and then find its players. The unique
            // player-first index above cannot efficiently seek by TeamId.
            entity.HasIndex(x => new { x.TeamId, x.PlayerId, x.SeasonId });
            entity.Property(x => x.Source).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ProviderExternalId).HasMaxLength(100);
            entity.Property(x => x.JerseyNumber).HasMaxLength(20);
            entity.Property(x => x.PositionName).HasMaxLength(50);
            entity.HasIndex(x => x.ProviderExternalId);
            entity.HasOne(x => x.Player).WithMany(x => x.TeamSeasons).HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Team).WithMany(x => x.PlayerSeasons).HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Season).WithMany(x => x.PlayerSeasons).HasForeignKey(x => x.SeasonId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Email).IsUnique();
            entity.HasIndex(x => x.GoogleSubject).IsUnique();
            entity.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(320).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(512);
            entity.Property(x => x.GoogleSubject).HasMaxLength(255);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
        });
        modelBuilder.Entity<DataSyncRun>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Provider).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.ErrorMessage).HasMaxLength(4000);
        });
    }
}
