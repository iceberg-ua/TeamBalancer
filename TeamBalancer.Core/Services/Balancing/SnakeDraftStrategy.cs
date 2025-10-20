using TeamBalancer.Core.Models;

namespace TeamBalancer.Core.Services.Balancing;

/// <summary>
/// Implements a snake draft (greedy) team balancing strategy.
/// Players are sorted by skill level and distributed in a snake pattern:
/// Team A, Team B, Team B, Team A, Team A, Team B, etc.
/// </summary>
public class SnakeDraftStrategy : ITeamBalancingStrategy
{
    /// <summary>
    /// Balances players using a snake draft approach.
    /// </summary>
    public List<Team> BalanceTeams(List<Player> players, int numberOfTeams)
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

        // Distribute players using snake draft pattern
        int currentTeamIndex = 0;
        bool forward = true;

        foreach (var player in sortedPlayers)
        {
            teams[currentTeamIndex].AddPlayer(player);

            // Move to next team in snake pattern
            if (forward)
            {
                currentTeamIndex++;
                if (currentTeamIndex >= numberOfTeams)
                {
                    currentTeamIndex = numberOfTeams - 1;
                    forward = false;
                }
            }
            else
            {
                currentTeamIndex--;
                if (currentTeamIndex < 0)
                {
                    currentTeamIndex = 0;
                    forward = true;
                }
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
