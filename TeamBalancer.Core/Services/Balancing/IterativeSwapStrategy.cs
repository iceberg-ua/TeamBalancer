using TeamBalancer.Core.Models;

namespace TeamBalancer.Core.Services.Balancing;

/// <summary>
/// Implements an iterative swap team balancing strategy.
/// Starts with an initial distribution and iteratively swaps players between teams
/// to minimize the balance score (variance in team skills).
/// This approach typically produces better balance than greedy methods.
/// </summary>
public class IterativeSwapStrategy : ITeamBalancingStrategy
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
    public List<Team> BalanceTeams(List<Player> players, int numberOfTeams, bool shuffle = false)
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

    /// <summary>
    /// Calculates balance score based on variance in overall skill levels.
    /// Also considers variance in individual attributes (Speed, Technical, Stamina).
    /// </summary>
    public double CalculateBalanceScore(List<Team> teams)
    {
        if (teams == null || teams.Count == 0)
        {
            return 0;
        }

        // Calculate variance in overall skill
        var overallSkills = teams.Select(t => t.OverallTeamSkill).ToList();
        double overallVariance = CalculateVariance(overallSkills);

        // Calculate variance in individual attributes
        var speedAvgs = teams.Select(t => t.AverageSpeed).ToList();
        var techAvgs = teams.Select(t => t.AverageTechnicalSkills).ToList();
        var staminaAvgs = teams.Select(t => t.AverageStamina).ToList();

        double speedVariance = CalculateVariance(speedAvgs);
        double techVariance = CalculateVariance(techAvgs);
        double staminaVariance = CalculateVariance(staminaAvgs);

        // Calculate variance in player counts
        var playerCounts = teams.Select(t => (double)t.PlayerCount).ToList();
        double countVariance = CalculateVariance(playerCounts);

        // Weighted sum of variances (overall skill is weighted more heavily)
        return (overallVariance * 2.0) + speedVariance + techVariance + staminaVariance + (countVariance * 1.5);
    }

    /// <summary>
    /// Calculates statistical variance for a list of values.
    /// </summary>
    private double CalculateVariance(List<double> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        double mean = values.Average();
        double sumOfSquares = values.Sum(v => Math.Pow(v - mean, 2));
        return sumOfSquares / values.Count;
    }
}
