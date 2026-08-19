namespace TeamBalancer.Core.Models;

/// <summary>
/// What an import did with every row of the user's file. Import used to report only the number
/// of players added, so a file that lost half its rows to long names or to players already in
/// the list still read as a plain success. Each way a row can be dropped is counted separately
/// so the screen can say which one happened rather than only that something did.
/// </summary>
public sealed class PlayerImportResult
{
    /// <summary>
    /// Gets the number of players added to the list.
    /// </summary>
    public int ImportedCount { get; init; }

    /// <summary>
    /// Gets the number of rows that could not be read as a player at all - too few columns, or
    /// a skill value that is not a number.
    /// </summary>
    public int UnreadableCount { get; init; }

    /// <summary>
    /// Gets the number of players rejected for a name that is empty or carries characters that
    /// would break the CSV. A name that was only too long is shortened and imported instead of
    /// being counted here.
    /// </summary>
    public int InvalidNameCount { get; init; }

    /// <summary>
    /// Gets the number of players whose name was shortened to fit
    /// <see cref="CsvSafeName.MaxLength"/>. These were imported, so they are part of
    /// <see cref="ImportedCount"/> and not of <see cref="SkippedCount"/> - the count is
    /// reported only so the user learns their names were changed on the way in.
    /// </summary>
    public int TruncatedCount { get; init; }

    /// <summary>
    /// Gets the number of players whose shortened name had a digit appended because another
    /// player already held it. These are a subset of <see cref="TruncatedCount"/>: shortening
    /// is what made the two names collide in the first place.
    /// </summary>
    public int NumberedCount { get; init; }

    /// <summary>
    /// Gets the number of players rejected for a skill value outside 1-3.
    /// </summary>
    public int InvalidSkillsCount { get; init; }

    /// <summary>
    /// Gets the number of players skipped because the list already holds that name. Only
    /// <see cref="ImportMode.AddOnly"/> skips them; a merge updates them instead.
    /// </summary>
    public int DuplicateCount { get; init; }

    /// <summary>
    /// Gets the number of players already in the list whose ratings or positions the import
    /// changed. A merge produces these in place of duplicates.
    /// </summary>
    public int UpdatedCount { get; init; }

    /// <summary>
    /// Gets the number of players already in the list that the import left alone because it
    /// carried exactly the same ratings and positions. Told apart from
    /// <see cref="UpdatedCount"/> so that receiving an unchanged squad reads as "nothing has
    /// changed" rather than as a list of edits that did nothing.
    /// </summary>
    public int UnchangedCount { get; init; }

    /// <summary>
    /// Gets the number of players dropped by an error the import did not expect.
    /// </summary>
    public int ErrorCount { get; init; }

    /// <summary>
    /// Gets the number of rows that did not become a player, for any reason.
    /// </summary>
    public int SkippedCount =>
        UnreadableCount + InvalidNameCount + InvalidSkillsCount + DuplicateCount + ErrorCount;

    /// <summary>
    /// Gets the number of data rows the file held. Updated and unchanged players count here
    /// too: they were rows the file carried and the import acted on, even though neither added
    /// a player nor dropped a row.
    /// </summary>
    public int TotalRows => ImportedCount + UpdatedCount + UnchangedCount + SkippedCount;

    /// <summary>
    /// Gets a value indicating whether the import changed nothing at all because every player
    /// it carried was already in the list with the same ratings. The merge counterpart of
    /// <see cref="IsEntirelyDuplicates"/>, and just as much a success rather than a failure.
    /// </summary>
    public bool IsEntirelyUnchanged => TotalRows > 0 && UnchangedCount == TotalRows;

    /// <summary>
    /// Gets a value indicating whether every row in the file was already in the list. This is
    /// the one all-skipped outcome that is not a problem with the file, and it reads very
    /// differently to the user, so it is worth telling apart.
    /// </summary>
    public bool IsEntirelyDuplicates => TotalRows > 0 && DuplicateCount == TotalRows;
}
