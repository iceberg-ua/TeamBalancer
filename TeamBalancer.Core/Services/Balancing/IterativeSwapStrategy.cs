using TeamBalancer.Core.Models;

namespace TeamBalancer.Core.Services.Balancing;

/// <summary>
/// Implements an iterative swap team balancing strategy.
/// Starts with an initial distribution and iteratively swaps players between teams
/// to minimize the balance score (variance in team skills and outfield positions).
/// Goalkeeper coverage is held as a hard constraint: the initial distribution deals one
/// keeper per team, and no swap that would leave more teams without one is accepted.
/// This approach typically produces better balance than greedy methods.
/// </summary>
/// <remarks>
/// NOT MAINTAINED. This strategy is not exposed in the app UI: MauiProgram registers only
/// <see cref="SnakeDraftStrategy"/> as the app's <see cref="ITeamBalancingStrategy"/>, and
/// there is no algorithm picker for end users. It is correct and tested as of version 1.2,
/// and is kept in the codebase in case algorithm selection is ever added.
/// <para>
/// By decision of the project owner it is frozen: future changes to balancing behaviour are
/// expected to land in <see cref="SnakeDraftStrategy"/> only, and this class may drift out of
/// sync with it. Do not treat the two as equivalent. Before exposing this strategy to users,
/// re-review it against whatever <see cref="SnakeDraftStrategy"/> has become.
/// </para>
/// </remarks>
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

        // Initial distribution using round-robin, seeded goalkeepers first so the round-robin
        // hands each team one before anyone else is placed.
        var playerList = BuildInitialOrder(players, numberOfTeams, shuffle);

        for (int i = 0; i < playerList.Count; i++)
        {
            teams[i % numberOfTeams].AddPlayer(playerList[i]);
        }

        // Iteratively improve balance by swapping players
        double currentScore = CalculateBalanceScore(teams);
        // Goalkeeper coverage is a hard constraint rather than a scored term, so it is
        // tracked separately: a swap may improve it or leave it alone, never worsen it.
        int teamsWithoutGoalkeeper = CountTeamsWithoutGoalkeeper(teams);
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

                if (improved) break;
            }
        }

        return teams;
    }

    /// <summary>
    /// Orders players for the initial round-robin: at most one goalkeeper per team first,
    /// so every team is dealt one before anyone else, then everyone else. Surplus
    /// goalkeepers are ordered with the rest and treated as ordinary players from there on.
    /// Only PrimaryPosition is considered; SecondaryPosition is intentionally unused in this
    /// phase.
    /// </summary>
    private List<Player> BuildInitialOrder(List<Player> players, int numberOfTeams, bool shuffle)
    {
        var keepers = Order(players.Where(p => p.PrimaryPosition == Position.Goalkeeper), shuffle)
            .Take(numberOfTeams)
            .ToList();

        var seeded = new HashSet<Player>(keepers);
        var rest = Order(players.Where(p => !seeded.Contains(p)), shuffle);

        return [.. keepers, .. rest];
    }

    /// <summary>
    /// Orders a set of players either randomly (for variety) or strongest first.
    /// </summary>
    private List<Player> Order(IEnumerable<Player> players, bool shuffle)
    {
        return shuffle
            ? players.OrderBy(_ => _random.Next()).ToList()
            : players.OrderByDescending(p => p.OverallSkillLevel).ToList();
    }
}
