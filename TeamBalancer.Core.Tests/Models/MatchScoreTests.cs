namespace TeamBalancer.Core.Tests.Models;

using TeamBalancer.Core.Models;

/// <summary>
/// Covers the rule the scoreboard runs on: a side's score is the larger of the figure entered
/// by hand and the goals attributed to its scorers. Everything the user was promised - a score
/// that counts itself when nobody enters one, a score that holds still while scorers are named
/// afterwards, and a score that gives way when the names outnumber it - is that one comparison,
/// so it is pinned down here rather than left to be re-derived from the screen.
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

        team.Players[0].AddGoal();
        team.Players[0].AddGoal();
        team.Players[1].AddGoal();

        Assert.Equal(3, team.Score);
        Assert.Equal(0, team.UnattributedGoals);
    }

    [Fact]
    public void Score_EnteredByHand_IsNotMovedByNamingScorers()
    {
        var team = NewTeam("Ivan", "Petro");

        Assert.True(team.TrySetScore(5));

        team.Players[0].AddGoal();
        team.Players[0].AddGoal();
        team.Players[1].AddGoal();

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

        team.Players[0].AddGoal();
        team.Players[0].AddGoal();

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
            team.Players[0].AddGoal();
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
            team.Players[0].AddGoal();
        }

        Assert.Equal(6, team.Score);

        // Deleting the goal that pushed it past the entered figure lets it come back down...
        team.Players[0].RemoveGoal();
        Assert.Equal(5, team.Score);

        // ...and it keeps coming down until the entered figure catches it.
        team.Players[0].RemoveGoal();
        Assert.Equal(4, team.Score);

        team.Players[0].RemoveGoal();
        Assert.Equal(4, team.Score);
        Assert.Equal(3, team.AttributedGoals);
    }

    [Fact]
    public void TrySetScore_BelowTheGoalsAlreadyNamed_IsRefused()
    {
        var team = NewTeam("Ivan");

        team.Players[0].AddGoal();
        team.Players[0].AddGoal();
        team.Players[0].AddGoal();

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

        team.Players[0].AddGoal();
        team.Players[0].AddGoal();
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
        var participant = new MatchPlayer { Player = NewPlayer("Ivan") };

        participant.RemoveGoal();
        participant.RemoveAssist();

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
        scorer.AddGoal();
        scorer.AddGoal();
        scorer.AddAssist();

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
        scorer.AddGoal();

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
        first.AddGoal();

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

        teamA.Add(NewPlayer("Ivan")).AddGoal();
        Assert.True(teamA.TrySetScore(3));

        Assert.True(teamB.TrySetScore(1));

        var match = new MatchRecord { Teams = { teamA, teamB } };

        Assert.True(match.HasUnattributedGoals);
        Assert.Equal(3, match.UnattributedGoals);
    }
}
