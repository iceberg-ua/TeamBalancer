namespace TeamBalancer.Core.Models;

/// <summary>
/// One side of a finished match: its name, what it scored, and the line-up as it stood at the
/// final whistle.
/// </summary>
public class FinishedTeam
{
    /// <summary>
    /// Gets the team name as it was written down, not as it would be generated today.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the final score. It is stored rather than recomputed: the score of a match being
    /// played is derived from what was entered and what was recorded, and once the match is
    /// over that derivation has no inputs left to run on.
    /// </summary>
    public required int Score { get; init; }

    /// <summary>
    /// Gets the players who finished on this side, including anyone who joined after kick-off.
    /// Empty for a side left with nobody on it, which the file still records because the score
    /// belongs to the side rather than to anyone on it.
    /// </summary>
    public required IReadOnlyList<FinishedPlayer> Players { get; init; }

    /// <summary>
    /// Gets the goals that have a scorer named.
    /// </summary>
    public int AttributedGoals => Players.Sum(p => p.Goals);

    /// <summary>
    /// Gets the assists credited to players on this side.
    /// </summary>
    public int AttributedAssists => Players.Sum(p => p.Assists);

    /// <summary>
    /// Gets the goals in the score that nobody was named for.
    /// </summary>
    /// <remarks>
    /// Floored at zero rather than trusted to be positive. The writing side keeps the score at
    /// or above the goals pinned to players, but this reads a file that can be older than that
    /// guarantee or edited by hand, and a negative count would print as one.
    /// </remarks>
    public int UnattributedGoals => Math.Max(0, Score - AttributedGoals);

    /// <summary>
    /// Gets whether any goal in this side's score is without a scorer.
    /// </summary>
    public bool HasUnattributedGoals => UnattributedGoals > 0;
}
