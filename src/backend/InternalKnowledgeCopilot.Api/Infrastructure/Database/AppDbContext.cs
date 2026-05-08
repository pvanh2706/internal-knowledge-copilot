using Microsoft.EntityFrameworkCore;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<UserEntity> Users => Set<UserEntity>();

    public DbSet<TeamEntity> Teams => Set<TeamEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TeamEntity>(entity =>
        {
            entity.ToTable("teams");
            entity.HasKey(team => team.Id);
            entity.Property(team => team.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(team => team.Description).HasColumnName("description").HasMaxLength(1000);
            entity.Property(team => team.CreatedAt).HasColumnName("created_at");
            entity.Property(team => team.UpdatedAt).HasColumnName("updated_at");
            entity.Property(team => team.DeletedAt).HasColumnName("deleted_at");
            entity.HasIndex(team => team.Name).IsUnique();
        });

        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
            entity.Property(user => user.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
            entity.Property(user => user.PasswordHash).HasColumnName("password_hash").IsRequired();
            entity.Property(user => user.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(50);
            entity.Property(user => user.PrimaryTeamId).HasColumnName("primary_team_id");
            entity.Property(user => user.MustChangePassword).HasColumnName("must_change_password");
            entity.Property(user => user.IsActive).HasColumnName("is_active");
            entity.Property(user => user.CreatedAt).HasColumnName("created_at");
            entity.Property(user => user.UpdatedAt).HasColumnName("updated_at");
            entity.Property(user => user.DeletedAt).HasColumnName("deleted_at");
            entity.HasIndex(user => user.Email).IsUnique();
            entity.HasOne(user => user.PrimaryTeam)
                .WithMany(team => team.Users)
                .HasForeignKey(user => user.PrimaryTeamId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
