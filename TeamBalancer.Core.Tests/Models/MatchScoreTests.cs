namespace TeamBalancer.Core.Tests.Models;

using TeamBalancer.Core.Models;

/// <summary>
/// Covers the rule the scoreboard runs on: a side's score is the larger of the figure entered
/// by hand and the goals actually recorded, named or not. Everything the user was promised - a
/// score that counts itself when nobody enters one, a score that holds still while scorers are
/// named afterwards, a score that gives way when the names outnumber it, and a goal tapped
/// straight onto the scoreboard that stays a goal of its own - is that one comparison, so it is
/// pinned down here rather than left to be re-derived from the screen.
/// </summary>
public class MatchScoreTests
{
    private static Player NewPlayer(string name) => new()
    {
        Name = name,
        Speed = 2,
        TechnicalSkills = 2,
        Stamina = 2,
        PrimaryPosition = Position.Midfielder
    };

    private static MatchTeam NewTeam(params string[] playerNames)
    {
        var team = new MatchTeam { Name = "Team A" };

        foreach (var name in playerNames)
        {
            team.Add(NewPlayer(name));
        }

        return team;
    }

    [Fact]
    public void Score_WithNothingRecorded_IsZero()
    {
        var team = NewTeam("Ivan", "Petro");

        Assert.Equal(0, team.Score);
        Assert.False(team.HasUnattributedGoals);
    }

    [Fact]
    public void Score_WithNoManualEntry_CountsTheGoalsAttributed()
    {
        var team = NewTeam("Ivan", "Petro");

        team.AddGoal(team.Players[0]);
        team.AddGoal(team.Players[0]);
        team.AddGoal(team.Players[1]);

        Assert.Equal(3, team.Score);
        Assert.Equal(0, team.UnattributedGoals);
    }

    [Fact]
    public void Score_EnteredByHand_IsNotMovedByNamingScorers()
    {
        var team = NewTeam("Ivan", "Petro");

        Assert.True(team.TrySetScore(5));

        team.AddGoal(team.Players[0]);
        team.AddGoal(team.Players[0]);
        team.AddGoal(team.Players[1]);

        // Three of the five now have a scorer. The score is still the five that were entered.
        Assert.Equal(5, team.Score);
        Assert.Equal(3, team.AttributedGoals);
        Assert.Equal(2, team.UnattributedGoals);
    }

    [Fact]
    public void Score_EnteredByHand_IsMatchedExactlyWhenTheLastGoalIsNamed()
    {
        var team = NewTeam("Ivan");

        Assert.True(team.TrySetScore(2));

        team.AddGoal(team.Players[0]);
        team.AddGoal(team.Players[0]);

        Assert.Equal(2, team.Score);
        Assert.False(team.HasUnattributedGoals);
    }

    [Fact]
    public void Score_RisesWhenTheGoalsNamedOutnumberTheFigureEntered()
    {
        var team = NewTeam("Ivan");

        Assert.True(team.TrySetScore(5));

        for (var i = 0; i < 6; i++)
        {
            team.AddGoal(team.Players[0]);
        }

        Assert.Equal(6, team.Score);
    }

    [Fact]
    public void Score_IsRecomputedRatherThanLatched()
    {
        var team = NewTeam("Ivan");

        Assert.True(team.TrySetScore(4));

        for (var i = 0; i < 6; i++)
        {
            team.AddGoal(team.Players[0]);
        }

        Assert.Equal(6, team.Score);

        // Deleting the goal that pushed it past the entered figure lets it come back down...
        team.RemoveGoal(team.Players[0]);
        Assert.Equal(5, team.Score);

        // ...and it keeps coming down until the entered figure catches it.
        team.RemoveGoal(team.Players[0]);
        Assert.Equal(4, team.Score);

        team.RemoveGoal(team.Players[0]);
        Assert.Equal(4, team.Score);
        Assert.Equal(3, team.AttributedGoals);
    }

