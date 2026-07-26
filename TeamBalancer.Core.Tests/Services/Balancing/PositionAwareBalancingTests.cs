namespace TeamBalancer.Core.Tests.Services.Balancing;

using TeamBalancer.Core.Models;
using TeamBalancer.Core.Services.Balancing;
using TeamBalancer.Core.Tests.TestSupport;

/// <summary>
/// Covers position-aware balancing (Phase 3) for both strategies.
/// Every scenario runs with shuffle disabled so results are deterministic.
/// </summary>
public class PositionAwareBalancingTests
{
    /// <summary>
    /// Exposes the protected scoring internals of <see cref="BaseTeamBalancingStrategy"/>
    /// so the position term can be asserted on directly.
    /// </summary>
    private sealed class ScoreProbe : BaseTeamBalancingStrategy
    {
        public override List<Team> BalanceTeams(List<Player> players, int numberOfTeams, bool shuffle = false)
            => throw new NotSupportedException("The probe only scores existing teams.");

        public double PositionImbalance(List<Team> teams) => CalculatePositionImbalance(teams);
    }

    private static ITeamBalancingStrategy CreateStrategy(BalancingAlgorithmType type) => type switch
    {
        BalancingAlgorithmType.SnakeDraft => new SnakeDraftStrategy(),
        BalancingAlgorithmType.IterativeSwap => new IterativeSwapStrategy(),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static TeamBalancingService CreateService() => new(new SnakeDraftStrategy());

    /// <summary>
    /// Asserts every input player appears exactly once across the teams.
    /// </summary>
    private static void AssertPlayersConserved(List<Player> input, List<Team> teams)
    {
        var placed = teams.SelectMany(t => t.Players).ToList();

        Assert.Equal(input.Count, placed.Count);
        Assert.Equal(input.Count, placed.Distinct().Count());
        Assert.All(input, p => Assert.Contains(p, placed));
    }

    // ---------------------------------------------------------------------
    // Regression: pools with no position data must behave as they did pre-Phase-3
    // ---------------------------------------------------------------------

    [Fact]
    public void PositionImbalance_PoolWithoutPositions_ContributesNothingToScore()
    {
        var players = TestPlayers.CreateMany(8, Position.Unspecified, "Flex");
        var teams = new SnakeDraftStrategy().BalanceTeams(players, 2);

        Assert.Equal(0, new ScoreProbe().PositionImbalance(teams));
    }

    [Fact]
    public void SnakeDraft_PoolWithoutPositions_KeepsTheClassicSnakeOrder()
    {
        // Pre-Phase-3 behaviour: sort everyone by skill, then deal A, B, B, A, A, B.
        var strongest = TestPlayers.Create("P1", speed: 3, technical: 3, stamina: 3);
        var second = TestPlayers.Create("P2", speed: 3, technical: 3, stamina: 2);
        var third = TestPlayers.Create("P3", speed: 3, technical: 2, stamina: 2);
        var fourth = TestPlayers.Create("P4", speed: 2, technical: 2, stamina: 2);
        var fifth = TestPlayers.Create("P5", speed: 2, technical: 2, stamina: 1);
        var weakest = TestPlayers.Create("P6", speed: 1, technical: 1, stamina: 1);

        var players = new List<Player> { third, weakest, strongest, fifth, second, fourth };

        var teams = new SnakeDraftStrategy().BalanceTeams(players, 2);

        Assert.Equal([strongest, fourth, fifth], teams[0].Players);
        Assert.Equal([second, third, weakest], teams[1].Players);
    }

    [Theory]
    [InlineData(BalancingAlgorithmType.SnakeDraft)]
    [InlineData(BalancingAlgorithmType.IterativeSwap)]
    public void PoolWithoutPositions_ReportsEveryTeamMissingAGoalkeeper(BalancingAlgorithmType type)
    {
        var players = TestPlayers.CreateMany(8, Position.Unspecified, "Flex");

        var teams = CreateStrategy(type).BalanceTeams(players, 2);
        var stats = CreateService().GetTeamStatistics(teams);

        Assert.Equal(2, stats["TeamsWithoutGoalkeeper"]);
        AssertPlayersConserved(players, teams);
    }

    // ---------------------------------------------------------------------
    // Balanced pool
    // ---------------------------------------------------------------------

    private static List<Player> BalancedPool()
    {
        // Equal skills throughout, so only positional spread can move the score.
        return
        [
            .. TestPlayers.CreateMany(2, Position.Goalkeeper, "GK"),
            .. TestPlayers.CreateMany(4, Position.Defender, "DEF"),
            .. TestPlayers.CreateMany(4, Position.Midfielder, "MID"),
            .. TestPlayers.CreateMany(4, Position.Forward, "FWD")
        ];
    }

    [Theory]
    [InlineData(BalancingAlgorithmType.SnakeDraft)]
    [InlineData(BalancingAlgorithmType.IterativeSwap)]
    public void BalancedPool_GivesEachTeamExactlyOneGoalkeeper(BalancingAlgorithmType type)
    {
        var players = BalancedPool();

        var teams = CreateStrategy(type).BalanceTeams(players, 2);

        Assert.All(teams, t => Assert.Equal(1, t.CountAt(Position.Goalkeeper)));
        AssertPlayersConserved(players, teams);
    }

    [Theory]
    [InlineData(BalancingAlgorithmType.SnakeDraft, Position.Defender)]
    [InlineData(BalancingAlgorithmType.SnakeDraft, Position.Midfielder)]
    [InlineData(BalancingAlgorithmType.SnakeDraft, Position.Forward)]
    [InlineData(BalancingAlgorithmType.IterativeSwap, Position.Defender)]
    [InlineData(BalancingAlgorithmType.IterativeSwap, Position.Midfielder)]
    [InlineData(BalancingAlgorithmType.IterativeSwap, Position.Forward)]
    public void BalancedPool_SpreadsOutfieldPositionsWithinOnePlayer(BalancingAlgorithmType type, Position position)
    {
        var players = BalancedPool();

        var teams = CreateStrategy(type).BalanceTeams(players, 2);
        var counts = teams.Select(t => t.CountAt(position)).ToList();

        Assert.True(counts.Max() - counts.Min() <= 1,
            $"{position} counts were [{string.Join(", ", counts)}] for {type}.");
    }

    // ---------------------------------------------------------------------
    // Goalkeeper scarcity, absence and surplus
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(BalancingAlgorithmType.SnakeDraft)]
    [InlineData(BalancingAlgorithmType.IterativeSwap)]
    public void SingleGoalkeeper_LeavesExactlyOneTeamWithoutOne(BalancingAlgorithmType type)
    {
        List<Player> players =
        [
            .. TestPlayers.CreateMany(1, Position.Goalkeeper, "GK"),
            .. TestPlayers.CreateMany(2, Position.Defender, "DEF"),
            .. TestPlayers.CreateMany(2, Position.Midfielder, "MID"),
            .. TestPlayers.CreateMany(1, Position.Forward, "FWD")
        ];

        var teams = CreateStrategy(type).BalanceTeams(players, 2);
        var stats = CreateService().GetTeamStatistics(teams);

        Assert.Equal(1, teams.Count(t => t.CountAt(Position.Goalkeeper) == 1));
        Assert.Equal(1, teams.Count(t => t.CountAt(Position.Goalkeeper) == 0));
        Assert.Equal(1, stats["TeamsWithoutGoalkeeper"]);
        AssertPlayersConserved(players, teams);
    }

