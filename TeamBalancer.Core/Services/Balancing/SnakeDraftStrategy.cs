using TeamBalancer.Core.Models;

namespace TeamBalancer.Core.Services.Balancing;

/// <summary>
/// Implements a snake draft (greedy) team balancing strategy.
/// Players are drafted one position group at a time - goalkeepers, then defenders,
/// midfielders, forwards, and finally players with no position set - and within each group
/// they are sorted by skill level and distributed in a snake pattern:
/// Team A, Team B, Team B, Team A, Team A, Team B, etc.
/// Supports optional shuffling to create variety while maintaining balance.
/// </summary>
public class SnakeDraftStrategy : BaseTeamBalancingStrategy
{
    private readonly Random _random = new();

    /// <summary>
    /// Balances players using a snake draft approach.
    /// </summary>
    /// <param name="players">The list of players to balance.</param>
    /// <param name="numberOfTeams">The number of teams to create.</param>
    /// <param name="shuffle">If true, adds randomization while maintaining overall balance.</param>
    public override List<Team> BalanceTeams(List<Player> players, int numberOfTeams, bool shuffle = false)
    {
        if (players == null || players.Count == 0)
        {
            throw new ArgumentException("Player list cannot be null or empty.", nameof(players));
        }

        if (numberOfTeams < 2)
        {
            throw new ArgumentException("Number of teams must be at least 2.", nameof(numberOfTeams));
        }

        // Create teams
        var teams = new List<Team>();
        
        for (int i = 0; i < numberOfTeams; i++)
        {
            teams.Add(new Team
            {
                Name = $"Team {(char)('A' + i)}"
            });
        }

        // Group players by primary position. SecondaryPosition is intentionally not used
        // here - this phase drafts on primary position only. Treating a secondary position
        // as a flex signal is a possible future enhancement, not an oversight.
        var goalkeepers = SortForDraft(players.Where(p => p.PrimaryPosition == Position.Goalkeeper), shuffle);
        var defenders = SortForDraft(players.Where(p => p.PrimaryPosition == Position.Defender), shuffle);
        var midfielders = SortForDraft(players.Where(p => p.PrimaryPosition == Position.Midfielder), shuffle);
        var forwards = SortForDraft(players.Where(p => p.PrimaryPosition == Position.Forward), shuffle);

        // Exactly one keeper per team - five- and seven-a-side sides only field one. Any
        // surplus goalkeepers join the flexible pool and are drafted as outfield players,
        // which also stops the snake handing a second keeper to a team while another has none.
        // When fewer goalkeepers than teams exist, the draft starts at team index 0 moving
        // forward, so each available keeper lands on a different team and the remaining
        // teams simply go without one.
        var keepers = goalkeepers.Take(numberOfTeams).ToList();
        var surplusGoalkeepers = goalkeepers.Skip(numberOfTeams).ToList();

        // Players with no position set never count toward positional coverage, so they fill
        // out the teams on skill alone, alongside any surplus goalkeepers.
        var flexible = SortForDraft(
            players.Where(p => p.PrimaryPosition == Position.Unspecified).Concat(surplusGoalkeepers),
            shuffle);

        // The snake cursor carries across position groups on purpose. Resetting it per group
        // would hand the first team the strongest player of every position.
        int currentTeamIndex = 0;
        int direction = 1;

        DraftGroup(teams, keepers, ref currentTeamIndex, ref direction);
        DraftGroup(teams, defenders, ref currentTeamIndex, ref direction);
        DraftGroup(teams, midfielders, ref currentTeamIndex, ref direction);
        DraftGroup(teams, forwards, ref currentTeamIndex, ref direction);
        DraftGroup(teams, flexible, ref currentTeamIndex, ref direction);

        return teams;
    }

    /// <summary>
    /// Sorts a position group by skill for drafting, applying tier shuffling when requested.
    /// </summary>
    private List<Player> SortForDraft(IEnumerable<Player> players, bool shuffle)
    {
        var sorted = players
            .OrderByDescending(p => p.OverallSkillLevel)
            .ThenByDescending(p => p.Speed)
            .ThenByDescending(p => p.TechnicalSkills)
            .ThenByDescending(p => p.Stamina)
            .ToList();

        return shuffle ? ShuffleWithinTiers(sorted) : sorted;
    }

    /// <summary>
    /// Distributes one group of players across the teams using the snake draft pattern:
    /// A, B, B, A, A, B... (each team picks once, then direction reverses). The cursor is
    /// passed by reference so successive groups continue the pattern rather than restarting.
    /// </summary>
    private static void DraftGroup(List<Team> teams, List<Player> group, ref int currentTeamIndex, ref int direction)
    {
        foreach (var player in group)
        {
            teams[currentTeamIndex].AddPlayer(player);

            // Calculate next team index
            int nextIndex = currentTeamIndex + direction;

            // If next index is out of bounds, reverse direction (team picks again at the turn)
            if (nextIndex >= teams.Count || nextIndex < 0)
            {
                direction = -direction;
            }
            else
            {
                currentTeamIndex = nextIndex;
            }
        }
    }

    /// <summary>
    /// Shuffles players within skill tiers to add variety while maintaining balance.
    /// Groups players by similar skill levels and randomizes within each group.
    /// </summary>
    private List<Player> ShuffleWithinTiers(List<Player> sortedPlayers)
    {
        var result = new List<Player>();

        // Define tier size - group players into tiers of similar skill
        // For example, every 2-4 players of similar skill are shuffled together
        int tierSize = Math.Max(2, sortedPlayers.Count / 6); // Adjust tier size based on player count

        for (int i = 0; i < sortedPlayers.Count; i += tierSize)
        {
            // Get players in this tier
            var tier = sortedPlayers
                .Skip(i)
                .Take(tierSize)
                .OrderBy(_ => _random.Next()) // Shuffle within tier
                .ToList();

            result.AddRange(tier);
        }

        return result;
    }
}
