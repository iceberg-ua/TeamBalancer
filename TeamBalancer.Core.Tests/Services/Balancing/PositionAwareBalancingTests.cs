namespace TeamBalancer.Core.Tests.Services.Balancing;

using TeamBalancer.Core.Models;
using TeamBalancer.Core.Services.Balancing;
using TeamBalancer.Core.Tests.TestSupport;

/// <summary>
/// Covers position-aware balancing (Phase 3) for <see cref="DraftStrategy"/>, the only
/// balancing strategy left. Every scenario runs with shuffle disabled so results are
/// deterministic.
/// </summary>
/// <remarks>
/// These scenarios used to run as theories across SnakeDraftStrategy and IterativeSwapStrategy.
/// Both are gone, so each one is now a single case against DraftStrategy; the behaviour being
/// pinned down is unchanged. Assertions that depended on a strategy having no refinement phase
/// are made against <see cref="SeedProbe"/> - DraftStrategy's phase A - rather than against the
/// finished teams, and are called out where that applies.
/// </remarks>
public class PositionAwareBalancingTests
{
    private static TeamBalancingService CreateService() => new(new DraftStrategy());

    // ---------------------------------------------------------------------
    // Regression: pools with no position data must behave as they did pre-Phase-3
    // ---------------------------------------------------------------------

    [Fact]
    public void PositionImbalance_PoolWithoutPositions_ContributesNothingToScore()
    {
        var players = TestPlayers.CreateMany(8, Position.Unspecified, "Flex");
        var teams = new DraftStrategy().BalanceTeams(players, 2);

        Assert.Equal(0, new ScoreProbe().PositionImbalance(teams));
    }

    /// <summary>
    /// The classic pre-position-aware behaviour - sort everyone by skill, then deal
    /// A, B, B, A, A, B - is a property of the draft itself, so it is asserted on the seed.
    /// The refinement pass that follows is free to improve on that split, and on this pool it
    /// does; see <see cref="Refinement_ImprovesASeedThatIsNotAlreadyOptimal"/>, which starts
    /// from exactly this arrangement.
    /// </summary>
    [Fact]
    public void PoolWithoutPositions_SeedsInTheClassicSnakeOrder()
    {
        var strongest = TestPlayers.Create("P1", speed: 3, technical: 3, stamina: 3);
        var second = TestPlayers.Create("P2", speed: 3, technical: 3, stamina: 2);
        var third = TestPlayers.Create("P3", speed: 3, technical: 2, stamina: 2);
        var fourth = TestPlayers.Create("P4", speed: 2, technical: 2, stamina: 2);
        var fifth = TestPlayers.Create("P5", speed: 2, technical: 2, stamina: 1);
        var weakest = TestPlayers.Create("P6", speed: 1, technical: 1, stamina: 1);

        var players = new List<Player> { third, weakest, strongest, fifth, second, fourth };

        var teams = new SeedProbe().Seed(players, 2);

        Assert.Equal([strongest, fourth, fifth], teams[0].Players);
        Assert.Equal([second, third, weakest], teams[1].Players);
    }

