namespace TeamBalancer.Core.Tests.Services.Balancing;

using TeamBalancer.Core.Models;
using TeamBalancer.Core.Services.Balancing;
using TeamBalancer.Core.Tests.TestSupport;

/// <summary>
/// Covers <see cref="DraftStrategy"/>: the constructive seeding of phase A, the refinement of
/// phase B, and the shuffle behaviour that spans both. Scenarios that pin down an exact
/// distribution run with shuffle disabled so the draft is deterministic.
/// </summary>
public class DraftStrategyTests
{
    private static void AssertPlayersConserved(List<Player> input, List<Team> teams)
        => TeamAssertions.AssertPlayersConserved(input, teams);

    // ---------------------------------------------------------------------
    // Argument validation, matching the other strategies
    // ---------------------------------------------------------------------

    [Fact]
    public void BalanceTeams_EmptyPool_Throws()
    {
        Assert.Throws<ArgumentException>(() => new DraftStrategy().BalanceTeams([], 2));
    }

    [Fact]
    public void BalanceTeams_FewerThanTwoTeams_Throws()
    {
        var players = TestPlayers.CreateMany(4, Position.Defender, "DEF");

        Assert.Throws<ArgumentException>(() => new DraftStrategy().BalanceTeams(players, 1));
    }

    // ---------------------------------------------------------------------
    // Phase A, step 1: goalkeepers
    // ---------------------------------------------------------------------

    [Fact]
    public void Goalkeepers_OnePerTeamWhenSupplyMatches()
    {
        List<Player> players =
        [
            .. TestPlayers.CreateMany(3, Position.Goalkeeper, "GK"),
            .. TestPlayers.CreateMany(3, Position.Defender, "DEF"),
            .. TestPlayers.CreateMany(3, Position.Midfielder, "MID"),
            .. TestPlayers.CreateMany(3, Position.Forward, "FWD")
        ];

        var teams = new DraftStrategy().BalanceTeams(players, 3);

        Assert.All(teams, t => Assert.Equal(1, t.CountAt(Position.Goalkeeper)));
        AssertPlayersConserved(players, teams);
    }

    [Fact]
    public void Goalkeepers_ShortSupply_LeavesTeamsWithoutOneInsteadOfThrowing()
    {
        List<Player> players =
        [
            .. TestPlayers.CreateMany(1, Position.Goalkeeper, "GK"),
            .. TestPlayers.CreateMany(3, Position.Defender, "DEF"),
            .. TestPlayers.CreateMany(2, Position.Midfielder, "MID")
        ];

        var teams = new DraftStrategy().BalanceTeams(players, 3);

        Assert.Equal(1, teams.Count(t => t.CountAt(Position.Goalkeeper) == 1));
        Assert.Equal(2, teams.Count(t => t.CountAt(Position.Goalkeeper) == 0));
        AssertPlayersConserved(players, teams);
    }

    [Fact]
    public void Goalkeepers_NoneAtAll_StillProducesTeams()
    {
        List<Player> players =
        [
            .. TestPlayers.CreateMany(3, Position.Defender, "DEF"),
            .. TestPlayers.CreateMany(3, Position.Midfielder, "MID")
        ];

        var teams = new DraftStrategy().BalanceTeams(players, 2);

        Assert.Equal(2, teams.Count);
        Assert.All(teams, t => Assert.Equal(0, t.CountAt(Position.Goalkeeper)));
        AssertPlayersConserved(players, teams);
    }

    [Fact]
    public void Goalkeepers_Surplus_RejoinsThePoolAsAnOrdinaryPlayer()
    {
        // Three keepers over two teams cannot go one apiece; the third is drafted as an
        // outfield player rather than being dropped or piled onto a covered team.
        List<Player> players =
        [
            .. TestPlayers.CreateMany(3, Position.Goalkeeper, "GK"),
            .. TestPlayers.CreateMany(2, Position.Defender, "DEF"),
            .. TestPlayers.CreateMany(2, Position.Midfielder, "MID"),
            .. TestPlayers.CreateMany(2, Position.Forward, "FWD")
        ];

        var teams = new DraftStrategy().BalanceTeams(players, 2);
        var keeperCounts = teams.Select(t => t.CountAt(Position.Goalkeeper)).ToList();

        Assert.All(teams, t => Assert.True(t.CountAt(Position.Goalkeeper) >= 1));
        Assert.Equal(3, keeperCounts.Sum());
        AssertPlayersConserved(players, teams);
    }

