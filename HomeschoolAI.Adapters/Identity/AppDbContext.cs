using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using HomeschoolAI.Domain.Progress;

namespace HomeschoolAI.Adapters.Identity;

/// <summary>
/// Contexto de escritura para identidad, familias y perfiles de niño.
/// Usa el esquema <see cref="Schema"/> para NO interferir con la tabla de solo
/// lectura <c>embeddings_sep</c> (esquema <c>public</c>, gestionada por Python).
/// </summary>
public sealed class AppDbContext : IdentityDbContext<FamilyMember>
{
    /// <summary>Esquema PostgreSQL dedicado a la aplicación (identity, family, learning, billing).</summary>
    public const string Schema = "app_identity";

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Family> Families => Set<Family>();

    public DbSet<ChildProfile> ChildProfiles => Set<ChildProfile>();

    public DbSet<ChildProgress> ChildProgress => Set<ChildProgress>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Todas las tablas de este contexto (incluidas las de Identity) viven en `app_identity`.
        builder.HasDefaultSchema(Schema);

        // Las tablas de Identity conservan sus nombres estándar (AspNetUsers, AspNetRoles,
        // AspNetUserClaims, AspNetUserLogins, AspNetUserRoles, AspNetUserTokens); las nuestras
        // usan snake_case, siguiendo la convención de PostgreSQL del proyecto.
        builder.Entity<Family>(entity =>
        {
            entity.ToTable("families");

            entity.HasKey(f => f.Id);

            entity.Property(f => f.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(f => f.Name).HasColumnName("name").IsRequired().HasMaxLength(200);
            entity.Property(f => f.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(f => f.IsPremium).HasColumnName("is_premium").IsRequired().HasDefaultValue(false);
        });

        builder.Entity<FamilyMember>(entity =>
        {
            // FamilyMember se mapea a la tabla de usuarios de Identity (AspNetUsers);
            // aquí solo se añaden las columnas y relaciones propias del dominio.
            entity.Property(m => m.FamilyId).HasColumnName("family_id").IsRequired();
            entity.Property(m => m.Role)
                .HasColumnName("role")
                .HasConversion<string>()
                .HasMaxLength(16)
                .IsRequired();

            entity.HasOne(m => m.Family)
                .WithMany(f => f.Members)
                .HasForeignKey(m => m.FamilyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ChildProfile>(entity =>
        {
            entity.ToTable("child_profiles");

            entity.HasKey(c => c.Id);

            entity.Property(c => c.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(c => c.FamilyId).HasColumnName("family_id").IsRequired();
            entity.Property(c => c.Name).HasColumnName("name").IsRequired().HasMaxLength(100);
            entity.Property(c => c.Grade).HasColumnName("grade").IsRequired();
            entity.Property(c => c.Fase).HasColumnName("fase");
            entity.Property(c => c.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

            entity.HasOne(c => c.Family)
                .WithMany(f => f.Children)
                .HasForeignKey(c => c.FamilyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ChildProgress>(entity =>
        {
            entity.ToTable("child_progress");

            entity.HasKey(c => c.ChildProfileId);

            entity.Property(c => c.ChildProfileId).HasColumnName("child_profile_id").ValueGeneratedNever();
            entity.Property(c => c.TotalXP).HasColumnName("total_xp").IsRequired();
            entity.Property(c => c.CurrentLevel).HasColumnName("current_level").IsRequired();

            entity.HasOne<ChildProfile>()
                .WithOne()
                .HasForeignKey<ChildProgress>(c => c.ChildProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
