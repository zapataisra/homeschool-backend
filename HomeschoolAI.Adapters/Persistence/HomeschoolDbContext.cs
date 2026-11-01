using Microsoft.EntityFrameworkCore;
using HomeschoolAI.Domain.Entities;

namespace HomeschoolAI.Adapters.Persistence;

/// <summary>
/// Contexto de EF Core (solo lectura) mapeado a la tabla PostgreSQL
/// existente <c>embeddings_sep</c>, creada y poblada desde el pipeline Python.
/// No se ejecutan migraciones de EF Core.
/// </summary>
public sealed class HomeschoolDbContext : DbContext
{
    public HomeschoolDbContext(DbContextOptions<HomeschoolDbContext> options)
        : base(options)
    {
    }

    public DbSet<EmbeddingChunk> EmbeddingsSep => Set<EmbeddingChunk>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Extensión pgvector (índices HNSW y operadores vectoriales).
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<EmbeddingChunk>(entity =>
        {
            entity.ToTable("embeddings_sep");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.Source)
                .HasColumnName("source")
                .IsRequired();

            entity.Property(e => e.ChunkId)
                .HasColumnName("chunk_id")
                .IsRequired();

            entity.Property(e => e.Texto)
                .HasColumnName("texto")
                .IsRequired();

            entity.Property(e => e.Embedding)
                .HasColumnName("embedding")
                .HasColumnType("vector(384)");

            entity.Property(e => e.EmbeddingModel)
                .HasColumnName("embedding_model");

            entity.Property(e => e.Page)
                .HasColumnName("page");

            // Restricción UNIQUE (source, chunk_id) de la tabla existente.
            entity.HasIndex(e => new { e.Source, e.ChunkId })
                .IsUnique()
                .HasDatabaseName("embeddings_sep_source_chunk_uq");
        });
    }
}