    [Fact]
    public void PoolWithoutPositions_ReportsEveryTeamMissingAGoalkeeper()
    {
        var players = TestPlayers.CreateMany(8, Position.Unspecified, "Flex");

        var teams = new DraftStrategy().BalanceTeams(players, 2);
        var stats = CreateService().GetTeamStatistics(teams);

        Assert.Equal(2, stats["TeamsWithoutGoalkeeper"]);
        TeamAssertions.AssertPlayersConserved(players, teams);
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

    [Fact]
    public void BalancedPool_GivesEachTeamExactlyOneGoalkeeper()
    {
        var players = BalancedPool();

        var teams = new DraftStrategy().BalanceTeams(players, 2);

        Assert.All(teams, t => Assert.Equal(1, t.CountAt(Position.Goalkeeper)));
        TeamAssertions.AssertPlayersConserved(players, teams);
    }

    [Theory]
    [InlineData(Position.Defender)]
    [InlineData(Position.Midfielder)]
    [InlineData(Position.Forward)]
    public void BalancedPool_SpreadsOutfieldPositionsWithinOnePlayer(Position position)
    {
        var players = BalancedPool();

        var teams = new DraftStrategy().BalanceTeams(players, 2);
        var counts = teams.Select(t => t.CountAt(position)).ToList();

        Assert.True(counts.Max() - counts.Min() <= 1,
            $"{position} counts were [{string.Join(", ", counts)}].");
    }

    // ---------------------------------------------------------------------
    // Goalkeeper scarcity, absence and surplus
    // ---------------------------------------------------------------------

    [Fact]
    public void SingleGoalkeeper_LeavesExactlyOneTeamWithoutOne()
    {
        List<Player> players =
        [
            .. TestPlayers.CreateMany(1, Position.Goalkeeper, "GK"),
            .. TestPlayers.CreateMany(2, Position.Defender, "DEF"),
            .. TestPlayers.CreateMany(2, Position.Midfielder, "MID"),
            .. TestPlayers.CreateMany(1, Position.Forward, "FWD")
        ];

        var teams = new DraftStrategy().BalanceTeams(players, 2);
        var stats = CreateService().GetTeamStatistics(teams);

        Assert.Equal(1, teams.Count(t => t.CountAt(Position.Goalkeeper) == 1));
        Assert.Equal(1, teams.Count(t => t.CountAt(Position.Goalkeeper) == 0));
        Assert.Equal(1, stats["TeamsWithoutGoalkeeper"]);
        TeamAssertions.AssertPlayersConserved(players, teams);
    }

    [Fact]
    public void NoGoalkeepers_StillProducesTeams()
    {
        List<Player> players =
        [
            .. TestPlayers.CreateMany(3, Position.Defender, "DEF"),
            .. TestPlayers.CreateMany(3, Position.Midfielder, "MID"),
            .. TestPlayers.CreateMany(2, Position.Forward, "FWD")
        ];

        var teams = new DraftStrategy().BalanceTeams(players, 2);
        var stats = CreateService().GetTeamStatistics(teams);

        Assert.Equal(2, teams.Count);
        Assert.Equal(2, stats["TeamsWithoutGoalkeeper"]);
        TeamAssertions.AssertPlayersConserved(players, teams);
    }

    /// <summary>
    /// DIVERGENCE FROM THE PHASE 4 SPEC. The spec asks this scenario to assert "no team gets
    /// more than 1 GK" for 3 goalkeepers across 2 teams — which the pigeonhole principle makes
    /// impossible, since three GK-primary players cannot occupy two teams one apiece. What the
    /// design actually promises is that only the first `numberOfTeams` keepers are treated as
    /// keepers and the surplus is drafted as an ordinary outfield player. That is what this
    /// test pins down: every team is covered, nobody is dropped, and the extra keeper does not
    /// pile onto a single team.
    /// </summary>
    [Fact]
    public void SurplusGoalkeepers_AreAllPlacedAndSpreadAcrossTeams()
    {
        List<Player> players =
        [
            .. TestPlayers.CreateMany(3, Position.Goalkeeper, "GK"),
            .. TestPlayers.CreateMany(3, Position.Defender, "DEF"),
            .. TestPlayers.CreateMany(2, Position.Midfielder, "MID"),
            .. TestPlayers.CreateMany(2, Position.Forward, "FWD")
        ];

        var teams = new DraftStrategy().BalanceTeams(players, 2);
        var stats = CreateService().GetTeamStatistics(teams);
        var keeperCounts = teams.Select(t => t.CountAt(Position.Goalkeeper)).ToList();

        Assert.Equal(0, stats["TeamsWithoutGoalkeeper"]);
        Assert.Equal(3, keeperCounts.Sum());
        Assert.True(keeperCounts.Max() - keeperCounts.Min() <= 1,
            $"Goalkeeper counts were [{string.Join(", ", keeperCounts)}].");
        TeamAssertions.AssertPlayersConserved(players, teams);
    }

    // ---------------------------------------------------------------------
    // Degenerate pools
    // ---------------------------------------------------------------------

    [Fact]
    public void HeavilySkewedPool_TerminatesAndKeepsEveryPlayer()
    {
        List<Player> players =
        [
            .. TestPlayers.CreateMany(10, Position.Forward, "FWD"),
            .. TestPlayers.CreateMany(1, Position.Goalkeeper, "GK"),
            .. TestPlayers.CreateMany(1, Position.Defender, "DEF"),
            .. TestPlayers.CreateMany(1, Position.Midfielder, "MID")
        ];

        var teams = new DraftStrategy().BalanceTeams(players, 2);

        Assert.Equal(13, teams.Sum(t => t.PlayerCount));
        TeamAssertions.AssertPlayersConserved(players, teams);
    }

    [Fact]
    public void SinglePositionPool_TerminatesAndKeepsEveryPlayer()
    {
        var players = TestPlayers.CreateMany(9, Position.Forward, "FWD");

        var teams = new DraftStrategy().BalanceTeams(players, 3);

        Assert.Equal(3, teams.Count);
        TeamAssertions.AssertPlayersConserved(players, teams);
    }

    // ---------------------------------------------------------------------
    // Refinement never loses ground on the plain draft
    // ---------------------------------------------------------------------

    /// <summary>
    /// The seed is a plain position-group snake draft with no refinement on top, so this is the
    /// guard against phase B being skipped, short-circuited or turned into a pessimisation:
    /// whatever the refinement returns must score at least as well as the draft it started from.
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Refinement_NeverScoresWorseThanThePlainDraft(int numberOfTeams)
    {
        var players = MixedSkillPool();
        var probe = new SeedProbe();

        double plainDraftScore = probe.CalculateBalanceScore(probe.Seed(players, numberOfTeams));
        double refinedScore = probe.CalculateBalanceScore(
            new DraftStrategy().BalanceTeams(players, numberOfTeams));

        Assert.True(refinedScore <= plainDraftScore,
            $"Refinement scored {refinedScore} against a plain draft's {plainDraftScore} " +
            $"over {numberOfTeams} teams.");
    }

    /// <summary>
    /// The other half of the guard: a pool whose plain draft is provably not optimal, so a
    /// refinement pass that silently stopped running would show up as an equal score instead of
    /// a better one.
    /// </summary>
    [Fact]
    public void Refinement_ImprovesASeedThatIsNotAlreadyOptimal()
    {
        // The classic snake split of this pool hands Team A the strongest player and leaves the
        // teams uneven on technical skill and stamina; trading the top two players evens both.
        List<Player> players =
        [
            TestPlayers.Create("P1", speed: 3, technical: 3, stamina: 3),
            TestPlayers.Create("P2", speed: 3, technical: 3, stamina: 2),
            TestPlayers.Create("P3", speed: 3, technical: 2, stamina: 2),
            TestPlayers.Create("P4", speed: 2, technical: 2, stamina: 2),
            TestPlayers.Create("P5", speed: 2, technical: 2, stamina: 1),
            TestPlayers.Create("P6", speed: 1, technical: 1, stamina: 1)
        ];

        var probe = new SeedProbe();

        double plainDraftScore = probe.CalculateBalanceScore(probe.Seed(players, 2));
        double refinedScore = probe.CalculateBalanceScore(new DraftStrategy().BalanceTeams(players, 2));

        Assert.True(refinedScore < plainDraftScore,
            $"Refinement left the plain draft's {plainDraftScore} untouched at {refinedScore}; " +
            "phase B may not be running.");
    }

    private static List<Player> MixedSkillPool()
    {
        return
        [
            TestPlayers.Create("GK1", Position.Goalkeeper, speed: 3, technical: 1, stamina: 2),
            TestPlayers.Create("GK2", Position.Goalkeeper, speed: 1, technical: 3, stamina: 1),
            TestPlayers.Create("GK3", Position.Goalkeeper, speed: 2, technical: 2, stamina: 3),
            TestPlayers.Create("GK4", Position.Goalkeeper, speed: 1, technical: 1, stamina: 2),
            TestPlayers.Create("DEF1", Position.Defender, speed: 3, technical: 3, stamina: 3),
            TestPlayers.Create("DEF2", Position.Defender, speed: 1, technical: 2, stamina: 1),
            TestPlayers.Create("DEF3", Position.Defender, speed: 2, technical: 1, stamina: 3),
            TestPlayers.Create("DEF4", Position.Defender, speed: 2, technical: 2, stamina: 2),
            TestPlayers.Create("MID1", Position.Midfielder, speed: 3, technical: 2, stamina: 1),
            TestPlayers.Create("MID2", Position.Midfielder, speed: 1, technical: 1, stamina: 1),
            TestPlayers.Create("MID3", Position.Midfielder, speed: 2, technical: 3, stamina: 2),
            TestPlayers.Create("MID4", Position.Midfielder, speed: 3, technical: 3, stamina: 1),
            TestPlayers.Create("FWD1", Position.Forward, speed: 3, technical: 3, stamina: 1),
            TestPlayers.Create("FWD2", Position.Forward, speed: 1, technical: 1, stamina: 3),
            TestPlayers.Create("FWD3", Position.Forward, speed: 2, technical: 1, stamina: 1),
            TestPlayers.Create("FLEX1", Position.Unspecified, speed: 2, technical: 2, stamina: 2),
            TestPlayers.Create("FLEX2", Position.Unspecified, speed: 3, technical: 1, stamina: 1)
        ];
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

        var strategy = new DraftStrategy();

        // Sanity: the two splits are indistinguishable on skill.
        Assert.Equal(balanced.Sum(t => t.TotalSkillPoints), skewed.Sum(t => t.TotalSkillPoints), 10);
        Assert.Equal(balanced[0].OverallTeamSkill, skewed[0].OverallTeamSkill, 10);

        Assert.True(strategy.CalculateBalanceScore(skewed) > strategy.CalculateBalanceScore(balanced),
            "A positionally skewed split must score strictly worse than a balanced one.");
    }

    [Fact]
    public void CalculateBalanceScore_GoalkeepersAreNotScored()
    {
        // Goalkeeper cover is a hard constraint in the strategy, not a scored term, so an
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
