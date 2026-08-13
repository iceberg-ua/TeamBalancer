namespace TeamBalancer.Core.Models;

/// <summary>
/// The outcome of reading a CSV: the players it yielded, and the rows it could not turn into
/// one. The dropped counts exist so an import can tell the user their file had rows the app did
/// not use, rather than reporting only what survived and leaving the rest unexplained.
/// </summary>
/// <param name="Players">The players successfully read from the file.</param>
/// <param name="UnreadableRowCount">
/// Rows dropped before a player could be built - too few columns, or a skill value that is not
/// a number at all.
/// </param>
/// <param name="InvalidSkillRowCount">
/// Rows that read cleanly but carry a skill value outside 1-3. Counted apart from unreadable
/// rows because it is the one the user can fix by editing a number rather than the file's shape.
/// </param>
public sealed record CsvParseResult(
    IReadOnlyList<Player> Players,
    int UnreadableRowCount,
    int InvalidSkillRowCount)
{
    /// <summary>
    /// Gets the number of data rows that did not become a player.
    /// </summary>
    public int SkippedRowCount => UnreadableRowCount + InvalidSkillRowCount;

    /// <summary>
    /// Gets the number of data rows the file held, however each one turned out.
    /// </summary>
    public int TotalRows => Players.Count + SkippedRowCount;
}