    [Fact]
    public void TrySetScore_BelowTheGoalsAlreadyNamed_IsRefused()
    {
        var team = NewTeam("Ivan");

        team.AddGoal(team.Players[0]);
        team.AddGoal(team.Players[0]);
        team.AddGoal(team.Players[0]);

        Assert.False(team.TrySetScore(2));
        Assert.Equal(3, team.Score);
    }

    [Fact]
    public void TrySetScore_Negative_IsRefused()
    {
        var team = NewTeam("Ivan");

        Assert.False(team.TrySetScore(-1));
        Assert.Equal(0, team.Score);
    }

    [Fact]
    public void IncrementScore_AddsAGoalNobodyIsNamedFor()
    {
        var team = NewTeam("Ivan");

        team.IncrementScore();
        team.IncrementScore();

        Assert.Equal(2, team.Score);
        Assert.Equal(0, team.AttributedGoals);
        Assert.Equal(2, team.UnattributedGoals);
    }

    [Fact]
    public void DecrementScore_StopsAtTheGoalsAlreadyNamed()
    {
        var team = NewTeam("Ivan");

        team.AddGoal(team.Players[0]);
        team.AddGoal(team.Players[0]);
        team.IncrementScore();

        Assert.Equal(3, team.Score);

        team.DecrementScore();
        Assert.Equal(2, team.Score);

        // Both remaining goals have a scorer, so the score cannot be taken any lower without
        // one of them being taken off its scorer first.
        Assert.False(team.CanDecrementScore);

        team.DecrementScore();
        Assert.Equal(2, team.Score);
    }

    [Fact]
    public void Tallies_NeverGoNegative()
    {
        var team = NewTeam("Ivan");
        var participant = team.Players[0];

        team.RemoveGoal(participant);
        team.RemoveAssist(participant);

        Assert.Equal(0, participant.Goals);
        Assert.Equal(0, participant.Assists);
        Assert.False(participant.HasGoals);
        Assert.False(participant.HasAssists);
    }

    [Fact]
    public void MovingAPlayer_TakesTheirGoalsToTheOtherSide()
    {
        var from = new MatchTeam { Name = "Team A" };
        var to = new MatchTeam { Name = "Team B" };

        var scorer = from.Add(NewPlayer("Ivan"));
        from.AddGoal(scorer);
        from.AddGoal(scorer);
        from.AddAssist(scorer);

        Assert.Equal(2, from.Score);
        Assert.Equal(0, to.Score);

        MatchRecord.Move(scorer, from, to);

        // Which side those goals counted for is exactly what the move corrects, so they go
        // with the player rather than staying behind.
        Assert.Equal(0, from.Score);
        Assert.Equal(2, to.Score);
        Assert.Equal(1, to.Players[0].Assists);
        Assert.Empty(from.Players);
    }

    [Fact]
    public void MovingAPlayer_LeavesAScoreEnteredByHandWhereItWas()
    {
        var from = new MatchTeam { Name = "Team A" };
        var to = new MatchTeam { Name = "Team B" };

        var scorer = from.Add(NewPlayer("Ivan"));
        from.AddGoal(scorer);

        Assert.True(from.TrySetScore(4));

        MatchRecord.Move(scorer, from, to);

        // The four were entered against this side, not against the player. Only the goal that
        // was pinned to him leaves with him.
        Assert.Equal(4, from.Score);
        Assert.Equal(4, from.UnattributedGoals);
        Assert.Equal(1, to.Score);
    }

    [Fact]
    public void AddingAPlayerTwice_DoesNotDuplicateThem()
    {
        var team = new MatchTeam { Name = "Team A" };
        var player = NewPlayer("Ivan");

        var first = team.Add(player);
        team.AddGoal(first);

        var second = team.Add(player);

        Assert.Same(first, second);
        Assert.Single(team.Players);
        Assert.Equal(1, team.Score);
    }

