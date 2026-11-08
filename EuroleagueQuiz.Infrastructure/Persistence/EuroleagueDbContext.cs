using EuroleagueQuiz.Domain.Entities;
using EuroleagueQuiz.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EuroleagueQuiz.Infrastructure.Persistence;

public sealed class EuroleagueDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public EuroleagueDbContext(DbContextOptions<EuroleagueDbContext> options) : base(options) { }

    public DbSet<Player> Players => Set<Player>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<PlayerTeamSeason> PlayerTeamSeasons => Set<PlayerTeamSeason>();
    public DbSet<DataSyncRun> DataSyncRuns => Set<DataSyncRun>();
    public DbSet<DailyGridChallenge> DailyGridChallenges => Set<DailyGridChallenge>();
    public DbSet<DailyGridAttempt> DailyGridAttempts => Set<DailyGridAttempt>();
    public DbSet<DailyGameChallenge> DailyGameChallenges => Set<DailyGameChallenge>();
    public DbSet<DailyGameAttempt> DailyGameAttempts => Set<DailyGameAttempt>();
    public DbSet<UserRefreshToken> UserRefreshTokens => Set<UserRefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(x => x.DisplayName).HasMaxLength(101).IsRequired();
            entity.Property(x => x.FirstName).HasMaxLength(50);
            entity.Property(x => x.LastName).HasMaxLength(50);
            entity.Property(x => x.SubscriptionPlan).HasMaxLength(30).IsRequired();
            entity.HasIndex(x => x.DisplayName);
        });
        modelBuilder.Entity<UserRefreshToken>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ReplacedByTokenHash).HasMaxLength(64);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.ExpiresAtUtc });
            entity.HasOne(x => x.User).WithMany(x => x.RefreshTokens).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<DailyGameChallenge>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Game).HasMaxLength(40).IsRequired();
            entity.Property(x => x.DefinitionJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(x => new { x.Game, x.Date }).IsUnique();
        });
        modelBuilder.Entity<DailyGameAttempt>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Game).HasMaxLength(40).IsRequired();
            entity.Property(x => x.StateJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(x => new { x.GuestId, x.Game, x.Date }).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.Game, x.Date }).IsUnique().HasFilter("\"UserId\" IS NOT NULL");
            entity.HasIndex(x => new { x.Game, x.Date, x.UserId }).HasFilter("\"LeaderboardEligible\" AND \"UserId\" IS NOT NULL");
            entity.HasOne(x => x.Challenge).WithMany().HasForeignKey(x => x.ChallengeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        });
        modelBuilder.Entity<DailyGridChallenge>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.Date, x.Difficulty }).IsUnique();
            entity.Property(x => x.Difficulty).HasMaxLength(20).IsRequired();
            entity.Property(x => x.DefinitionJson).HasColumnType("jsonb").IsRequired();
        });
        modelBuilder.Entity<DailyGridAttempt>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.GuestId, x.Date }).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.Date }).IsUnique().HasFilter("\"UserId\" IS NOT NULL");
            entity.HasIndex(x => new { x.Date, x.UserId }).HasFilter("\"LeaderboardEligible\" AND \"UserId\" IS NOT NULL");
            entity.Property(x => x.StateJson).HasColumnType("jsonb").IsRequired();
            entity.HasOne(x => x.Challenge).WithMany().HasForeignKey(x => x.ChallengeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        });
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

        modelBuilder.Entity<DataSyncRun>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Provider).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.ErrorMessage).HasMaxLength(4000);
        });
    }
}
