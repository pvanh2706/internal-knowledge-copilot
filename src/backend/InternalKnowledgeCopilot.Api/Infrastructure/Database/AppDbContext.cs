using Microsoft.EntityFrameworkCore;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<UserEntity> Users => Set<UserEntity>();

    public DbSet<TeamEntity> Teams => Set<TeamEntity>();

    public DbSet<FolderEntity> Folders => Set<FolderEntity>();

    public DbSet<FolderPermissionEntity> FolderPermissions => Set<FolderPermissionEntity>();

    public DbSet<UserFolderPermissionEntity> UserFolderPermissions => Set<UserFolderPermissionEntity>();

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

        modelBuilder.Entity<FolderEntity>(entity =>
        {
            entity.ToTable("folders");
            entity.HasKey(folder => folder.Id);
            entity.Property(folder => folder.ParentId).HasColumnName("parent_id");
            entity.Property(folder => folder.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(folder => folder.Path).HasColumnName("path").HasMaxLength(1000).IsRequired();
            entity.Property(folder => folder.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.Property(folder => folder.CreatedAt).HasColumnName("created_at");
            entity.Property(folder => folder.UpdatedAt).HasColumnName("updated_at");
            entity.Property(folder => folder.DeletedAt).HasColumnName("deleted_at");
            entity.HasIndex(folder => folder.Path).IsUnique();
            entity.HasOne(folder => folder.Parent)
                .WithMany(folder => folder.Children)
                .HasForeignKey(folder => folder.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(folder => folder.CreatedByUser)
                .WithMany()
                .HasForeignKey(folder => folder.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FolderPermissionEntity>(entity =>
        {
            entity.ToTable("folder_permissions");
            entity.HasKey(permission => permission.Id);
            entity.Property(permission => permission.FolderId).HasColumnName("folder_id");
            entity.Property(permission => permission.TeamId).HasColumnName("team_id");
            entity.Property(permission => permission.CanView).HasColumnName("can_view");
            entity.Property(permission => permission.CreatedAt).HasColumnName("created_at");
            entity.Property(permission => permission.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(permission => new { permission.FolderId, permission.TeamId }).IsUnique();
            entity.HasOne(permission => permission.Folder)
                .WithMany(folder => folder.TeamPermissions)
                .HasForeignKey(permission => permission.FolderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(permission => permission.Team)
                .WithMany()
                .HasForeignKey(permission => permission.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserFolderPermissionEntity>(entity =>
        {
            entity.ToTable("user_folder_permissions");
            entity.HasKey(permission => permission.Id);
            entity.Property(permission => permission.UserId).HasColumnName("user_id");
            entity.Property(permission => permission.FolderId).HasColumnName("folder_id");
            entity.Property(permission => permission.CanView).HasColumnName("can_view");
            entity.Property(permission => permission.CreatedAt).HasColumnName("created_at");
            entity.Property(permission => permission.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(permission => new { permission.UserId, permission.FolderId }).IsUnique();
            entity.HasOne(permission => permission.User)
                .WithMany()
                .HasForeignKey(permission => permission.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(permission => permission.Folder)
                .WithMany(folder => folder.UserPermissions)
                .HasForeignKey(permission => permission.FolderId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
