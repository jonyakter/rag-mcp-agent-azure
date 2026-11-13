using AIRecommendation.Application.Configuration;
using MyExperiment.Mcp;

var port = Environment.GetEnvironmentVariable("PORT") ?? "5080";
var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? $"http://+:{port}";

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls($"http://+:{port}");

builder.Services.Configure<McpConfig>(
    builder.Configuration.GetSection("McpConfig"));

builder.Services.Configure<OpenAiSettings>(
    builder.Configuration.GetSection("OpenAI"));

builder.Services.AddSingleton<MyExperiment.Mcp.RecommendationService>();
builder.Services.AddHttpClient();

builder.Services
    .AddMcpServer()
    .WithHttpTransport(o => o.Stateless = true)
    .WithTools<RecommendationTools>()
    .WithTools<EmbeddingTools>()
    .WithTools<CosineSimilarityTools>()
    .WithTools<SaveRecommendationTools>()
    .WithTools<IngestDocumentTools>();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    status = "ok",
    server = "MyExperiment.Mcp — Insight Agent",
    tools = new[]
    {
        "Recommend",
        "GenerateEmbedding",
        "FindSimilarDocuments",
        "SaveRecommendation",
        "IngestDocument"
    }
}));

app.MapMcp("/mcp");

app.Run();