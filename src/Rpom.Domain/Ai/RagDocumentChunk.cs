using Pgvector;
using Rpom.Domain.Common;

namespace Rpom.Domain.Ai;

/// <summary>
/// Indexed content chunk for retrieval-augmented generation (RAG).
/// Supports AI Operations Assistant semantic search over project documentation.
/// Embedding stored as pgvector (typically 1536-dim from text-embedding-3-small).
/// AI must cite SourceRef when answering domain/concept questions (NFR-AI-02).
/// </summary>
public class RagDocumentChunk : Entity
{
    public long Id { get; set; }

    /// <summary>GLOSSARY | BUSINESS_RULE | PROCESS_PLAYBOOK | AI_SPEC (see <see cref="RagSourceType"/>).</summary>
    public string SourceType { get; set; } = null!;

    /// <summary>Human-readable source pointer: "Glossary §3.10", "BR-INV5", "Process Playbook E.4 step 3".</summary>
    public string SourceRef { get; set; } = null!;

    /// <summary>The actual content chunk fed into LLM context.</summary>
    public string ChunkText { get; set; } = null!;

    /// <summary>Position within source doc — for ordering related chunks.</summary>
    public int ChunkOrder { get; set; }

    /// <summary>
    /// Vector embedding (typically 1536-dim from text-embedding-3-small).
    /// Native Postgres pgvector type — supports ivfflat / hnsw index for ANN search.
    /// </summary>
    public Vector? Embedding { get; set; }

    /// <summary>Model used (e.g. "text-embedding-3-small") — for re-index detection when model changes.</summary>
    public string? EmbeddingModel { get; set; }

    /// <summary>Token count of ChunkText — for context budget planning.</summary>
    public int? TokenCount { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>When this chunk was last embedded.</summary>
    public DateTime IndexedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
