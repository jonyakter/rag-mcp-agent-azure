using AIRecommendation.Application.Interfaces;
using Daenet.DocumentParser.DocumentParsers;
using Daenet.DocumentParser.TessDataProvider;
using Microsoft.Extensions.Logging;

namespace AIRecommendation.Infrastructure.Parsing;

/// <summary>
/// A document parser that uses the Daenet DocumentParser library to extract text from PDF, Word, and image files (via OCR).
/// </summary>
public class DaenetDocumentParser : IDocumentParser
{
    private static readonly string[] SupportedExtensions =
    {
        ".pdf",
        ".docx",
        ".jpg",
        ".jpeg",
        ".png",
        ".bmp",
        ".gif",
        ".tiff",
        ".csv"

    };

    private readonly OcrParser _ocrParser;
    private readonly PDFParser _pdfParser;
    private readonly WordParser _wordParser;
    private readonly CsvParser _csvParser;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaenetDocumentParser"/> class.
    /// </summary>
    /// <param name="tessDataPath">Optional path to the Tesseract training data. If null, it will be resolved automatically.</param>
    /// <param name="language">The language to use for OCR (defaults to "eng").</param>
    public DaenetDocumentParser(string? tessDataPath = null, string language = "eng")
    {
        var resolvedTessDataPath = ResolveTessDataPath(tessDataPath);

        var loggerFactory = LoggerFactory.Create(builder => { });

        var ocrConfig = new OcrParserConfig
        {
            OcrParserLanguage = language,
            TessarectFilePath = resolvedTessDataPath,
            TessDataAzureBlobStorageSasUrl = string.Empty
        };

        var tessDataProvider = new LocalTessDataProvider(resolvedTessDataPath);

        _ocrParser = new OcrParser(
            loggerFactory.CreateLogger<OcrParser>(),
            ocrConfig,
            tessDataProvider
        );

        _pdfParser = new PDFParser(
            loggerFactory.CreateLogger<PDFParser>(),
            _ocrParser
        );

        _wordParser = new WordParser(
            loggerFactory.CreateLogger<WordParser>(),
            _ocrParser
        );

        _csvParser = new CsvParser();
    }

    /// <inheritdoc />
    public bool CanParse(string extension)
    {
        return SupportedExtensions.Contains(extension.ToLowerInvariant());
    }

    /// <summary>
    /// Asynchronously extracts text from the document at the specified path.
    /// </summary>
    /// <param name="path">The full path to the document.</param>
    /// <returns>A task representing the asynchronous operation, containing the extracted text.</returns>
    /// <exception cref="FileNotFoundException">Thrown if the file does not exist.</exception>
    /// <exception cref="NotSupportedException">Thrown if the file type is not supported by this parser.</exception>
    public async Task<string> ExtractTextAsync(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("File not found", path);

        var ext = Path.GetExtension(path).ToLowerInvariant();

        string? text = ext switch
        {
            ".pdf" => await _pdfParser.ReadContentAsync(path),
            ".docx" => await _wordParser.ReadContentAsync(path),
            ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".tiff"
                => await _ocrParser.ReadContentAsync(path),
            _ => throw new NotSupportedException(
                $"Unsupported file type for Daenet parser: {ext}"
            )
        };

        return text?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Attempts to resolve the path to the Tesseract training data directory.
    /// </summary>
    /// <param name="tessDataPath">The provided path, if any.</param>
    /// <returns>The resolved path to the tessdata directory.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown if the tessdata directory cannot be located.</exception>
    private static string ResolveTessDataPath(string? tessDataPath)
    {
        var candidates = new[]
        {
            tessDataPath,
            Path.Combine(AppContext.BaseDirectory, "tessdata"),
            Path.Combine(Directory.GetCurrentDirectory(), "tessdata"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "tessdata"))
        }
        .Where(p => !string.IsNullOrWhiteSpace(p))
        .Select(p => Path.GetFullPath(p!))
        .Distinct()
        .ToArray();

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
                return candidate;
        }

        throw new DirectoryNotFoundException(
            "Could not find tessdata folder. Checked:\n - " + string.Join("\n - ", candidates)
        );
    }

    /// <summary>
    /// Internal implementation of <see cref="ITessDataProvider"/> that provides Tesseract data from a local directory.
    /// </summary>
    private sealed class LocalTessDataProvider : ITessDataProvider
    {
        private readonly string _tessDataPath;

        public LocalTessDataProvider(string tessDataPath)
        {
            _tessDataPath = tessDataPath;
        }

        /// <inheritdoc />
        public async Task<TessData> GetTessDataAsync(string language)
        {
            var filePath = Path.Combine(_tessDataPath, $"{language}.traineddata");

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(
                    $"Tesseract language file not found: {filePath}. " +
                    $"Make sure '{language}.traineddata' exists inside tessdata."
                );
            }

            return new TessData
            {
                FileName = Path.GetFileName(filePath),
                Content = await File.ReadAllBytesAsync(filePath)
            };
        }
    }
}