    // ---------------------------------------------------------------------
    // Phase A, step 2: snake order with the cursor carried across groups
    // ---------------------------------------------------------------------

    [Fact]
    public void Seeding_CarriesTheSnakeCursorAcrossPositionGroups()
    {
        // Two keepers use up picks A then B, leaving the cursor on B with the direction
        // reversed - so the strongest defender goes to B, not back to A.
        var keeper1 = TestPlayers.Create("GK1", Position.Goalkeeper, speed: 3, technical: 3, stamina: 3);
        var keeper2 = TestPlayers.Create("GK2", Position.Goalkeeper, speed: 2, technical: 2, stamina: 2);
        var def1 = TestPlayers.Create("DEF1", Position.Defender, speed: 3, technical: 3, stamina: 3);
        var def2 = TestPlayers.Create("DEF2", Position.Defender, speed: 2, technical: 2, stamina: 2);
        var def3 = TestPlayers.Create("DEF3", Position.Defender, speed: 1, technical: 1, stamina: 1);

        var teams = new SeedProbe().Seed([def3, keeper2, def1, keeper1, def2], 2);

        Assert.Equal([keeper1, def2, def3], teams[0].Players);
        Assert.Equal([keeper2, def1], teams[1].Players);
    }

    [Fact]
    public void Seeding_DraftsEachGroupStrongestFirst()
    {
        var strong = TestPlayers.Create("D-STR", Position.Defender, speed: 3, technical: 3, stamina: 3);
        var middling = TestPlayers.Create("D-MID", Position.Defender, speed: 2, technical: 2, stamina: 2);
        var weak = TestPlayers.Create("D-WEAK", Position.Defender, speed: 1, technical: 1, stamina: 1);

        var teams = new SeedProbe().Seed([middling, weak, strong], 2);

        // Snake: strongest to A, then B twice would exhaust the group - A, B, B.
        Assert.Equal([strong], teams[0].Players);
        Assert.Equal([middling, weak], teams[1].Players);
    }

    // ---------------------------------------------------------------------
    // Phase A, step 3: secondary position as a fill signal
    // ---------------------------------------------------------------------

    [Fact]
    public void Seeding_ShortGroup_PullsASecondaryMatchOutOfTheLeftoverPool()
    {
        // One real defender cannot cover two teams. The flex player who lists Defender as his
        // secondary fills the gap even though the other leftover is the stronger player.
        var def1 = TestPlayers.Create("DEF1", Position.Defender);
        var mid1 = TestPlayers.Create("MID1", Position.Midfielder);
        var mid2 = TestPlayers.Create("MID2", Position.Midfielder);
        var fwd1 = TestPlayers.Create("FWD1", Position.Forward);
        var fwd2 = TestPlayers.Create("FWD2", Position.Forward);
        var flexDefender = TestPlayers.Create("FLEX-D", Position.Unspecified, Position.Defender);
        var strongerFlex = TestPlayers.Create("FLEX", Position.Unspecified,
            speed: 3, technical: 3, stamina: 3);

        List<Player> players = [def1, mid1, mid2, fwd1, fwd2, flexDefender, strongerFlex];

        var teams = new SeedProbe().Seed(players, 2);

        // Defender group is [def1, flexDefender]; the unmatched flex player waits until last.
        Assert.Equal([def1, mid2, fwd1], teams[0].Players);
        Assert.Equal([flexDefender, mid1, fwd2, strongerFlex], teams[1].Players);
    }

    [Fact]
    public void Seeding_SeveralShortGroups_EachTakesItsOwnSecondaryMatchAheadOfPureFlex()
    {
        // Defenders and midfielders are both a player short. Each group takes the leftover who
        // lists its position as his secondary, and the flex player with no secondary at all is
        // left to the end - even though he is the strongest player in the pool.
        var def1 = TestPlayers.Create("DEF1", Position.Defender);
        var mid1 = TestPlayers.Create("MID1", Position.Midfielder);
        var fwd1 = TestPlayers.Create("FWD1", Position.Forward);
        var fwd2 = TestPlayers.Create("FWD2", Position.Forward);
        var flexDefender = TestPlayers.Create("FLEX-D", Position.Unspecified, Position.Defender);
        var flexMidfielder = TestPlayers.Create("FLEX-M", Position.Unspecified, Position.Midfielder);
        var pureFlex = TestPlayers.Create("FLEX", Position.Unspecified,
            speed: 3, technical: 3, stamina: 3);

        List<Player> players = [def1, mid1, fwd1, fwd2, flexDefender, flexMidfielder, pureFlex];

        var teams = new SeedProbe().Seed(players, 2);

        // Groups drafted as [def1, flexDefender], [mid1, flexMidfielder], [fwd1, fwd2],
        // then the leftover pool, which by now holds only the unmatched flex player.
        Assert.Equal([def1, flexMidfielder, fwd1], teams[0].Players);
        Assert.Equal([flexDefender, mid1, fwd2, pureFlex], teams[1].Players);
    }

