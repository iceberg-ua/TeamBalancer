using TeamBalancer.Core.Models;

namespace TeamBalancer.Core.Services.Balancing;

/// <summary>
/// Implements an iterative swap team balancing strategy.
/// Starts with an initial distribution and iteratively swaps players between teams
/// to minimize the balance score (variance in team skills).
/// This approach typically produces better balance than greedy methods.
/// </summary>
public class IterativeSwapStrategy : BaseTeamBalancingStrategy
{
    private readonly Random _random = new();
    private const int MaxIterations = 1000;
    private const double ImprovementThreshold = 0.0001;

    /// <summary>
    /// Balances players using iterative swapping approach.
    /// </summary>
    /// <param name="players">The list of players to balance.</param>
    /// <param name="numberOfTeams">The number of teams to create.</param>
    /// <param name="shuffle">Whether to use random initial distribution (recommended).</param>
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

        // Initial distribution using round-robin
        var playerList = shuffle
            ? players.OrderBy(_ => _random.Next()).ToList()
            : players.OrderByDescending(p => p.OverallSkillLevel).ToList();

        for (int i = 0; i < playerList.Count; i++)
        {
            teams[i % numberOfTeams].AddPlayer(playerList[i]);
        }

        // Iteratively improve balance by swapping players
        double currentScore = CalculateBalanceScore(teams);
        bool improved = true;
        int iterations = 0;

        while (improved && iterations < MaxIterations)
        {
            improved = false;
            iterations++;

            // Try swapping players between all pairs of teams
            for (int i = 0; i < numberOfTeams - 1; i++)
            {
                for (int j = i + 1; j < numberOfTeams; j++)
                {
                    // Try swapping each player from team i with each player from team j
                    foreach (var player1 in teams[i].Players.ToList())
                    {
                        foreach (var player2 in teams[j].Players.ToList())
                        {
                            // Perform swap
                            teams[i].RemovePlayer(player1);
                            teams[j].RemovePlayer(player2);
                            teams[i].AddPlayer(player2);
                            teams[j].AddPlayer(player1);

                            // Check if this improved balance
                            double newScore = CalculateBalanceScore(teams);

                            if (newScore < currentScore - ImprovementThreshold)
                            {
                                // Keep the swap
                                currentScore = newScore;
                                improved = true;
                                break;
                            }
                            else
                            {
                                // Revert swap
                                teams[i].RemovePlayer(player2);
                                teams[j].RemovePlayer(player1);
                                teams[i].AddPlayer(player1);
                                teams[j].AddPlayer(player2);
                            }
                        }

                        if (improved) break;
                    }

                    if (improved) break;
                }

                if (improved) break;
            }
        }

        return teams;
    }
}
