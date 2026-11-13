using System.ComponentModel.DataAnnotations;

namespace AIRecommendation.Domain.Entities;

/// <summary>
/// Represents a single document or text chunk stored in the recommendation system.
/// </summary>
public class DocumentRecord
{
    /// <summary>
    /// Gets or sets the unique identifier for the document record.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the name of the file from which the content was extracted.
    /// Limited to 450 characters so the column can be used in a SQL Server index.
    /// </summary>
    [MaxLength(450)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of the file (e.g., "txt", "csv", "pdf").
    /// </summary>
    public string FileType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the full physical path to the source file.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the title of the document or chunk.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the extracted text content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the vector embedding as a JSON-serialized float array.
    /// </summary>
    public string EmbeddingJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date and time when the record was imported.
    /// </summary>
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the category or dataset name (e.g., "support", "restaurant", "books").
    /// Limited to 450 characters so the column can be used in a SQL Server index.
    /// </summary>
    [MaxLength(450)]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional URL link to the document source.
    /// </summary>
    public string? Url { get; set; }
}