    [Fact]
    public void Seeding_PrimaryMatchAlwaysOutranksSecondaryMatchWithinAGroup()
    {
        // The primary defender is the weakest player in the pool and still picks first in his
        // own group; a secondary match can only ever fill in behind him.
        var weakDefender = TestPlayers.Create("DEF", Position.Defender, speed: 1, technical: 1, stamina: 1);
        var strongFlex = TestPlayers.Create("FLEX-D", Position.Unspecified, Position.Defender,
            speed: 3, technical: 3, stamina: 3);

        var teams = new SeedProbe().Seed([strongFlex, weakDefender], 2);

        Assert.Equal([weakDefender], teams[0].Players);
        Assert.Equal([strongFlex], teams[1].Players);
    }

    [Fact]
    public void Seeding_GroupThatIsNotShort_IgnoresSecondaryMatches()
    {
        // Two defenders already cover two teams, so the flex player stays in the leftover pool
        // and is drafted last rather than being promoted into the defender group.
        var def1 = TestPlayers.Create("DEF1", Position.Defender);
        var def2 = TestPlayers.Create("DEF2", Position.Defender);
        var flexDefender = TestPlayers.Create("FLEX-D", Position.Unspecified, Position.Defender);

        var teams = new SeedProbe().Seed([def1, def2, flexDefender], 2);

        Assert.Equal([def1], teams[0].Players);
        Assert.Equal([def2, flexDefender], teams[1].Players);
    }

    [Fact]
    public void Seeding_SurplusGoalkeeperCanFillAShortGroupOnHisSecondaryPosition()
    {
        // A surplus keeper is an ordinary outfield-eligible player, so his secondary position
        // counts for fill just like any other leftover's.
        var keeper1 = TestPlayers.Create("GK1", Position.Goalkeeper, speed: 3, technical: 3, stamina: 3);
        var keeper2 = TestPlayers.Create("GK2", Position.Goalkeeper, speed: 2, technical: 2, stamina: 2);
        var surplusKeeper = TestPlayers.Create("GK3", Position.Goalkeeper, Position.Forward,
            speed: 1, technical: 1, stamina: 1);
        var fwd1 = TestPlayers.Create("FWD1", Position.Forward);

        var teams = new SeedProbe().Seed([keeper1, keeper2, surplusKeeper, fwd1], 2);

        // Keepers take A and B, the forward group is short so the surplus keeper fills it,
        // and the leftover pool ends up empty.
        Assert.Equal([keeper1, surplusKeeper], teams[0].Players);
        Assert.Equal([keeper2, fwd1], teams[1].Players);
    }

    // ---------------------------------------------------------------------
    // Phase B: bounded refinement
    // ---------------------------------------------------------------------

    /// <summary>
    /// Hill climbing has run to completion when no single pairwise swap is still acceptable -
    /// that is, none both lowers the score past the threshold and holds goalkeeper cover.
    /// This brute-forces every swap on the finished teams to prove the pass really ran.
    /// </summary>
    private static void AssertNoImprovingSwapRemains(DraftStrategy strategy, List<Team> teams)
    {
        double baseline = strategy.CalculateBalanceScore(teams);
        int baselineUncovered = CountUncovered(teams);

        for (int i = 0; i < teams.Count - 1; i++)
        {
            for (int j = i + 1; j < teams.Count; j++)
            {
                foreach (var player1 in teams[i].Players.ToList())
                {
                    foreach (var player2 in teams[j].Players.ToList())
                    {
                        teams[i].RemovePlayer(player1);
                        teams[j].RemovePlayer(player2);
                        teams[i].AddPlayer(player2);
                        teams[j].AddPlayer(player1);

                        double swapped = strategy.CalculateBalanceScore(teams);
                        int uncovered = CountUncovered(teams);

                        teams[i].RemovePlayer(player2);
                        teams[j].RemovePlayer(player1);
                        teams[i].AddPlayer(player1);
                        teams[j].AddPlayer(player2);

                        Assert.False(uncovered <= baselineUncovered && swapped < baseline - 0.0001,
                            $"Swapping {player1.Name} with {player2.Name} would still improve " +
                            $"the score from {baseline} to {swapped}.");
                    }
                }
            }
        }
    }

