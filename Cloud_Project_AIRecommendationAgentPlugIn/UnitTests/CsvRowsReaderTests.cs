using AIRecommendation.Infrastructure.Parsing;
using Xunit;

namespace UnitTests;

public class CsvRowsReaderTests
{
    [Fact]
    public void ReadRows_ParsesHeaderAndValuesIntoDictionaries()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(path, new[]
            {
                "Title,Category",
                "Wait Until Dark,Movie",
                "Frankfurter Wirtshaus,Restaurant",
            });

            var rows = CsvRowsReader.ReadRows(path).ToList();

            Assert.Equal(2, rows.Count);
            Assert.Equal("Wait Until Dark", rows[0]["Title"]);
            Assert.Equal("Movie", rows[0]["Category"]);
            Assert.Equal("Frankfurter Wirtshaus", rows[1]["Title"]);
            Assert.Equal("Restaurant", rows[1]["Category"]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadRows_HandlesQuotedValuesContainingDelimiter()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(path, new[]
            {
                "Title,Description",
                "\"Comedy, Drama\",\"A story about family, loss, and hope\"",
            });

            var rows = CsvRowsReader.ReadRows(path).ToList();

            Assert.Single(rows);
            Assert.Equal("Comedy, Drama", rows[0]["Title"]);
            Assert.Equal("A story about family, loss, and hope", rows[0]["Description"]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadRows_HeaderOnlyFile_ReturnsNoRows()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(path, new[] { "Title,Category" });

            var rows = CsvRowsReader.ReadRows(path).ToList();

            Assert.Empty(rows);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
