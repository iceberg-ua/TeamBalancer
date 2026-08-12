namespace TeamBalancer.Core.Models;

/// <summary>
/// Describes one named list of players - "Sunday League", "Work 5-a-side" - without holding
/// any of its players. Each list keeps its players in a CSV file of its own, and only the
/// active list's file is loaded at a time, so this is the metadata row that names a list and
/// identifies which file belongs to it.
/// </summary>
public class PlayerListInfo
{
    /// <summary>
    /// Gets or sets the unique identifier for the list. It also decides which CSV file holds
    /// the list's players.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the name the user gave the list. Held to the same rules as a player name,
    /// in <see cref="CsvSafeName"/> - both end up as a CSV cell the user can open in a
    /// spreadsheet - and validated where lists are created and renamed.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date the list was created. Lists are ordered by this, so "the first
    /// list" means the same thing everywhere.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the date the list's metadata was last changed - which today only a rename
    /// does, since editing players touches the list's own file rather than this row.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Returns a string representation of the list.
    /// </summary>
    public override string ToString() => $"{Name} ({Id})";
}
