namespace AIRecommendation.Application.Interfaces;

/// <summary>
/// Defines a contract for parsing various file formats and extracting text.
/// </summary>
public interface IDocumentParser
{
    /// <summary>
    /// Determines if the parser can handle files with the specified extension.
    /// </summary>
    /// <param name="extension">The file extension (e.g., ".txt", ".csv").</param>
    /// <returns>True if the extension is supported; otherwise, false.</returns>
    bool CanParse(string extension);

    /// <summary>
    /// Asynchronously extracts text from the file at the specified path.
    /// </summary>
    /// <param name="path">The full path to the file.</param>
    /// <returns>A task representing the asynchronous operation, containing the extracted text.</returns>
    Task<string> ExtractTextAsync(string path);
}