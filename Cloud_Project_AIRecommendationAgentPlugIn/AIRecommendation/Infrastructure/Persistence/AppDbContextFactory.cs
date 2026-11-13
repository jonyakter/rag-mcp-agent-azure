using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AIRecommendation.Infrastructure.Persistence;

/// <summary>
/// Factory for creating AppDbContext instances at design time (migrations, scaffolding).
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    /// <summary>
    /// Creates a new instance of AppDbContext with design-time settings.
    /// </summary>
    /// <param name="args">Arguments passed by the design-time tools.</param>
    /// <returns>A configured AppDbContext instance.</returns>
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        // Use SQLite for local development and design-time tools
        optionsBuilder.UseSqlite("Data Source=app.db");

        return new AppDbContext(optionsBuilder.Options);
    }
}