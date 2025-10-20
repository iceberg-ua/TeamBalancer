using TeamBalancer.Core.Models;

namespace TeamBalancer.Core.Services.Balancing;

/// <summary>
/// Interface for team balancing strategies.
/// </summary>
public interface ITeamBalancingStrategy
{
    /// <summary>
    /// Balances a list of players into the specified number of teams.
    /// </summary>
    /// <param name="players">The list of players to balance.</param>
    /// <param name="numberOfTeams">The number of teams to create.</param>
    /// <returns>A list of balanced teams.</returns>
    List<Team> BalanceTeams(List<Player> players, int numberOfTeams);

    /// <summary>
    /// Calculates a balance score for the given teams (lower is better).
    /// The score represents how balanced the teams are.
    /// </summary>
    /// <param name="teams">The teams to evaluate.</param>
    /// <returns>A balance score (0 = perfectly balanced).</returns>
    double CalculateBalanceScore(List<Team> teams);
}
