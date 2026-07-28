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
    /// Maximum number of improvement passes made by <see cref="ImproveByPairwiseSwaps"/>
    /// before it gives up, so a pathological pool cannot loop indefinitely.
    /// </summary>
    protected const int MaxIterations = 1000;

    /// <summary>
    /// Minimum drop in balance score required for a swap to count as an improvement.
    /// Guards against churn from floating point noise.
    /// </summary>
    protected const double ImprovementThreshold = 0.0001;

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

        // Skill is compared as the strength a team actually puts on the pitch - its total,
        // divided by the average squad size so the numbers stay on a per-player scale.
        //
        // Where every team is the same size this is identical to the plain per-player average
        // (total / n / (n / n) == total / n), so evenly split pools score exactly as before.
        // It only differs when sizes are forced apart, and there the average is actively wrong:
        // across three players it rates a lone weak player against a strong-plus-weak pair
        // (1.0 vs 2.0) as closer than a lone strong player against two weak ones (3.0 vs 1.0),
        // even though the first is a 1-against-4 mismatch and the second is 3-against-2. Totals
        // rank those the right way round.
        double meanTeamSize = teams.Average(t => (double)t.PlayerCount);

        if (meanTeamSize <= 0)
        {
            return 0;
        }

        var overallSkills = teams.Select(t => t.TotalSkillPoints / meanTeamSize).ToList();
        double overallVariance = CalculateVariance(overallSkills);

        // Calculate variance in individual attributes, on the same per-roster-slot scale
        var speedTotals = teams.Select(t => t.Players.Sum(p => (double)p.Speed) / meanTeamSize).ToList();
        var techTotals = teams.Select(t => t.Players.Sum(p => (double)p.TechnicalSkills) / meanTeamSize).ToList();
        var staminaTotals = teams.Select(t => t.Players.Sum(p => (double)p.Stamina) / meanTeamSize).ToList();

        double speedVariance = CalculateVariance(speedTotals);
        double techVariance = CalculateVariance(techTotals);
        double staminaVariance = CalculateVariance(staminaTotals);

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
    /// Iteratively improves an existing distribution by swapping single players between
    /// pairs of teams, in place. A swap is kept only when it lowers
    /// <see cref="CalculateBalanceScore"/> by more than <see cref="ImprovementThreshold"/>
    /// and leaves no more teams without a goalkeeper than before; anything else is reverted.
    /// Goalkeeper coverage is a hard constraint rather than a scored term, so it is tracked
    /// separately: a swap may improve it or leave it alone, never worsen it.
    /// Each accepted swap restarts the search, which stops once a full pass finds no
    /// improvement or after <see cref="MaxIterations"/> passes.
    /// </summary>
    /// <param name="teams">The teams to improve. Modified in place.</param>
    /// <param name="random">
    /// When supplied, the team pairs and the players within them are visited in random order,
    /// so that equally good swaps are picked between at random instead of always resolving to
    /// the lowest team and player index. The set of acceptable swaps is unchanged either way.
    /// Pass null for a fully deterministic search.
    /// </param>
    protected void ImproveByPairwiseSwaps(List<Team> teams, Random? random = null)
    {
        double currentScore = CalculateBalanceScore(teams);
        int teamsWithoutGoalkeeper = CountTeamsWithoutGoalkeeper(teams);
        bool improved = true;
        int iterations = 0;

        while (improved && iterations < MaxIterations)
        {
            improved = false;
            iterations++;

            // Try swapping players between all pairs of teams
            foreach (var (i, j) in BuildTeamPairs(teams.Count, random))
            {
                // Try swapping each player from team i with each player from team j
                foreach (var player1 in SwapCandidates(teams[i], random))
                {
                    foreach (var player2 in SwapCandidates(teams[j], random))
                    {
                        // Perform swap
                        teams[i].RemovePlayer(player1);
                        teams[j].RemovePlayer(player2);
                        teams[i].AddPlayer(player2);
                        teams[j].AddPlayer(player1);

                        // Check if this improved balance without costing goalkeeper cover
                        double newScore = CalculateBalanceScore(teams);
                        int newTeamsWithoutGoalkeeper = CountTeamsWithoutGoalkeeper(teams);

                        if (newTeamsWithoutGoalkeeper <= teamsWithoutGoalkeeper &&
                            newScore < currentScore - ImprovementThreshold)
                        {
                            // Keep the swap
                            currentScore = newScore;
                            teamsWithoutGoalkeeper = newTeamsWithoutGoalkeeper;
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
        }
    }

    /// <summary>
    /// Builds every unordered pair of team indices. Without a <paramref name="random"/> the
    /// pairs come back in ascending order - (0,1), (0,2), (1,2)... - which is the order the
    /// nested loops used to visit them in.
    /// </summary>
    private static List<(int First, int Second)> BuildTeamPairs(int numberOfTeams, Random? random)
    {
        var pairs = new List<(int, int)>();

        for (int i = 0; i < numberOfTeams - 1; i++)
        {
            for (int j = i + 1; j < numberOfTeams; j++)
            {
                pairs.Add((i, j));
            }
        }

        return random is null ? pairs : pairs.OrderBy(_ => random.Next()).ToList();
    }

    /// <summary>
    /// Snapshots a team's players for swap testing, so the list can be mutated while it is
    /// being walked. Without a <paramref name="random"/> the snapshot keeps the team's own order.
    /// </summary>
    private static List<Player> SwapCandidates(Team team, Random? random)
    {
        return random is null
            ? team.Players.ToList()
            : team.Players.OrderBy(_ => random.Next()).ToList();
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