    private static int CountUncovered(List<Team> teams)
        => teams.Count(t => t.Players.All(p => p.PrimaryPosition != Position.Goalkeeper));

    [Fact]
    public void Refinement_LeavesNoImprovingSwapBehind()
    {
        // Uneven skills across positions, so the seed is unlikely to be locally optimal and
        // the refinement pass has real work to do.
        List<Player> players =
        [
            TestPlayers.Create("GK1", Position.Goalkeeper, speed: 3, technical: 1, stamina: 2),
            TestPlayers.Create("GK2", Position.Goalkeeper, speed: 1, technical: 3, stamina: 1),
            TestPlayers.Create("DEF1", Position.Defender, speed: 3, technical: 3, stamina: 3),
            TestPlayers.Create("DEF2", Position.Defender, speed: 1, technical: 2, stamina: 1),
            TestPlayers.Create("DEF3", Position.Defender, speed: 2, technical: 1, stamina: 3),
            TestPlayers.Create("MID1", Position.Midfielder, speed: 3, technical: 2, stamina: 1),
            TestPlayers.Create("MID2", Position.Midfielder, speed: 1, technical: 1, stamina: 1),
            TestPlayers.Create("MID3", Position.Midfielder, speed: 2, technical: 3, stamina: 2),
            TestPlayers.Create("FWD1", Position.Forward, speed: 3, technical: 3, stamina: 1),
            TestPlayers.Create("FWD2", Position.Forward, speed: 1, technical: 1, stamina: 3),
            TestPlayers.Create("FLEX1", Position.Unspecified, speed: 2, technical: 2, stamina: 2),
            TestPlayers.Create("FLEX2", Position.Unspecified, speed: 3, technical: 1, stamina: 1)
        ];

        var strategy = new DraftStrategy();
        var teams = strategy.BalanceTeams(players, 2);

        AssertNoImprovingSwapRemains(strategy, teams);
        AssertPlayersConserved(players, teams);
    }

    [Fact]
    public void Refinement_NeverCostsGoalkeeperCover()
    {
        // Skills are lopsided towards the keepers, so a swap that trades one away would look
        // attractive on score alone. The hard constraint has to veto it.
        List<Player> players =
        [
            TestPlayers.Create("GK1", Position.Goalkeeper, speed: 3, technical: 3, stamina: 3),
            TestPlayers.Create("GK2", Position.Goalkeeper, speed: 3, technical: 3, stamina: 3),
            .. TestPlayers.CreateMany(3, Position.Defender, "DEF", speed: 1, technical: 1, stamina: 1),
            .. TestPlayers.CreateMany(3, Position.Forward, "FWD", speed: 1, technical: 1, stamina: 1)
        ];

        var teams = new DraftStrategy().BalanceTeams(players, 2);

        Assert.All(teams, t => Assert.Equal(1, t.CountAt(Position.Goalkeeper)));
    }

    [Fact]
    public void Refinement_ScoresAtLeastAsWellAsTheSeedAlone()
    {
        List<Player> players =
        [
            TestPlayers.Create("GK1", Position.Goalkeeper, speed: 3, technical: 2, stamina: 1),
            TestPlayers.Create("GK2", Position.Goalkeeper, speed: 1, technical: 1, stamina: 2),
            TestPlayers.Create("DEF1", Position.Defender, speed: 3, technical: 3, stamina: 3),
            TestPlayers.Create("DEF2", Position.Defender, speed: 1, technical: 1, stamina: 1),
            TestPlayers.Create("MID1", Position.Midfielder, speed: 3, technical: 1, stamina: 2),
            TestPlayers.Create("MID2", Position.Midfielder, speed: 2, technical: 3, stamina: 1),
            TestPlayers.Create("FWD1", Position.Forward, speed: 3, technical: 3, stamina: 2),
            TestPlayers.Create("FWD2", Position.Forward, speed: 1, technical: 2, stamina: 1)
        ];

        var probe = new SeedProbe();
        double seedScore = probe.CalculateBalanceScore(probe.Seed(players, 2));
        double finalScore = probe.CalculateBalanceScore(new DraftStrategy().BalanceTeams(players, 2));

        Assert.True(finalScore <= seedScore,
            $"Refinement made the seed worse: {seedScore} became {finalScore}.");
    }

