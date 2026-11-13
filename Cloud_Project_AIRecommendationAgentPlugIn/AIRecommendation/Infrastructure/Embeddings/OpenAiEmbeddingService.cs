using AIRecommendation.Application;
using AIRecommendation.Application.Configuration;
using AIRecommendation.Application.Interfaces;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AIRecommendation.Infrastructure.Embeddings;

/// <summary>
/// Generates embeddings using the OpenAI API.
/// HttpClient is injected via IHttpClientFactory (named "OpenAI").
/// Settings (ApiKey, Model, EmbeddingsPath) are injected via IOptions&lt;OpenAiSettings&gt;.
/// Nothing is hardcoded.
/// </summary>
public class OpenAiEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _http;
    private readonly string _model;
    private readonly string _embeddingsPath;

    /// <summary>
    /// Constructor called by the DI container.
    /// HttpClient comes from IHttpClientFactory registered as "OpenAI".
    /// ApiKey, Model, and EmbeddingsPath come from IOptions&lt;OpenAiSettings&gt; bound from appsettings.json.
    /// </summary>
    public OpenAiEmbeddingService(HttpClient httpClient, IOptions<OpenAiSettings> options)
    {
        var settings = options.Value;

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            throw new InvalidOperationException(
                "OpenAI ApiKey is not set. " +
                "Set environment variable: OpenAI__ApiKey=sk-xxxx");

        _model = settings.Model;
        _embeddingsPath = settings.EmbeddingsPath;
        _http = httpClient;
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", settings.ApiKey);
    }

    /// <inheritdoc/>
    public float[] GenerateEmbedding(string text)
        => GenerateEmbeddingAsync(text).GetAwaiter().GetResult();

    private async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        // _model and _embeddingsPath come from OpenAiSettings - NOT hardcoded
        var payload = new { model = _model, input = text };

        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var res = await _http.PostAsync(_embeddingsPath, content);
        res.EnsureSuccessStatusCode();

        var resJson = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(resJson);
        var data = doc.RootElement.GetProperty("data");

        if (data.GetArrayLength() == 0) return Array.Empty<float>();

        var embElem = data[0].GetProperty("embedding");
        var list = new List<float>(embElem.GetArrayLength());
        foreach (var v in embElem.EnumerateArray())
            list.Add(v.GetSingle());

        return list.ToArray();
    }
}