    [Theory]
    [InlineData(BalancingAlgorithmType.SnakeDraft)]
    [InlineData(BalancingAlgorithmType.IterativeSwap)]
    public void NoGoalkeepers_StillProducesTeams(BalancingAlgorithmType type)
    {
        List<Player> players =
        [
            .. TestPlayers.CreateMany(3, Position.Defender, "DEF"),
            .. TestPlayers.CreateMany(3, Position.Midfielder, "MID"),
            .. TestPlayers.CreateMany(2, Position.Forward, "FWD")
        ];

        var teams = CreateStrategy(type).BalanceTeams(players, 2);
        var stats = CreateService().GetTeamStatistics(teams);

        Assert.Equal(2, teams.Count);
        Assert.Equal(2, stats["TeamsWithoutGoalkeeper"]);
        AssertPlayersConserved(players, teams);
    }

    /// <summary>
    /// DIVERGENCE FROM THE PHASE 4 SPEC. The spec asks this scenario to assert "no team gets
    /// more than 1 GK" for 3 goalkeepers across 2 teams — which the pigeonhole principle makes
    /// impossible, since three GK-primary players cannot occupy two teams one apiece. What the
    /// Phase 3 design actually promises is that only the first `numberOfTeams` keepers are
    /// treated as keepers and the surplus is drafted as an ordinary outfield player. That is
    /// what this test pins down: every team is covered, nobody is dropped, and the extra keeper
    /// does not pile onto a single team.
    /// </summary>
    [Theory]
    [InlineData(BalancingAlgorithmType.SnakeDraft)]
    [InlineData(BalancingAlgorithmType.IterativeSwap)]
    public void SurplusGoalkeepers_AreAllPlacedAndSpreadAcrossTeams(BalancingAlgorithmType type)
    {
        List<Player> players =
        [
            .. TestPlayers.CreateMany(3, Position.Goalkeeper, "GK"),
            .. TestPlayers.CreateMany(3, Position.Defender, "DEF"),
            .. TestPlayers.CreateMany(2, Position.Midfielder, "MID"),
            .. TestPlayers.CreateMany(2, Position.Forward, "FWD")
        ];

        var teams = CreateStrategy(type).BalanceTeams(players, 2);
        var stats = CreateService().GetTeamStatistics(teams);
        var keeperCounts = teams.Select(t => t.CountAt(Position.Goalkeeper)).ToList();

        Assert.Equal(0, stats["TeamsWithoutGoalkeeper"]);
        Assert.Equal(3, keeperCounts.Sum());
        Assert.True(keeperCounts.Max() - keeperCounts.Min() <= 1,
            $"Goalkeeper counts were [{string.Join(", ", keeperCounts)}] for {type}.");
        AssertPlayersConserved(players, teams);
    }