    // ---------------------------------------------------------------------
    // Shuffle
    //
    // The unshuffled arms of these degenerate pools live in PositionAwareBalancingTests; what
    // is left here is that turning shuffle on does not break them.
    // ---------------------------------------------------------------------

    [Fact]
    public void Shuffle_HeavilySkewedPool_TerminatesAndKeepsEveryPlayer()
    {
        List<Player> players =
        [
            .. TestPlayers.CreateMany(10, Position.Forward, "FWD"),
            .. TestPlayers.CreateMany(1, Position.Goalkeeper, "GK"),
            .. TestPlayers.CreateMany(1, Position.Defender, "DEF"),
            .. TestPlayers.CreateMany(1, Position.Midfielder, "MID")
        ];

        var teams = new DraftStrategy().BalanceTeams(players, 2, shuffle: true);

        Assert.Equal(13, teams.Sum(t => t.PlayerCount));
        AssertPlayersConserved(players, teams);
    }

    [Fact]
    public void Shuffle_SinglePositionPool_TerminatesAndKeepsEveryPlayer()
    {
        var players = TestPlayers.CreateMany(9, Position.Forward, "FWD");

        var teams = new DraftStrategy().BalanceTeams(players, 3, shuffle: true);

        Assert.Equal(3, teams.Count);
        AssertPlayersConserved(players, teams);
    }

    [Fact]
    public void Shuffle_KeepsGoalkeeperCoverAndEveryPlayerAcrossRepeatedRuns()
    {
        List<Player> players =
        [
            .. TestPlayers.CreateMany(2, Position.Goalkeeper, "GK"),
            .. TestPlayers.CreateMany(4, Position.Defender, "DEF"),
            .. TestPlayers.CreateMany(4, Position.Midfielder, "MID"),
            .. TestPlayers.CreateMany(4, Position.Forward, "FWD")
        ];

        var strategy = new DraftStrategy();

        for (int run = 0; run < 25; run++)
        {
            var teams = strategy.BalanceTeams(players, 2, shuffle: true);

            Assert.All(teams, t => Assert.Equal(1, t.CountAt(Position.Goalkeeper)));
            AssertPlayersConserved(players, teams);
        }
    }

    [Fact]
    public void Shuffle_ProducesVariedTeamsAcrossRuns()
    {
        // Distinct skills throughout, so a genuinely varied draft shows up as different team
        // compositions rather than the same split every time.
        var players = Enumerable.Range(1, 12)
            .Select(i => TestPlayers.Create(
                $"P{i}",
                Position.Defender,
                speed: (i % 3) + 1,
                technical: ((i + 1) % 3) + 1,
                stamina: ((i + 2) % 3) + 1))
            .ToList();

        var strategy = new DraftStrategy();

        var seen = new HashSet<string>();

        for (int run = 0; run < 25; run++)
        {
            var teams = strategy.BalanceTeams(players, 2, shuffle: true);
            seen.Add(string.Join("|", teams[0].Players.Select(p => p.Name).OrderBy(n => n)));
        }

        Assert.True(seen.Count > 1, "Shuffling produced the same split on every run.");
    }

    [Fact]
    public void NoShuffle_IsDeterministic()
    {
        List<Player> players =
        [
            .. TestPlayers.CreateMany(2, Position.Goalkeeper, "GK"),
            .. TestPlayers.CreateMany(3, Position.Defender, "DEF"),
            .. TestPlayers.CreateMany(3, Position.Midfielder, "MID"),
            .. TestPlayers.CreateMany(2, Position.Forward, "FWD")
        ];

        var first = new DraftStrategy().BalanceTeams(players, 2);
        var second = new DraftStrategy().BalanceTeams(players, 2);

        for (int i = 0; i < first.Count; i++)
        {
            Assert.Equal(first[i].Players, second[i].Players);
        }
    }
}
