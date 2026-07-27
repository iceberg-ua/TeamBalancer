namespace TeamBalancer.Core.Tests.TestSupport;

using TeamBalancer.Core.Models;
using TeamBalancer.Core.Services.Balancing;

/// <summary>
/// Exposes <see cref="DraftStrategy"/>'s phase A on its own, so the constructive seeding can be
/// asserted on before the refinement pass gets a chance to move anyone. A plain seed is also
/// exactly what "a position-group snake draft with no refinement" means, which is the baseline
/// the refinement is measured against.
/// </summary>
internal sealed class SeedProbe : DraftStrategy
{
    public List<Team> Seed(List<Player> players, int numberOfTeams, bool shuffle = false)
        => SeedTeams(players, numberOfTeams, shuffle);
}

/// <summary>
/// Exposes the protected scoring internals of <see cref="BaseTeamBalancingStrategy"/> so the
/// position term can be asserted on directly.
/// </summary>
internal sealed class ScoreProbe : BaseTeamBalancingStrategy
{
    public override List<Team> BalanceTeams(List<Player> players, int numberOfTeams, bool shuffle = false)
        => throw new NotSupportedException("The probe only scores existing teams.");

    public double PositionImbalance(List<Team> teams) => CalculatePositionImbalance(teams);
}

/// <summary>
/// Assertions shared by the balancing test suites.
/// </summary>
internal static class TeamAssertions
{
    /// <summary>
    /// Asserts every input player appears exactly once across the teams.
    /// </summary>
    public static void AssertPlayersConserved(List<Player> input, List<Team> teams)
    {
        var placed = teams.SelectMany(t => t.Players).ToList();

        Assert.Equal(input.Count, placed.Count);
        Assert.Equal(input.Count, placed.Distinct().Count());
        Assert.All(input, p => Assert.Contains(p, placed));
    }
}
