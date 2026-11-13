using AIRecommendation.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AIRecommendation.Infrastructure.Persistence;

/// <summary>
/// The Entity Framework Core database context for the AI Recommendation system.
/// </summary>
public class AppDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AppDbContext"/> class.
    /// </summary>
    /// <param name="options">The options to be used by this context.</param>
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    /// <summary>
    /// Gets or sets the database set of document records.
    /// </summary>
    public DbSet<DocumentRecord> Documents => Set<DocumentRecord>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DocumentRecord>(entity =>
        {
            entity.ToTable("Documents");

            // Limit Category and FileName to 450 chars so they can be used
            // as SQL Server index key columns (nvarchar(max) cannot be indexed).
            entity.Property(d => d.Category).HasMaxLength(450);
            entity.Property(d => d.FileName).HasMaxLength(450);

            // Composite index for faster category + filename lookups.
            entity.HasIndex(d => new { d.Category, d.FileName });
        });

        base.OnModelCreating(modelBuilder);
    }

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.ConfigureWarnings(w =>
            w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    }
}