namespace ETL.Tests.TestHelpers;

/// <summary>
/// Writes the given rows to a temporary CSV file which is deleted again on dispose.
/// </summary>
public sealed class TempCsvFile : IDisposable
{
    private TempCsvFile(string path) => this.Path = path;

    public string Path { get; }

    public static TempCsvFile Create(params CsvRow[] rows)
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"etl-test-{Guid.NewGuid():N}.csv");
        File.WriteAllLines(path, [CsvRow.Header, .. rows.Select(r => r.ToCsvLine())]);
        return new TempCsvFile(path);
    }

    public void Dispose()
    {
        if (File.Exists(this.Path))
        {
            File.Delete(this.Path);
        }
    }
}