    // ---------------------------------------------------------------------
    // Degenerate pools
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(BalancingAlgorithmType.SnakeDraft)]
    [InlineData(BalancingAlgorithmType.IterativeSwap)]
    public void HeavilySkewedPool_TerminatesAndKeepsEveryPlayer(BalancingAlgorithmType type)
    {
        List<Player> players =
        [
            .. TestPlayers.CreateMany(10, Position.Forward, "FWD"),
            .. TestPlayers.CreateMany(1, Position.Goalkeeper, "GK"),
            .. TestPlayers.CreateMany(1, Position.Defender, "DEF"),
            .. TestPlayers.CreateMany(1, Position.Midfielder, "MID")
        ];

        var teams = CreateStrategy(type).BalanceTeams(players, 2);

        Assert.Equal(13, teams.Sum(t => t.PlayerCount));
        AssertPlayersConserved(players, teams);
    }

    [Theory]
    [InlineData(BalancingAlgorithmType.SnakeDraft)]
    [InlineData(BalancingAlgorithmType.IterativeSwap)]
    public void SinglePositionPool_TerminatesAndKeepsEveryPlayer(BalancingAlgorithmType type)
    {
        var players = TestPlayers.CreateMany(9, Position.Forward, "FWD");

        var teams = CreateStrategy(type).BalanceTeams(players, 3);

        Assert.Equal(3, teams.Count);
        AssertPlayersConserved(players, teams);
    }

    // ---------------------------------------------------------------------
    // Scoring
    // ---------------------------------------------------------------------

    [Fact]
    public void CalculateBalanceScore_SkewedPositions_ScoreWorseThanBalancedAtEqualSkill()
    {
        // Both splits hold four identical players per team, so total and average skill match
        // exactly and only the positional spread differs.
        var balanced = new List<Team>
        {
            BuildTeam("Team A", Position.Defender, Position.Defender, Position.Forward, Position.Forward),
            BuildTeam("Team B", Position.Defender, Position.Defender, Position.Forward, Position.Forward)
        };

        var skewed = new List<Team>
        {
            BuildTeam("Team A", Position.Defender, Position.Defender, Position.Defender, Position.Defender),
            BuildTeam("Team B", Position.Forward, Position.Forward, Position.Forward, Position.Forward)
        };

        var strategy = new SnakeDraftStrategy();

        // Sanity: the two splits are indistinguishable on skill.
        Assert.Equal(balanced.Sum(t => t.TotalSkillPoints), skewed.Sum(t => t.TotalSkillPoints), 10);
        Assert.Equal(balanced[0].OverallTeamSkill, skewed[0].OverallTeamSkill, 10);

        Assert.True(strategy.CalculateBalanceScore(skewed) > strategy.CalculateBalanceScore(balanced),
            "A positionally skewed split must score strictly worse than a balanced one.");
    }

    [Fact]
    public void CalculateBalanceScore_GoalkeepersAreNotScored()
    {
        // Goalkeeper cover is a hard constraint in the strategies, not a scored term, so an
        // uneven keeper spread on its own must not change the score.
        var evenKeepers = new List<Team>
        {
            BuildTeam("Team A", Position.Goalkeeper, Position.Defender),
            BuildTeam("Team B", Position.Goalkeeper, Position.Defender)
        };

        var unevenKeepers = new List<Team>
        {
            BuildTeam("Team A", Position.Goalkeeper, Position.Goalkeeper),
            BuildTeam("Team B", Position.Defender, Position.Defender)
        };

        var probe = new ScoreProbe();

        Assert.Equal(0, probe.PositionImbalance(evenKeepers));
        // Only the two defenders clustering on one team registers here, not the keepers.
        Assert.True(probe.PositionImbalance(unevenKeepers) > 0);
    }

    private static Team BuildTeam(string name, params Position[] positions)
    {
        var team = new Team { Name = name };

        for (int i = 0; i < positions.Length; i++)
        {
            team.AddPlayer(TestPlayers.Create($"{name[^1]}{i}", positions[i]));
        }

        return team;
    }
}
