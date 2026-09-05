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
    /// Gets the goals recorded with nobody named for them - the corner-of-the-eye goal, tapped
    /// straight onto the scoreboard.
    /// </summary>
    /// <remarks>
    /// These are goals in their own right, counted alongside the attributed ones rather than
    /// folded into <see cref="ManualScore"/>. Folding them in would make the next goal pinned
    /// to a scorer disappear into the figure they had raised: a side that scored one named
    /// goal, one nobody saw, and then a second named goal would show two rather than three.
    /// </remarks>
    public int AnonymousGoals { get; private set; }

    /// <summary>
    /// Gets the number of goals that have been attributed to a scorer.
    /// </summary>
    public int AttributedGoals => Players.Sum(p => p.Goals);

    /// <summary>
    /// Gets the number of assists credited to players on this side.
    /// </summary>
    public int AttributedAssists => Players.Sum(p => p.Assists);

    /// <summary>
    /// Gets the goals actually recorded one by one, whether or not a scorer was named for them.
    /// </summary>
    public int RecordedGoals => AttributedGoals + AnonymousGoals;

    /// <summary>
    /// Gets the figure the score can never go below: the goals and assists pinned to a player.
    /// A goal carries at most one assist, so a side credited with five assists scored at least
    /// five goals, exactly as a side with five named scorers did - both are evidence of goals
    /// that were definitely scored.
    /// </summary>
    /// <remarks>
    /// Anonymous goals are not in the floor and the figure entered by hand is not either, since
    /// both can be taken back off the scoreboard by the button that put them there.
    /// </remarks>
    public int ScoreFloor => Math.Max(AttributedGoals, AttributedAssists);

    /// <summary>
    /// Gets the score: the larger of what was entered by hand and what the goals and assists
    /// recorded prove was scored.
    /// </summary>
    /// <remarks>
    /// The whole behaviour asked of the scoreboard falls out of that comparison. With nothing
    /// entered by hand the score simply counts the goals as they are recorded, named or not.
    /// With a figure entered, that figure stands while scorers are named, because naming them
    /// cannot make the count exceed it. Record one goal too many and the count takes over,
    /// which is the only way the score can be pushed past what was entered.
    ///
    /// It is recomputed rather than remembered, so deleting a goal that had pushed the score
    /// up lets it fall back to whichever term is now the larger.
    ///
    /// Assists sit in the floor rather than only being capped on the way in, and that is what
    /// makes "assists cannot outnumber goals" true at all times rather than merely true while
    /// the buttons are the only way in. Adding one is capped, so in normal use the score never
    /// moves because of an assist. But a player sent to the other side takes their goals and
    /// assists with them, and the two need not leave in step - the side they left can lose
    /// three goals and no assists. Without assists in the floor that side would be left
    /// claiming more assists than goals; with them, its score cannot fall that far.
    /// </remarks>
    public int Score => Math.Max(ManualScore, Math.Max(RecordedGoals, AttributedAssists));

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
    /// Sets the score by hand. A figure below the goals and assists already pinned to a player
    /// cannot take effect, so it is refused rather than stored and silently overridden - a
    /// stored figure the scoreboard disagrees with would surface later as a score that jumps on
    /// its own when a goal is deleted.
    /// </summary>
    /// <param name="value">The score to set.</param>
    /// <returns>True if the score was set, false if it was below <see cref="ScoreFloor"/>.</returns>
    public bool TrySetScore(int value)
    {
        if (value < ScoreFloor || value < 0)
        {
            return false;
        }

        ManualScore = value;

        // Typing a score under the goals tapped onto the scoreboard takes those goals off it,
        // exactly as pressing minus that many times would. They are not pinned to anyone, so
        // there is nothing here that the user has to go and undo first.
        AnonymousGoals = Math.Min(AnonymousGoals, value - AttributedGoals);

        return true;
    }

    /// <summary>
    /// Adds a goal to the score without naming who scored it.
    /// </summary>
    /// <remarks>
    /// Where the score is already ahead of the goals recorded - a figure entered by hand, or an
    /// assist standing in for a goal nobody was named for - that lead is what is carrying the
    /// unnamed goals, so it is what grows. Otherwise the goal is recorded as one more anonymous
    /// goal, so that naming a scorer afterwards adds to it rather than being swallowed by it.
    /// Either way the score goes up by exactly one.
    /// </remarks>
    public void IncrementScore()
    {
        if (Score > RecordedGoals)
        {
            ManualScore = Score + 1;
        }
        else
        {
            AnonymousGoals++;
        }
    }

    /// <summary>
    /// Gets whether the score can be taken down by one. It cannot go below the goals and
    /// assists pinned to a player - one of those has to come off its player first.
    /// </summary>
    public bool CanDecrementScore => Score > ScoreFloor;

    /// <summary>
    /// Takes one off the score, ignoring the request when every goal in it is pinned to a
    /// player. Whichever of the two loose terms is holding the score up comes down with it.
    /// </summary>
    public void DecrementScore()
    {
        if (!CanDecrementScore)
        {
            return;
        }

        var target = Score - 1;

        ManualScore = Math.Min(ManualScore, target);
        AnonymousGoals = Math.Min(AnonymousGoals, target - AttributedGoals);
    }

    /// <summary>
    /// Gets whether another assist can be credited to anyone on this side. A goal carries at
    /// most one, so a side cannot record more assists than it scored goals.
    /// </summary>
    public bool CanAddAssist => AttributedAssists < Score;

    /// <summary>
    /// Credits a player on this side with a goal. Always allowed: a goal is its own evidence,
    /// and it takes the score up with it once the named goals outnumber the figure entered.
    /// </summary>
    /// <param name="participant">The scorer.</param>
    public void AddGoal(MatchPlayer participant) => participant.AddGoal();

    /// <summary>
    /// Takes a goal back off a player. Always allowed, even where it drops the named goals
    /// below the assists recorded: the score keeps those assists covered by itself, and
    /// someone correcting a mis-tapped scorer should not have to dismantle the assists first.
    /// </summary>
    /// <param name="participant">The player it was credited to.</param>
    public void RemoveGoal(MatchPlayer participant) => participant.RemoveGoal();

    /// <summary>
    /// Credits a player on this side with an assist, if there is a goal left to assist.
    /// </summary>
    /// <param name="participant">The player who made it.</param>
    public void AddAssist(MatchPlayer participant)
    {
        if (CanAddAssist)
        {
            participant.AddAssist();
        }
    }

    /// <summary>
    /// Takes an assist back off a player. Always allowed - it can only bring the side further
    /// inside the rule.
    /// </summary>
    /// <param name="participant">The player it was credited to.</param>
    public void RemoveAssist(MatchPlayer participant) => participant.RemoveAssist();

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

    /// <summary>
    /// Takes in an entry arriving from the other side, tallies and all.
    /// </summary>
    /// <remarks>
    /// This is <see cref="Add(Player)"/>'s rule applied to an entry that already has a record:
    /// one person is one entry on a side. Two entries for the same player would both be summed
    /// into the score and would both be written down when the match is finished, so an arrival
    /// who is somehow already here is merged into the entry that is here rather than appended
    /// beside it.
    /// </remarks>
    /// <param name="arriving">The entry joining this side.</param>
    internal void Absorb(MatchPlayer arriving)
    {
        var existing = Find(arriving.Player.Id);

        if (existing == null)
        {
            Players.Add(arriving);
            return;
        }

        for (var goal = 0; goal < arriving.Goals; goal++)
        {
            existing.AddGoal();
        }

        for (var assist = 0; assist < arriving.Assists; assist++)
        {
            existing.AddAssist();
        }
    }
}