    [Fact]
    public void FromTeams_GivesTheMatchItsOwnSides()
    {
        var teamA = new Team { Name = "Team A" };
        teamA.AddPlayer(NewPlayer("Ivan"));

        var teamB = new Team { Name = "Team B" };
        teamB.AddPlayer(NewPlayer("Petro"));

        var match = MatchRecord.FromTeams([teamA, teamB], Guid.NewGuid());

        Assert.Equal(2, match.Teams.Count);
        Assert.Equal(2, match.PlayerCount);

        // Moving someone during the match must not reach back and rearrange the split it was
        // accepted from - the user can still go back to that split and draw again.
        MatchRecord.Move(match.Teams[0].Players[0], match.Teams[0], match.Teams[1]);

        Assert.Single(teamA.Players);
        Assert.Empty(match.Teams[0].Players);
        Assert.Equal(2, match.Teams[1].Players.Count);
    }

    [Fact]
    public void Contains_KnowsWhoIsAlreadyOnThePitch()
    {
        var teamA = new Team { Name = "Team A" };
        var playing = NewPlayer("Ivan");
        teamA.AddPlayer(playing);

        var match = MatchRecord.FromTeams([teamA], Guid.NewGuid());

        Assert.True(match.Contains(playing.Id));
        Assert.False(match.Contains(NewPlayer("Petro").Id));
    }

    [Fact]
    public void UnattributedGoals_AreCountedAcrossBothSides()
    {
        var teamA = new MatchTeam { Name = "Team A" };
        var teamB = new MatchTeam { Name = "Team B" };

        teamA.AddGoal(teamA.Add(NewPlayer("Ivan")));
        Assert.True(teamA.TrySetScore(3));

        Assert.True(teamB.TrySetScore(1));

        var match = new MatchRecord { Teams = { teamA, teamB } };

        Assert.True(match.HasUnattributedGoals);
        Assert.Equal(3, match.UnattributedGoals);
    }

    // ---- A goal carries at most one assist ----

    [Fact]
    public void AddAssist_WithNoGoalScored_IsRefused()
    {
        var team = NewTeam("Ivan");

        Assert.False(team.CanAddAssist);

        team.AddAssist(team.Players[0]);

        Assert.Equal(0, team.AttributedAssists);
        Assert.Equal(0, team.Score);
    }

    [Fact]
    public void AddAssist_StopsOnceEveryGoalHasOne()
    {
        var team = NewTeam("Ivan", "Petro");

        team.AddGoal(team.Players[0]);
        team.AddGoal(team.Players[0]);

        team.AddAssist(team.Players[1]);
        team.AddAssist(team.Players[1]);

        Assert.Equal(2, team.AttributedAssists);
        Assert.False(team.CanAddAssist);

        // The third has no goal to belong to.
        team.AddAssist(team.Players[1]);

        Assert.Equal(2, team.AttributedAssists);
        Assert.Equal(2, team.Score);
    }

    [Fact]
    public void AddAssist_CountsAgainstGoalsNobodyIsNamedFor()
    {
        var team = NewTeam("Ivan");

        // Three goals went in and nobody has been named for any of them - but the assists are
        // known. Naming an assister does not require naming the scorer first.
        Assert.True(team.TrySetScore(3));

        team.AddAssist(team.Players[0]);
        team.AddAssist(team.Players[0]);
        team.AddAssist(team.Players[0]);

        Assert.Equal(3, team.AttributedAssists);
        Assert.Equal(3, team.Score);
        Assert.False(team.CanAddAssist);
    }

    [Fact]
    public void RemoveGoal_StrandingAnAssist_LeavesTheScoreCoveringIt()
    {
        var team = NewTeam("Ivan", "Petro");

        team.AddGoal(team.Players[0]);
        team.AddGoal(team.Players[0]);
        team.AddAssist(team.Players[1]);
        team.AddAssist(team.Players[1]);

        // Correcting a mis-tapped scorer is allowed even though it leaves more assists than
        // named goals - the score keeps them covered rather than the button refusing.
        team.RemoveGoal(team.Players[0]);

        Assert.Equal(1, team.AttributedGoals);
        Assert.Equal(2, team.AttributedAssists);
        Assert.Equal(2, team.Score);
    }

