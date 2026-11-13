using AIRecommendation.Domain.Entities;

namespace AIRecommendation.Application.Interfaces;

/// <summary>
/// Defines a contract for persisting and retrieving document records.
/// </summary>
public interface IDocumentRepository
{
    /// <summary>
    /// Asynchronously adds a new document record to the repository.
    /// </summary>
    /// <param name="doc">The document record to add.</param>
    Task AddAsync(DocumentRecord doc);

    /// <summary>
    /// Asynchronously saves the changes made to the repository.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, returning the number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync();

    /// <summary>
    /// Asynchronously retrieves all document records, optionally filtered by category.
    /// </summary>
    /// <param name="category">Optional category filter. Pass null or empty to retrieve all records.</param>
    /// <returns>A task containing the list of matching document records.</returns>
    Task<List<DocumentRecord>> GetAllAsync(string? category = null);

    /// <summary>
    /// Asynchronously retrieves all document records belonging to a specific category.
    /// </summary>
    /// <param name="category">The category name.</param>
    /// <returns>A task representing the asynchronous operation, containing a list of matching document records.</returns>
    Task<List<DocumentRecord>> GetByCategoryAsync(string category);

    /// <summary>
    /// Returns the total number of document records in the repository.
    /// </summary>
    int Count();

    /// <summary>
    /// Deletes all document records that came from the given source file name. Used to make
    /// re-ingesting a file idempotent — re-uploading the same file replaces its old rows
    /// instead of appending duplicates alongside them.
    /// </summary>
    /// <param name="fileName">The FileName value the records were stored under.</param>
    Task DeleteByFileNameAsync(string fileName);
}