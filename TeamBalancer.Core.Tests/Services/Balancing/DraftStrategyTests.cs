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
    // Uneven team sizes: the short-handed side carries the stronger players
    //
    // Balancing on team totals means a side that is a player down has to make the difference
    // up in quality. These pin that down, because the scoring is easy to break in a way that
    // still passes every test above: strength competes against three attribute-spread terms,
    // and if it is measured on a different scale from them it quietly stops winning.
    // ---------------------------------------------------------------------

    /// <summary>
    /// Seven players over two teams forces a 3 v 4. The pool splits exactly - A+B+C and
    /// D+E+F+G are both worth 7.0 - so equal totals are reachable and must be reached, which
    /// leaves the three-man side ahead on quality by a wide margin.
    /// </summary>
    [Fact]
    public void UnevenSizes_ShortHandedTeamGetsTheStrongerPlayers()
    {
        List<Player> players =
        [
            TestPlayers.Create("A", speed: 3, technical: 3, stamina: 3),
            TestPlayers.Create("B", speed: 3, technical: 3, stamina: 2),
            TestPlayers.Create("C", speed: 3, technical: 2, stamina: 2),
            TestPlayers.Create("D", speed: 2, technical: 2, stamina: 2),
            TestPlayers.Create("E", speed: 2, technical: 2, stamina: 1),
            TestPlayers.Create("F", speed: 1, technical: 2, stamina: 1),
            TestPlayers.Create("G", speed: 1, technical: 1, stamina: 1)
        ];

        var teams = new DraftStrategy().BalanceTeams(players, 2);

        var shortHanded = teams.MinBy(t => t.PlayerCount)!;
        var fullStrength = teams.MaxBy(t => t.PlayerCount)!;

        Assert.Equal(3, shortHanded.PlayerCount);
        Assert.Equal(4, fullStrength.PlayerCount);

        Assert.Equal(fullStrength.TotalSkillPoints, shortHanded.TotalSkillPoints, 4);

        Assert.True(shortHanded.OverallTeamSkill > fullStrength.OverallTeamSkill,
            $"The short-handed side should hold the better players, but averaged " +
            $"{shortHanded.OverallTeamSkill:F2} against {fullStrength.OverallTeamSkill:F2}.");

        AssertPlayersConserved(players, teams);
    }

    /// <summary>
    /// The regression that motivated scoring strength in attribute points rather than as the
    /// mean of them. This pool has an exact 3-way split at 7.33 apiece, but reaching it costs
    /// some stamina spread. Scored as a mean, strength was worth about a twentieth of the
    /// spread terms and the even-stamina, uneven-strength split won instead - leaving the
    /// three-man side behind on total as well as a player down.
    /// </summary>
    [Fact]
    public void UnevenSizes_StrengthParityOutranksAttributeSpread()
    {
        List<Player> players =
        [
            TestPlayers.Create("A", speed: 3, technical: 3, stamina: 3),
            TestPlayers.Create("B", speed: 3, technical: 3, stamina: 2),
            TestPlayers.Create("C", speed: 3, technical: 2, stamina: 2),
            TestPlayers.Create("D", speed: 2, technical: 2, stamina: 2),
            TestPlayers.Create("E", speed: 2, technical: 2, stamina: 1),
            TestPlayers.Create("F", speed: 1, technical: 2, stamina: 1),
            TestPlayers.Create("G", speed: 1, technical: 1, stamina: 1),
            TestPlayers.Create("H", speed: 3, technical: 1, stamina: 2),
            TestPlayers.Create("I", speed: 2, technical: 3, stamina: 2),
            TestPlayers.Create("J", speed: 1, technical: 2, stamina: 2),
            TestPlayers.Create("K", speed: 2, technical: 1, stamina: 3)
        ];

        var teams = new DraftStrategy().BalanceTeams(players, 3);

        Assert.Equal([3, 4, 4], teams.Select(t => t.PlayerCount).Order());

        double spread = teams.Max(t => t.TotalSkillPoints) - teams.Min(t => t.TotalSkillPoints);

        Assert.True(spread < 0.0001,
            "The pool splits exactly 7.33 / 7.33 / 7.33, so no team should be left behind on " +
            $"strength. Totals came out {string.Join(" / ", teams.Select(t => t.TotalSkillPoints.ToString("F2")))}.");

        AssertPlayersConserved(players, teams);
    }

    /// <summary>
    /// The property the two cases above are specimens of, asserted across a spread of awkward
    /// pool sizes: whatever else the scoring trades off, a team that is short a player must
    /// never also be the weaker team per player - and the teams must finish close on total
    /// strength, which is what forces the short-handed side to hold the better players.
    /// </summary>
    /// <param name="poolSize">How many players to draft.</param>
    /// <param name="numberOfTeams">How many teams to split them into.</param>
    /// <param name="maxMeanSpread">
    /// Ceiling on the mean gap between the strongest and weakest team's totals over the sweep.
    /// These are empirical, measured with roughly 15% headroom over what the strategy achieves,
    /// and every one of them sits below what the pre-existing scoring managed - a 5 v 2 pool
    /// averaged 0.83 against the 0.70 allowed here, an 8-player three-way 1.11 against 0.97.
    /// They are quality ratchets, so a change that loosens strength parity trips them; a change
    /// that tightens it should lower the numbers rather than leave slack.
    /// </param>
    [Theory]
    [InlineData(5, 2, 0.70)]
    [InlineData(7, 2, 0.60)]
    [InlineData(9, 2, 0.40)]
    [InlineData(11, 2, 0.31)]
    [InlineData(13, 2, 0.25)]
    [InlineData(15, 2, 0.22)]
    [InlineData(7, 3, 0.75)]
    [InlineData(8, 3, 0.97)]
    [InlineData(11, 3, 0.56)]
    [InlineData(13, 3, 0.47)]
    [InlineData(14, 3, 0.46)]
    public void UnevenSizes_ShortHandedTeamIsNeverWeakerPerPlayer(
        int poolSize,
        int numberOfTeams,
        double maxMeanSpread)
    {
        // One pool is not enough to catch this: the property held for all but a handful of
        // pools even under the old scoring, so a single fixed case would pass either way and
        // guard nothing. Each size combination therefore sweeps a run of pools off one seed,
        // which stays reproducible while covering enough skill spreads to bite. Positions are
        // dealt out too - the position term competes with strength for the same swaps, and the
        // pools that used to come out short-handed and outclassed were all position-bearing.
        Position[] positions =
        [
            Position.Goalkeeper, Position.Defender, Position.Midfielder, Position.Forward
        ];

        var rng = new Random(poolSize * 100 + numberOfTeams);
        var strategy = new DraftStrategy();
        double spreadSum = 0;

        for (int trial = 0; trial < 40; trial++)
        {
            var players = Enumerable.Range(1, poolSize)
                .Select(n => TestPlayers.Create(
                    $"P{n}",
                    positions[rng.Next(positions.Length)],
                    speed: rng.Next(1, 4),
                    technical: rng.Next(1, 4),
                    stamina: rng.Next(1, 4)))
                .ToList();

            var teams = strategy.BalanceTeams(players, numberOfTeams);

            spreadSum += teams.Max(t => t.TotalSkillPoints) - teams.Min(t => t.TotalSkillPoints);

            int smallest = teams.Min(t => t.PlayerCount);
            int largest = teams.Max(t => t.PlayerCount);

            // Sizes stay as even as the pool allows either way - that is the count term's job.
            Assert.True(largest - smallest <= 1,
                $"Trial {trial}: team sizes should differ by at most one, got " +
                $"{string.Join("/", teams.Select(t => t.PlayerCount))}.");

            if (smallest == largest)
            {
                continue;
            }

            double weakestShortHanded = teams.Where(t => t.PlayerCount == smallest).Min(t => t.OverallTeamSkill);
            double strongestFull = teams.Where(t => t.PlayerCount == largest).Max(t => t.OverallTeamSkill);

            Assert.True(weakestShortHanded >= strongestFull - 0.0001,
                $"Trial {trial}: a {smallest}-player team averaged {weakestShortHanded:F2} against a " +
                $"{largest}-player team's {strongestFull:F2} - short-handed and outclassed.");
        }

        double meanSpread = spreadSum / 40;

        Assert.True(meanSpread <= maxMeanSpread,
            $"Teams finished {meanSpread:F3} apart on total strength on average across " +
            $"{poolSize} players in {numberOfTeams} teams, over a ceiling of {maxMeanSpread:F2}. " +
            "Strength parity has been traded away for one of the other scoring terms.");
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
