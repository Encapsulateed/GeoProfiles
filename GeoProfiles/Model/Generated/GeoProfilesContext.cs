using System;
using System.Collections.Generic;
using GeoProfiles.Model;
using Microsoft.EntityFrameworkCore;

namespace GeoProfiles;

public partial class GeoProfilesContext : DbContext
{
    public GeoProfilesContext(DbContextOptions<GeoProfilesContext> options)
        : base(options)
    {
    }

    public virtual DbSet<ElevationCache> ElevationCache { get; set; }

    public virtual DbSet<FlywaySchemaHistory> FlywaySchemaHistory { get; set; }

    public virtual DbSet<Isolines> Isolines { get; set; }

    public virtual DbSet<Projects> Projects { get; set; }

    public virtual DbSet<RefreshTokens> RefreshTokens { get; set; }

    public virtual DbSet<SystemLogs> SystemLogs { get; set; }

    public virtual DbSet<TerrainProfilePoints> TerrainProfilePoints { get; set; }

    public virtual DbSet<TerrainProfiles> TerrainProfiles { get; set; }

    public virtual DbSet<Users> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasPostgresExtension("postgis")
            .HasPostgresExtension("uuid-ossp")
            .HasPostgresExtension("topology", "postgis_topology");

        modelBuilder.Entity<ElevationCache>(entity =>
        {
            entity.HasKey(e => e.Pt).HasName("elevation_cache_pkey");

            entity.ToTable("elevation_cache");

            entity.HasIndex(e => e.Pt, "gist_elev_cache_pt").HasMethod("gist");

            entity.Property(e => e.Pt)
                .HasColumnType("geometry(Point,4326)")
                .HasColumnName("pt");
            entity.Property(e => e.ElevM).HasColumnName("elev_m");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<FlywaySchemaHistory>(entity =>
        {
            entity.HasKey(e => e.InstalledRank).HasName("flyway_schema_history_pk");

            entity.ToTable("flyway_schema_history");

            entity.HasIndex(e => e.Success, "flyway_schema_history_s_idx");

            entity.Property(e => e.InstalledRank)
                .ValueGeneratedNever()
                .HasColumnName("installed_rank");
            entity.Property(e => e.Checksum).HasColumnName("checksum");
            entity.Property(e => e.Description)
                .HasMaxLength(200)
                .HasColumnName("description");
            entity.Property(e => e.ExecutionTime).HasColumnName("execution_time");
            entity.Property(e => e.InstalledBy)
                .HasMaxLength(100)
                .HasColumnName("installed_by");
            entity.Property(e => e.InstalledOn)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("installed_on");
            entity.Property(e => e.Script)
                .HasMaxLength(1000)
                .HasColumnName("script");
            entity.Property(e => e.Success).HasColumnName("success");
            entity.Property(e => e.Type)
                .HasMaxLength(20)
                .HasColumnName("type");
            entity.Property(e => e.Version)
                .HasMaxLength(50)
                .HasColumnName("version");
        });

        modelBuilder.Entity<Isolines>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("isolines_pkey");

            entity.ToTable("isolines");

            entity.HasIndex(e => e.Geom, "gist_isolines_geom").HasMethod("gist");

            entity.HasIndex(e => e.ProjectId, "ix_isolines_project_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Direction).HasColumnName("direction");
            entity.Property(e => e.Geom)
                .HasColumnType("geometry(Polygon,4326)")
                .HasColumnName("geom");
            entity.Property(e => e.Level).HasColumnName("level");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Project).WithMany(p => p.Isolines)
                .HasForeignKey(d => d.ProjectId)
                .HasConstraintName("fk_isolines_projects");
        });

        modelBuilder.Entity<Projects>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("projects_pkey");

            entity.ToTable("projects");

            entity.HasIndex(e => e.Bbox, "gist_projects_bbox").HasMethod("gist");

            entity.HasIndex(e => e.UserId, "ix_projects_user_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.Bbox)
                .HasColumnType("geometry(Polygon,4326)")
                .HasColumnName("bbox");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.Projects)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_projects_users");
        });

        modelBuilder.Entity<RefreshTokens>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("refresh_tokens_pkey");

            entity.ToTable("refresh_tokens");

            entity.HasIndex(e => e.UserId, "ix_refresh_tokens_user_id");

            entity.HasIndex(e => e.Token, "refresh_tokens_token_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.IsRevoked)
                .HasDefaultValue(false)
                .HasColumnName("is_revoked");
            entity.Property(e => e.Token)
                .HasMaxLength(2000)
                .HasColumnName("token");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.RefreshTokens)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_refresh_tokens_users");
        });

        modelBuilder.Entity<SystemLogs>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("system_logs");

            entity.Property(e => e.Exception).HasColumnName("exception");
            entity.Property(e => e.Level).HasColumnName("level");
            entity.Property(e => e.Message).HasColumnName("message");
            entity.Property(e => e.RaiseDate).HasColumnName("raise_date");
            entity.Property(e => e.RequestId).HasColumnName("request_id");
        });

        modelBuilder.Entity<TerrainProfilePoints>(entity =>
        {
            entity.HasKey(e => new { e.ProfileId, e.Seq }).HasName("terrain_profile_points_pkey");

            entity.ToTable("terrain_profile_points");

            entity.HasIndex(e => e.ProfileId, "ix_tpp_profile_id");

            entity.Property(e => e.ProfileId).HasColumnName("profile_id");
            entity.Property(e => e.Seq).HasColumnName("seq");
            entity.Property(e => e.DistM).HasColumnName("dist_m");
            entity.Property(e => e.ElevM).HasColumnName("elev_m");

            entity.HasOne(d => d.Profile).WithMany(p => p.TerrainProfilePoints)
                .HasForeignKey(d => d.ProfileId)
                .HasConstraintName("terrain_profile_points_profile_id_fkey");
        });

        modelBuilder.Entity<TerrainProfiles>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("terrain_profiles_pkey");

            entity.ToTable("terrain_profiles");

            entity.HasIndex(e => new { e.StartPt, e.EndPt }, "gist_terrain_profiles_geom").HasMethod("gist");

            entity.HasIndex(e => e.ProjectId, "ix_terrain_profiles_project_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.EndPt)
                .HasColumnType("geometry(Point,4326)")
                .HasColumnName("end_pt");
            entity.Property(e => e.LengthM).HasColumnName("length_m");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.StartPt)
                .HasColumnType("geometry(Point,4326)")
                .HasColumnName("start_pt");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Project).WithMany(p => p.TerrainProfiles)
                .HasForeignKey(d => d.ProjectId)
                .HasConstraintName("terrain_profiles_project_id_fkey");
        });

        modelBuilder.Entity<Users>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");

            entity.ToTable("users");

            entity.HasIndex(e => e.Email, "users_email_key").IsUnique();

            entity.HasIndex(e => e.Username, "users_username_key").IsUnique();

            entity.HasIndex(e => e.Email, "ux_users_email");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnName("email");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .HasColumnName("password_hash");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.Username)
                .HasMaxLength(50)
                .HasColumnName("username");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
