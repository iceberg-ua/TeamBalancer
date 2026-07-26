using TeamBalancer.Core.Models;

namespace TeamBalancer.Core.Services.Balancing;

/// <summary>
/// Abstract base class for team balancing strategies.
/// Provides shared balance calculation logic to avoid code duplication.
/// </summary>
public abstract class BaseTeamBalancingStrategy : ITeamBalancingStrategy
{
    /// <summary>
    /// Weight factor for overall skill variance in balance score calculation.
    /// Higher weight means overall skill balance is more important.
    /// </summary>
    protected const double OverallSkillWeight = 2.0;

    /// <summary>
    /// Weight factor for player count variance in balance score calculation.
    /// Higher weight means equal team sizes are more important.
    /// </summary>
    protected const double PlayerCountWeight = 1.5;

    /// <summary>
    /// Weight factor for outfield position spread in balance score calculation.
    /// Deliberately lower than <see cref="OverallSkillWeight"/> so skill balance still
    /// dominates, but high enough that positional coverage breaks near-ties.
    /// </summary>
    protected const double PositionImbalanceWeight = 1.0;

    /// <summary>
    /// The positions scored as a soft preference. Goalkeeper is excluded because it is
    /// enforced as a hard constraint by the strategies themselves, and Unspecified is
    /// excluded because those players are treated as fully flexible.
    /// </summary>
    protected static readonly Position[] OutfieldPositions =
    [
        Position.Defender,
        Position.Midfielder,
        Position.Forward
    ];

    /// <summary>
    /// Balances a list of players into the specified number of teams.
    /// Must be implemented by derived classes.
    /// </summary>
    /// <param name="players">The list of players to balance.</param>
    /// <param name="numberOfTeams">The number of teams to create.</param>
    /// <param name="shuffle">Whether to shuffle players before balancing for variety.</param>
    /// <returns>A list of balanced teams.</returns>
    public abstract List<Team> BalanceTeams(List<Player> players, int numberOfTeams, bool shuffle = false);

    /// <summary>
    /// Calculates balance score based on variance in overall skill levels.
    /// Also considers variance in individual attributes (Speed, Technical, Stamina).
    /// Lower score means better balance (0 = perfectly balanced).
    /// </summary>
    /// <param name="teams">The teams to evaluate.</param>
    /// <returns>A balance score where lower is better.</returns>
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
        return (overallVariance * OverallSkillWeight) +
               speedVariance +
               techVariance +
               staminaVariance +
               (countVariance * PlayerCountWeight) +
               (CalculatePositionImbalance(teams) * PositionImbalanceWeight);
    }

    /// <summary>
    /// Calculates how unevenly the outfield positions are spread across teams, as the sum
    /// of the per-position variance in player counts. Returns 0 when no player has an
    /// outfield position set, so pools without position data score exactly as before.
    /// </summary>
    /// <param name="teams">The teams to evaluate.</param>
    /// <returns>A position imbalance score where lower is better.</returns>
    protected double CalculatePositionImbalance(List<Team> teams)
    {
        if (teams == null || teams.Count == 0)
        {
            return 0;
        }

        double total = 0;

        foreach (var position in OutfieldPositions)
        {
            var counts = teams
                .Select(t => (double)t.Players.Count(p => p.PrimaryPosition == position))
                .ToList();

            total += CalculateVariance(counts);
        }

        return total;
    }

    /// <summary>
    /// Counts the teams that have no goalkeeper. Used both to enforce the goalkeeper hard
    /// constraint during balancing and to report coverage once balancing completes.
    /// </summary>
    /// <param name="teams">The teams to evaluate.</param>
    /// <returns>The number of teams without a goalkeeper.</returns>
    protected static int CountTeamsWithoutGoalkeeper(List<Team> teams)
    {
        return teams.Count(t => t.Players.All(p => p.PrimaryPosition != Position.Goalkeeper));
    }

    /// <summary>
    /// Calculates statistical variance for a list of values.
    /// Variance measures how spread out the values are from their mean.
    /// </summary>
    /// <param name="values">The list of values to calculate variance for.</param>
    /// <returns>The variance of the values.</returns>
    protected double CalculateVariance(List<double> values)
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
