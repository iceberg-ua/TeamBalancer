namespace TeamBalancer.Core.Models;

/// <summary>
/// A player as they took part in a finished match: the name they played under, and what they
/// did in it.
/// </summary>
/// <remarks>
/// The name is the one stored with the match, not the one the player has now. That is the
/// whole point of writing it down beside the id: renaming someone, or deleting them from their
/// list, must not rewrite a game that has already been played.
/// </remarks>
public class FinishedPlayer
{
    /// <summary>
    /// Gets the player's identifier, as it was when the match was played. The player it points
    /// at may since have been renamed or deleted, so this is a link to follow only where a
    /// miss is acceptable - never the source of the name to show.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets the name the player was recorded under.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the goals they scored.
    /// </summary>
    public required int Goals { get; init; }

    /// <summary>
    /// Gets the assists they made.
    /// </summary>
    public required int Assists { get; init; }
}
