using TeamBalancer.Core.Models;

namespace TeamBalancer.Core.Services.Balancing;

/// <summary>
/// Service for balancing players into teams using various strategies.
/// </summary>
public class TeamBalancingService
{
    private readonly ITeamBalancingStrategy _defaultStrategy;

    /// <summary>
    /// Initializes a new instance of the TeamBalancingService.
    /// </summary>
    /// <param name="defaultStrategy">The default balancing strategy to use.</param>
    public TeamBalancingService(ITeamBalancingStrategy defaultStrategy)
    {
        _defaultStrategy = defaultStrategy ?? throw new ArgumentNullException(nameof(defaultStrategy));
    }

    /// <summary>
    /// Balances players into teams using the default strategy.
    /// </summary>
    /// <param name="players">The list of players to balance.</param>
    /// <param name="numberOfTeams">The number of teams to create.</param>
    /// <param name="shuffle">Whether to shuffle players for variety while maintaining balance.</param>
    /// <returns>A list of balanced teams.</returns>
    public List<Team> BalanceTeams(List<Player> players, int numberOfTeams, bool shuffle = false)
    {
        return BalanceTeams(players, numberOfTeams, _defaultStrategy, shuffle);
    }

    /// <summary>
    /// Balances players into teams using a specific strategy.
    /// </summary>
    /// <param name="players">The list of players to balance.</param>
    /// <param name="numberOfTeams">The number of teams to create.</param>
    /// <param name="strategy">The balancing strategy to use.</param>
    /// <param name="shuffle">Whether to shuffle players for variety while maintaining balance.</param>
    /// <returns>A list of balanced teams.</returns>
    public List<Team> BalanceTeams(List<Player> players, int numberOfTeams, ITeamBalancingStrategy strategy, bool shuffle = false)
    {
        if (players == null || players.Count == 0)
        {
            throw new ArgumentException("Player list cannot be null or empty.", nameof(players));
        }

        if (numberOfTeams < 2)
        {
            throw new ArgumentException("Number of teams must be at least 2.", nameof(numberOfTeams));
        }

        if (strategy == null)
        {
            throw new ArgumentNullException(nameof(strategy));
        }

        return strategy.BalanceTeams(players, numberOfTeams, shuffle);
    }

    /// <summary>
    /// Calculates how balanced a set of teams is.
    /// </summary>
    /// <param name="teams">The teams to evaluate.</param>
    /// <returns>A balance score (lower is better, 0 = perfectly balanced).</returns>
    public double CalculateBalanceScore(List<Team> teams)
    {
        return _defaultStrategy.CalculateBalanceScore(teams);
    }

    /// <summary>
    /// Gets team statistics for display purposes.
    /// </summary>
    /// <param name="teams">The teams to get statistics for.</param>
    /// <returns>A dictionary of team statistics.</returns>
    public Dictionary<string, object> GetTeamStatistics(List<Team> teams)
    {
        if (teams == null || teams.Count == 0)
        {
            return new Dictionary<string, object>();
        }

        var stats = new Dictionary<string, object>
        {
            ["TotalPlayers"] = teams.Sum(t => t.PlayerCount),
            ["NumberOfTeams"] = teams.Count,
            ["AveragePlayersPerTeam"] = teams.Average(t => t.PlayerCount),
            ["MinPlayersInTeam"] = teams.Min(t => t.PlayerCount),
            ["MaxPlayersInTeam"] = teams.Max(t => t.PlayerCount),
            ["BalanceScore"] = CalculateBalanceScore(teams),
            ["OverallSkillRange"] = teams.Max(t => t.OverallTeamSkill) - teams.Min(t => t.OverallTeamSkill),
            ["SpeedRange"] = teams.Max(t => t.AverageSpeed) - teams.Min(t => t.AverageSpeed),
            ["TechnicalRange"] = teams.Max(t => t.AverageTechnicalSkills) - teams.Min(t => t.AverageTechnicalSkills),
            ["StaminaRange"] = teams.Max(t => t.AverageStamina) - teams.Min(t => t.AverageStamina)
        };

        return stats;
    }
}
