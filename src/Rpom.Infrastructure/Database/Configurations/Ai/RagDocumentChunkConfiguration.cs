using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pgvector.EntityFrameworkCore;
using Rpom.Domain.Ai;

namespace Rpom.Infrastructure.Database.Configurations.Ai;

internal sealed class RagDocumentChunkConfiguration : IEntityTypeConfiguration<RagDocumentChunk>
{
    public void Configure(EntityTypeBuilder<RagDocumentChunk> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SourceType).IsRequired().HasMaxLength(30);
        builder.Property(x => x.SourceRef).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ChunkText).IsRequired().HasColumnType("text");
        builder.Property(x => x.ChunkOrder).HasDefaultValue(0);
        // pgvector — 1536 dim default for text-embedding-3-small. Sized → indexable.
        builder.Property(x => x.Embedding).HasColumnType("vector(1536)");
        builder.Property(x => x.EmbeddingModel).HasMaxLength(50);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.IndexedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_rag_document_chunk_source_type",
            "source_type IN ('GLOSSARY', 'BUSINESS_RULE', 'PROCESS_PLAYBOOK', 'AI_SPEC')"));

        builder.HasIndex(x => x.SourceType);
        builder.HasIndex(x => new { x.SourceType, x.SourceRef }).HasDatabaseName("ix_rag_document_chunk_source");
        builder.HasIndex(x => x.IsActive);

        // ANN index on embedding (cosine) for fast top-K retrieval
        builder.HasIndex(x => x.Embedding)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops")
            .HasDatabaseName("ix_rag_document_chunk_embedding");
    }
}
