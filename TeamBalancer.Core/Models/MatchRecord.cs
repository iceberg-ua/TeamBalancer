namespace TeamBalancer.Core.Models;

/// <summary>
/// A match: the two sides that were accepted from a split, what they scored, and who scored
/// it. This is the thing a finished game is written to storage as.
/// </summary>
public class MatchRecord
{
    /// <summary>
    /// Gets the unique identifier for the match. Rows of a flat file are grouped back into a
    /// match by this, so it is generated once here rather than at the point of writing.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets when the match was accepted, in UTC.
    /// </summary>
    public DateTime PlayedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the identifier of the player list the sides were drawn from.
    /// </summary>
    public Guid ListId { get; init; }

    /// <summary>
    /// Gets the two sides.
    /// </summary>
    public List<MatchTeam> Teams { get; init; } = [];

    /// <summary>
    /// Gets the total number of players taking part, across both sides.
    /// </summary>
    public int PlayerCount => Teams.Sum(t => t.Players.Count);

    /// <summary>
    /// Gets whether either side has a goal in its score with no scorer named.
    /// </summary>
    public bool HasUnattributedGoals => Teams.Any(t => t.HasUnattributedGoals);

    /// <summary>
    /// Gets the number of goals across the match that no scorer has been named for.
    /// </summary>
    public int UnattributedGoals => Teams.Sum(t => t.UnattributedGoals);

    /// <summary>
    /// Starts a match from an accepted split. The players themselves are shared with the
    /// split rather than copied, but the sides are new lists, so moving someone between teams
    /// here does not reach back and rearrange the split it came from.
    /// </summary>
    /// <param name="teams">The teams as they were accepted.</param>
    /// <param name="listId">The list the players were drawn from.</param>
    /// <returns>A match ready to be played.</returns>
    public static MatchRecord FromTeams(IEnumerable<Team> teams, Guid listId)
    {
        ArgumentNullException.ThrowIfNull(teams);

        return new MatchRecord
        {
            ListId = listId,
            Teams =
            [
                .. teams.Select(team => new MatchTeam
                {
                    Name = team.Name,
                    Players = [.. team.Players.Select(player => new MatchPlayer { Player = player })]
                })
            ]
        };
    }

    /// <summary>
    /// Gets whether a player is already taking part on either side. This is what keeps the
    /// mid-match add from offering someone who is already on the pitch.
    /// </summary>
    /// <param name="playerId">The player's unique identifier.</param>
    public bool Contains(Guid playerId) => Teams.Any(t => t.Find(playerId) != null);

    /// <summary>
    /// Moves a player to the other side, carrying their goals and assists with them. Whoever
    /// they scored for was decided by which side they were on, so correcting the side has to
    /// correct the scores too.
    /// </summary>
    /// <param name="matchPlayer">The player to move.</param>
    /// <param name="from">The side they are leaving.</param>
    /// <param name="to">The side they are joining.</param>
    public static void Move(MatchPlayer matchPlayer, MatchTeam from, MatchTeam to)
    {
        ArgumentNullException.ThrowIfNull(matchPlayer);
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        if (from.Remove(matchPlayer))
        {
            to.Players.Add(matchPlayer);
        }
    }
}
