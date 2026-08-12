namespace TeamBalancer.Core.Tests.TestSupport;

using TeamBalancer.Core.Services.Csv;

/// <summary>
/// A throwaway stand-in for the app's data directory, deleted when the test finishes. Used by
/// the player list tests, which span several real files - lists.csv plus one player file per
/// list - and care about which of them exist.
/// </summary>
internal sealed class TempDataDirectory : IDisposable
{
    public TempDataDirectory()
    {
        Path_ = Path.Combine(Path.GetTempPath(), "TeamBalancerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path_);
    }

    /// <summary>
    /// Full path to the directory.
    /// </summary>
    public string Path_ { get; }

    /// <summary>
    /// Full path to the list metadata file, whether or not it exists yet.
    /// </summary>
    public string ListsPath => PlayerListFiles.ListsFilePath(Path_);

    /// <summary>
    /// Full path to a list's player file, whether or not it exists yet.
    /// </summary>
    public string PlayerFilePath(Guid listId) => PlayerListFiles.PlayerFilePath(Path_, listId);

    /// <summary>
    /// Writes the player file a pre-multi-list install would have left behind.
    /// </summary>
    /// <param name="contents">The CSV contents of that file.</param>
    public void WriteLegacyPlayerFile(string contents) =>
        File.WriteAllText(Path.Combine(Path_, PlayerListFiles.LegacyPlayerFileName), contents);

    /// <summary>
    /// Reads back whatever is currently in one of this directory's files.
    /// </summary>
    /// <param name="fileName">The file's name, without a directory.</param>
    public string Read(string fileName) => File.ReadAllText(Path.Combine(Path_, fileName));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path_))
            {
                Directory.Delete(Path_, recursive: true);
            }
        }
        catch (IOException)
        {
            // A locked file must not fail an otherwise passing test.
        }
    }
}
