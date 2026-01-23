using TeamBalancer.Core.Models;

namespace TeamBalancer.Core.Services.Balancing;

/// <summary>
/// Implements a snake draft (greedy) team balancing strategy.
/// Players are sorted by skill level and distributed in a snake pattern:
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

        // Sort players by overall skill level (descending)
        var sortedPlayers = players
            .OrderByDescending(p => p.OverallSkillLevel)
            .ThenByDescending(p => p.Speed)
            .ThenByDescending(p => p.TechnicalSkills)
            .ThenByDescending(p => p.Stamina)
            .ToList();

        // If shuffle is enabled, group players by skill tier and shuffle within tiers
        // This maintains balance while adding variety
        if (shuffle)
        {
            sortedPlayers = ShuffleWithinTiers(sortedPlayers);
        }

        // Distribute players using snake draft pattern
        // Pattern: A, B, B, A, A, B, B, A... (each team picks once, then direction reverses)
        int currentTeamIndex = 0;
        int direction = 1;

        foreach (var player in sortedPlayers)
        {
            teams[currentTeamIndex].AddPlayer(player);

            // Calculate next team index
            int nextIndex = currentTeamIndex + direction;

            // If next index is out of bounds, reverse direction (team picks again at the turn)
            if (nextIndex >= numberOfTeams || nextIndex < 0)
            {
                direction = -direction;
            }
            else
            {
                currentTeamIndex = nextIndex;
            }
        }

        return teams;
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
