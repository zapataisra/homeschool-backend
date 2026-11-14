using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using HomeschoolAI.Domain.Billing;
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

    public DbSet<ChildTopicProgress> ChildTopicProgresses => Set<ChildTopicProgress>();

    public DbSet<PaymentEvent> PaymentEvents => Set<PaymentEvent>();

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
            entity.Property(f => f.FreeInteractionsLeft).HasColumnName("free_interactions_left").IsRequired().HasDefaultValue(20);
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
            entity.Property(c => c.AvatarUrl).HasColumnName("avatar_url").IsRequired().HasMaxLength(255).HasDefaultValue("avatar-aguila.svg");
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

        builder.Entity<ChildTopicProgress>(entity =>
        {
            entity.ToTable("child_topic_progress");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.ChildProfileId).HasColumnName("child_profile_id").IsRequired();
            entity.Property(e => e.TopicId).HasColumnName("topic_id").IsRequired().HasMaxLength(128);
            entity.Property(e => e.TopicTitle).HasColumnName("topic_title").IsRequired().HasMaxLength(255);
            entity.Property(e => e.CampoFormativo).HasColumnName("campo_formativo").IsRequired().HasMaxLength(128);
            entity.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.Property(e => e.Attempts).HasColumnName("attempts").IsRequired().HasDefaultValue(0);
            entity.Property(e => e.TimeSpentSeconds).HasColumnName("time_spent_seconds").IsRequired().HasDefaultValue(0);
            entity.Property(e => e.BadgeEarned).HasColumnName("badge_earned").HasMaxLength(128);
            entity.Property(e => e.StartedAtUtc).HasColumnName("started_at_utc").IsRequired();
            entity.Property(e => e.LastActivityAtUtc).HasColumnName("last_activity_at_utc").IsRequired();
            entity.Property(e => e.CompletedAtUtc).HasColumnName("completed_at_utc");

            // Un único registro de progreso por (niño, tema).
            entity.HasIndex(e => new { e.ChildProfileId, e.TopicId }).IsUnique();

            entity.HasOne<ChildProfile>()
                .WithMany()
                .HasForeignKey(e => e.ChildProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PaymentEvent>(entity =>
        {
            entity.ToTable("payment_events");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.StripeEventId).HasColumnName("stripe_event_id").IsRequired().HasMaxLength(128);
            entity.Property(e => e.Type).HasColumnName("type").IsRequired().HasMaxLength(64);
            entity.Property(e => e.Result).HasColumnName("result").IsRequired().HasMaxLength(32);
            entity.Property(e => e.ReceivedAtUtc).HasColumnName("received_at_utc").IsRequired();

            // Idempotencia: un mismo evento de Stripe solo se registra una vez.
            entity.HasIndex(e => e.StripeEventId).IsUnique();
        });
    }
}
