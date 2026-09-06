namespace TeamBalancer.Core.Models;

/// <summary>
/// A match as it comes back out of storage: what was played, when, and how it ended.
/// </summary>
/// <remarks>
/// Deliberately not <see cref="MatchRecord"/>. A match being played is a thing with rules - a
/// score that cannot fall below the goals pinned to players, assists that cannot outnumber
/// goals - and every one of them exists to keep the tallies honest while they are tapped in at
/// the side of a pitch. A match that has been finished is none of that. It is a result, and
/// the only thing asked of it is to be read.
///
/// Reading one back into the live model would also mean inventing what the file does not hold:
/// matches.csv stores a player's name and id, not their skills or position, so the players it
/// returned would be fictional everywhere but their names. A model that carries only what was
/// written cannot mislead a screen into showing a rating nobody recorded.
/// </remarks>
public class FinishedMatch
{
    /// <summary>
    /// Gets the identifier the match's rows were grouped back together by.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets when the match was accepted, in UTC. Storage is written in UTC so that a phone
    /// carried across a timezone does not reorder the games it has already played; converting
    /// for display is the reading screen's job.
    /// </summary>
    public required DateTime PlayedAt { get; init; }

    /// <summary>
    /// Gets the identifier of the player list the sides were drawn from.
    /// </summary>
    public required Guid ListId { get; init; }

    /// <summary>
    /// Gets the sides, in the order they were written.
    /// </summary>
    public required IReadOnlyList<FinishedTeam> Teams { get; init; }

    /// <summary>
    /// Gets the total number of players who took part, across both sides.
    /// </summary>
    public int PlayerCount => Teams.Sum(t => t.Players.Count);

    /// <summary>
    /// Gets the number of goals in the result that no scorer was ever named for.
    /// </summary>
    public int UnattributedGoals => Teams.Sum(t => t.UnattributedGoals);

    /// <summary>
    /// Gets whether any goal in the result is without a scorer. A game recorded in a hurry is
    /// a perfectly valid game, so this is shown as a note rather than treated as damage.
    /// </summary>
    public bool HasUnattributedGoals => UnattributedGoals > 0;
}
