namespace TeamBalancer.Core.Models;

/// <summary>
/// A player as they took part in one match: who they are, and what they did in it.
/// </summary>
/// <remarks>
/// Goals and assists are independent tallies rather than a list of goal events, so the record
/// cannot say which assist fed which goal. That is deliberate: the tallies are tapped at the
/// side of a pitch while the game is still going on, and pairing every assist to a goal would
/// cost a step per goal to record something nobody asks of a Sunday kickabout.
/// </remarks>
public class MatchPlayer
{
    /// <summary>
    /// Gets the player taking part.
    /// </summary>
    public required Player Player { get; init; }

    /// <summary>
    /// Gets the number of goals scored, never below zero.
    /// </summary>
    public int Goals { get; private set; }

    /// <summary>
    /// Gets the number of assists made, never below zero.
    /// </summary>
    public int Assists { get; private set; }

    /// <summary>
    /// Gets whether there is a goal to take back off this player.
    /// </summary>
    public bool HasGoals => Goals > 0;

    /// <summary>
    /// Gets whether there is an assist to take back off this player.
    /// </summary>
    public bool HasAssists => Assists > 0;

    /// <summary>
    /// Credits the player with a goal.
    /// </summary>
    public void AddGoal() => Goals++;

    /// <summary>
    /// Takes a goal back off the player, ignoring the request when they have none. The guard
    /// lives here rather than in the button that calls it so no caller can drive the tally
    /// negative and, through it, the team's score.
    /// </summary>
    public void RemoveGoal()
    {
        if (HasGoals)
        {
            Goals--;
        }
    }

    /// <summary>
    /// Credits the player with an assist.
    /// </summary>
    public void AddAssist() => Assists++;

    /// <summary>
    /// Takes an assist back off the player, ignoring the request when they have none.
    /// </summary>
    public void RemoveAssist()
    {
        if (HasAssists)
        {
            Assists--;
        }
    }
}
