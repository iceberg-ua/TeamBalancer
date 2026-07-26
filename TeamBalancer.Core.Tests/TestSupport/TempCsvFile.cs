namespace TeamBalancer.Core.Tests.TestSupport;

/// <summary>
/// A CSV file in a throwaway directory, deleted when the test finishes.
/// Used by repository tests, which read and write real files.
/// </summary>
internal sealed class TempCsvFile : IDisposable
{
    private readonly string _directory;

    public TempCsvFile(string contents)
    {
        _directory = Path.Combine(Path.GetTempPath(), "TeamBalancerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        Path_ = Path.Combine(_directory, "players.csv");
        File.WriteAllText(Path_, contents);
    }

    /// <summary>
    /// Full path to the CSV file.
    /// </summary>
    public string Path_ { get; }

    /// <summary>
    /// Reads back whatever is currently on disk.
    /// </summary>
    public string Read() => File.ReadAllText(Path_);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // A locked file must not fail an otherwise passing test.
        }
    }
}
