namespace TeamBalancer.Core.Models;

/// <summary>
/// One side of a match being played: its line-up, what each player did, and the score.
/// </summary>
public class MatchTeam
{
    /// <summary>
    /// Gets or sets the team name, carried over from the split this match was accepted from.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets the players on this side, including anyone who joined after kick-off.
    /// </summary>
    public List<MatchPlayer> Players { get; init; } = [];

    /// <summary>
    /// Gets the score entered by hand, which is zero until the user enters one.
    /// </summary>
    /// <remarks>
    /// This is not the score shown - <see cref="Score"/> is. Keeping the entered figure apart
    /// from the one on the scoreboard is what lets goals be attributed to scorers afterwards
    /// without the total moving underneath the user.
    /// </remarks>
    public int ManualScore { get; private set; }

    /// <summary>
    /// Gets the number of goals that have been attributed to a scorer.
    /// </summary>
    public int AttributedGoals => Players.Sum(p => p.Goals);

    /// <summary>
    /// Gets the score, which is the larger of what was entered by hand and what has been
    /// attributed to scorers.
    /// </summary>
    /// <remarks>
    /// The whole behaviour asked of the scoreboard falls out of that one comparison. With
    /// nothing entered by hand the score simply counts the goals as they are attributed. With
    /// a figure entered, that figure stands while scorers are named, because naming them
    /// cannot make the sum exceed it. Name one goal too many and the sum takes over, which is
    /// the only way the score can be pushed past what was entered.
    ///
    /// It is recomputed rather than remembered, so deleting a goal that had pushed the score
    /// up lets it fall back to whichever of the two is now the larger.
    /// </remarks>
    public int Score => Math.Max(ManualScore, AttributedGoals);

    /// <summary>
    /// Gets the goals in the score that no scorer has been named for. Never negative, since
    /// the score is never below the attributed sum.
    /// </summary>
    public int UnattributedGoals => Score - AttributedGoals;

    /// <summary>
    /// Gets whether any goal in the score is still without a scorer.
    /// </summary>
    public bool HasUnattributedGoals => UnattributedGoals > 0;

    /// <summary>
    /// Sets the score by hand. A figure below the goals already attributed cannot take effect,
    /// so it is refused rather than stored and silently overridden - a stored figure the
    /// scoreboard disagrees with would surface later as a score that jumps on its own when a
    /// goal is deleted.
    /// </summary>
    /// <param name="value">The score to set.</param>
    /// <returns>True if the score was set, false if it was below the attributed goals.</returns>
    public bool TrySetScore(int value)
    {
        if (value < AttributedGoals || value < 0)
        {
            return false;
        }

        ManualScore = value;
        return true;
    }

    /// <summary>
    /// Adds a goal to the score without naming who scored it.
    /// </summary>
    public void IncrementScore() => ManualScore = Score + 1;

    /// <summary>
    /// Gets whether the score can be taken down by one. It cannot go below the goals already
    /// attributed to scorers - one of those has to be taken off its scorer first.
    /// </summary>
    public bool CanDecrementScore => Score > AttributedGoals;

    /// <summary>
    /// Takes one off the score, ignoring the request when every goal in it has a scorer.
    /// </summary>
    public void DecrementScore()
    {
        if (CanDecrementScore)
        {
            ManualScore = Score - 1;
        }
    }

    /// <summary>
    /// Finds a player's part in this match.
    /// </summary>
    /// <param name="playerId">The player's unique identifier.</param>
    /// <returns>Their entry, or null when they are not on this side.</returns>
    public MatchPlayer? Find(Guid playerId) =>
        Players.FirstOrDefault(p => p.Player.Id == playerId);

    /// <summary>
    /// Adds a player to this side, mid-match or otherwise. Nothing is rebalanced around them.
    /// </summary>
    /// <param name="player">The player joining.</param>
    /// <returns>Their entry in this match, existing or newly created.</returns>
    public MatchPlayer Add(Player player)
    {
        var existing = Find(player.Id);
        if (existing != null)
        {
            return existing;
        }

        var joined = new MatchPlayer { Player = player };
        Players.Add(joined);

        return joined;
    }

    /// <summary>
    /// Removes a player's entry from this side, keeping their goals and assists with them so
    /// that moving to the other team carries their record across rather than resetting it.
    /// </summary>
    /// <param name="matchPlayer">The entry to remove.</param>
    /// <returns>True if it was on this side.</returns>
    public bool Remove(MatchPlayer matchPlayer) => Players.Remove(matchPlayer);
}
