namespace TeamBalancer.Core.Services.Csv;

/// <summary>
/// The file naming convention behind multiple player lists: one CSV per list, plus the
/// metadata file that names them. Both the list repository and the active-list repository
/// resolve paths through here so a list and its players can never end up in disagreement
/// about which file they live in.
/// </summary>
public static class PlayerListFiles
{
    /// <summary>
    /// The player file every build before multi-list support read and wrote unconditionally.
    /// The list migration creates for that squad keeps this name, which is what makes the
    /// migration a metadata-only operation instead of a data move.
    /// </summary>
    public const string LegacyPlayerFileName = "players.csv";

    /// <summary>
    /// The metadata file holding one row per list.
    /// </summary>
    public const string ListsFileName = "lists.csv";

    /// <summary>
    /// The id reserved for the list that owns the legacy player file. It is a fixed value
    /// rather than a generated one because something has to answer "which list is stored in
    /// players.csv?" after a restart, and a fixed id answers it without adding a file name
    /// column to lists.csv that could contradict the rest of the convention.
    /// </summary>
    public static readonly Guid DefaultListId = new("00000000-0000-0000-0000-0000000000fb");

    /// <summary>
    /// Gets the name of the CSV file holding a list's players.
    /// </summary>
    /// <param name="listId">The list's unique identifier.</param>
    /// <returns>The file name, without a directory.</returns>
    public static string PlayerFileNameFor(Guid listId) =>
        listId == DefaultListId ? LegacyPlayerFileName : $"players_{listId}.csv";

    /// <summary>
    /// Gets the full path of the CSV file holding a list's players.
    /// </summary>
    /// <param name="dataDirectory">The directory the app keeps its data in.</param>
    /// <param name="listId">The list's unique identifier.</param>
    /// <returns>The full path to the list's player file.</returns>
    public static string PlayerFilePath(string dataDirectory, Guid listId) =>
        Path.Combine(dataDirectory, PlayerFileNameFor(listId));

    /// <summary>
    /// Gets the full path of the list metadata file.
    /// </summary>
    /// <param name="dataDirectory">The directory the app keeps its data in.</param>
    /// <returns>The full path to lists.csv.</returns>
    public static string ListsFilePath(string dataDirectory) =>
        Path.Combine(dataDirectory, ListsFileName);
}
