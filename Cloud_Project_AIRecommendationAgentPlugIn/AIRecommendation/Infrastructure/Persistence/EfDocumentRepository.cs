using AIRecommendation.Application.Interfaces;
using AIRecommendation.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AIRecommendation.Infrastructure.Persistence;

/// <summary>
/// An Entity Framework Core implementation of the <see cref="IDocumentRepository"/> interface.
/// </summary>
public class EfDocumentRepository : IDocumentRepository
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="EfDocumentRepository"/> class.
    /// </summary>
    /// <param name="db">The database context to use.</param>
    public EfDocumentRepository(AppDbContext db) => _db = db;

    /// <inheritdoc />
    public Task AddAsync(DocumentRecord doc)
    {
        _db.Documents.Add(doc);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<int> SaveChangesAsync() => _db.SaveChangesAsync();

    /// <inheritdoc />
    /// CHANGED: Select only the columns needed for cosine similarity search.
    /// Previously this loaded ALL columns including Content (full document text)
    /// which is never used during search - only EmbeddingJson, Title, Category,
    /// FileName and Id are needed. This significantly reduces the data transferred
    /// from the university SQL server and prevents the execution timeout.
    public Task<List<DocumentRecord>> GetAllAsync(string? category = null)
    {
        var query = _db.Documents.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(d => d.Category == category);

        // Only select columns needed for search + display.
        // Content and FilePath are large fields not needed during similarity ranking.
        return query
            .Select(d => new DocumentRecord
            {
                Id = d.Id,
                Title = d.Title,
                FileName = d.FileName,
                FileType = d.FileType,
                Category = d.Category,
                EmbeddingJson = d.EmbeddingJson,  // needed for cosine similarity
                ImportedAt = d.ImportedAt,
                Url = d.Url
                // Content and FilePath intentionally excluded to reduce data transfer
            })
            .ToListAsync();
    }

    /// <inheritdoc />
    public Task<List<DocumentRecord>> GetByCategoryAsync(string category)
        => _db.Documents.AsNoTracking().Where(d => d.Category == category).ToListAsync();

    /// <inheritdoc />
    public int Count() => _db.Documents.Count();

    /// <inheritdoc />
    public async Task DeleteByFileNameAsync(string fileName)
    {
        await _db.Documents.Where(d => d.FileName == fileName).ExecuteDeleteAsync();
    }
}