    [Fact]
    public void DecrementScore_StopsAtTheAssistsRecorded()
    {
        var team = NewTeam("Ivan");

        Assert.True(team.TrySetScore(2));

        team.AddAssist(team.Players[0]);
        team.AddAssist(team.Players[0]);

        Assert.False(team.CanDecrementScore);

        team.DecrementScore();

        Assert.Equal(2, team.Score);
    }

    [Fact]
    public void TrySetScore_BelowTheAssistsRecorded_IsRefused()
    {
        var team = NewTeam("Ivan");

        Assert.True(team.TrySetScore(3));

        team.AddAssist(team.Players[0]);
        team.AddAssist(team.Players[0]);
        team.AddAssist(team.Players[0]);

        Assert.False(team.TrySetScore(1));
        Assert.Equal(3, team.Score);
    }

    [Fact]
    public void MovingAScorerAway_CannotLeaveTheSideClaimingMoreAssistsThanGoals()
    {
        var from = new MatchTeam { Name = "Team A" };
        var to = new MatchTeam { Name = "Team B" };

        var scorer = from.Add(NewPlayer("Ivan"));
        var assister = from.Add(NewPlayer("Petro"));

        from.AddGoal(scorer);
        from.AddGoal(scorer);
        from.AddAssist(assister);
        from.AddAssist(assister);

        // The scorer leaves and takes both goals; the assists stay behind with the player who
        // made them. Nothing was capped on the way out, so only the floor holds the rule up.
        MatchRecord.Move(scorer, from, to);

        Assert.Equal(0, from.AttributedGoals);
        Assert.Equal(2, from.AttributedAssists);
        Assert.Equal(2, from.Score);
        Assert.True(from.AttributedAssists <= from.Score);
    }

    [Fact]
    public void MovingAnAssisterAway_CarriesTheirAssistsWithThem()
    {
        var from = new MatchTeam { Name = "Team A" };
        var to = new MatchTeam { Name = "Team B" };

        var scorer = from.Add(NewPlayer("Ivan"));
        var assister = from.Add(NewPlayer("Petro"));

        from.AddGoal(scorer);
        from.AddAssist(assister);

        MatchRecord.Move(assister, from, to);

        Assert.Equal(1, from.Score);
        Assert.Equal(0, from.AttributedAssists);

        // The receiving side now claims an assist with no goal behind it, so its score has to
        // account for the goal that assist proves happened.
        Assert.Equal(1, to.AttributedAssists);
        Assert.Equal(1, to.Score);
        Assert.True(to.AttributedAssists <= to.Score);
    }

    // ---- A goal tapped onto the scoreboard is a goal of its own ----

    [Fact]
    public void IncrementScore_ThenNamingTheNextScorer_AddsToTheScoreRatherThanDisappearingIntoIt()
    {
        var team = NewTeam("Ivan", "Petro");

        // Ivan scores and is tapped.
        team.AddGoal(team.Players[0]);
        Assert.Equal(1, team.Score);

        // A second goes in and nobody sees the scorer, so it goes straight on the scoreboard.
        team.IncrementScore();
        Assert.Equal(2, team.Score);
        Assert.Equal(1, team.UnattributedGoals);

        // Petro scores the third. It is a goal in its own right: the anonymous one is still
        // sitting in the score, and must not be quietly used up by the goal after it.
        team.AddGoal(team.Players[1]);

        Assert.Equal(3, team.Score);
        Assert.Equal(2, team.AttributedGoals);
        Assert.Equal(1, team.UnattributedGoals);
        Assert.True(team.HasUnattributedGoals);
    }

    [Fact]
    public void IncrementScore_AfterAFigureEnteredByHand_RaisesThatFigureRatherThanStandingBesideIt()
    {
        var team = NewTeam("Ivan");

        Assert.True(team.TrySetScore(3));

        // One more went in after the figure was entered. The figure is what is carrying the
        // unnamed goals, so it is what grows - and the score goes up by exactly one.
        team.IncrementScore();
        Assert.Equal(4, team.Score);
        Assert.Equal(4, team.UnattributedGoals);

        // All four now have a scorer, and naming them lands exactly on the figure rather than
        // overshooting it by the goal that was tapped in.
        for (var i = 0; i < 4; i++)
        {
            team.AddGoal(team.Players[0]);
        }

        Assert.Equal(4, team.Score);
        Assert.False(team.HasUnattributedGoals);

        // The fifth is one goal too many, which is the only way the count takes over.
        team.AddGoal(team.Players[0]);
        Assert.Equal(5, team.Score);
    }

    [Fact]
    public void IncrementScore_WithTheScoreHeldUpByAssists_StillAddsExactlyOne()
    {
        var team = NewTeam("Ivan", "Petro");

        Assert.True(team.TrySetScore(2));

        team.AddAssist(team.Players[1]);
        team.AddAssist(team.Players[1]);
        Assert.Equal(2, team.AttributedAssists);

        team.IncrementScore();

        Assert.Equal(3, team.Score);
        Assert.Equal(3, team.UnattributedGoals);
    }

    [Fact]
    public void DecrementScore_TakesOffTheGoalsTappedOntoTheScoreboardOneAtATime()
    {
        var team = NewTeam("Ivan");

        team.AddGoal(team.Players[0]);
        team.IncrementScore();
        team.IncrementScore();

        Assert.Equal(3, team.Score);

        team.DecrementScore();
        Assert.Equal(2, team.Score);

        team.DecrementScore();
        Assert.Equal(1, team.Score);

        // What is left is pinned to a scorer, so it cannot come off the scoreboard.
        Assert.False(team.CanDecrementScore);
        Assert.Equal(1, team.AttributedGoals);
    }

    [Fact]
    public void TrySetScore_BelowTheGoalsTappedOntoTheScoreboard_TakesThemOffRatherThanRefusing()
    {
        var team = NewTeam("Ivan");

        team.IncrementScore();
        team.IncrementScore();
        team.IncrementScore();
        Assert.Equal(3, team.Score);

        // Nothing here is pinned to a player, so typing a lower figure does what pressing
        // minus twice would do. Refusing it would leave the user with no way to correct a
        // scoreboard they had over-tapped except by finding the minus button.
        Assert.True(team.TrySetScore(1));
        Assert.Equal(1, team.Score);
        Assert.Equal(1, team.UnattributedGoals);

        // And the goals taken off are gone rather than lurking behind the lower figure.
        Assert.True(team.TrySetScore(2));
        Assert.Equal(2, team.Score);
    }

    [Fact]
    public void MovingAPlayerOntoASideThatAlreadyHasThem_MergesRatherThanListingThemTwice()
    {
        var from = new MatchTeam { Name = "Team A" };
        var to = new MatchTeam { Name = "Team B" };

        var player = NewPlayer("Ivan");

        var here = from.Add(player);
        var alsoThere = to.Add(player);

        from.AddGoal(here);
        from.AddGoal(here);
        from.AddAssist(here);

        to.AddGoal(alsoThere);

        MatchRecord.Move(here, from, to);

        // One person is one entry on a side. Two would both be summed into the score and both
        // be written down when the match is finished, under the same player id.
        Assert.Single(to.Players);
        Assert.Empty(from.Players);

        Assert.Equal(3, to.AttributedGoals);
        Assert.Equal(1, to.AttributedAssists);
        Assert.Equal(3, to.Score);
    }
}
