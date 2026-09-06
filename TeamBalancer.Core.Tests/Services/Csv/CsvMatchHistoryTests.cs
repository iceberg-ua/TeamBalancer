namespace TeamBalancer.Core.Tests.Services.Csv;

using System.Globalization;
using TeamBalancer.Core.Models;
using TeamBalancer.Core.Services.Csv;
using TeamBalancer.Core.Tests.TestSupport;

/// <summary>
/// Covers reading matches.csv back: the rows a finished match became, grouped again into the
/// game they came from. The write side is pinned by <see cref="CsvMatchRepositoryTests"/>;
/// this is the other half of the round trip, plus what the reader does with a file that is not
/// what it wrote - the file lives in a directory the user's other tools can reach, and one bad
/// row must cost that row rather than the history.
/// </summary>
public class CsvMatchHistoryTests
{
    private const string Header = "MatchId,PlayedAt,ListId,Team,Score,PlayerId,PlayerName,Goals,Assists";

    private static Player NewPlayer(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Speed = 2,
        TechnicalSkills = 2,
        Stamina = 2,
        PrimaryPosition = Position.Midfielder
    };

    /// <summary>
    /// Team A beat Team B 2-1: Ivan scored both and Petro made one, Olena scored the reply.
    /// </summary>
    private static MatchRecord NewMatch(Guid listId, DateTime? playedAt = null)
    {
        var teamA = new MatchTeam { Name = "Team A" };
        var teamB = new MatchTeam { Name = "Team B" };

        var ivan = teamA.Add(NewPlayer("Ivan"));
        var petro = teamA.Add(NewPlayer("Petro"));

        teamA.AddGoal(ivan);
        teamA.AddGoal(ivan);
        teamA.AddAssist(petro);

        teamB.AddGoal(teamB.Add(NewPlayer("Olena")));

        return new MatchRecord
        {
            ListId = listId,
            PlayedAt = playedAt ?? DateTime.UtcNow,
            Teams = { teamA, teamB }
        };
    }

    private static async Task WriteRawAsync(TempDataDirectory directory, params string[] lines) =>
        await File.WriteAllTextAsync(
            Path.Combine(directory.Path_, CsvMatchRepository.MatchesFileName),
            string.Join(Environment.NewLine, lines) + Environment.NewLine);

    [Fact]
    public async Task GetAllAsync_WithNoFile_ReturnsNothing()
    {
        using var directory = new TempDataDirectory();
        var repository = new CsvMatchRepository(directory.Path_);

        // A squad that has never finished a game is the normal state of a fresh install, not
        // an error, so the history screen has to be able to open on it.
        Assert.Empty(await repository.GetAllAsync());
    }

    [Fact]
    public async Task GetAllAsync_ReadsBackTheMatchThatWasWritten()
    {
        using var directory = new TempDataDirectory();
        var repository = new CsvMatchRepository(directory.Path_);
        var listId = Guid.NewGuid();
        var written = NewMatch(listId);

        await repository.AppendAsync(written);

        var match = Assert.Single(await repository.GetAllAsync());

        Assert.Equal(written.Id, match.Id);
        Assert.Equal(written.PlayedAt, match.PlayedAt);
        Assert.Equal(listId, match.ListId);
        Assert.Equal(3, match.PlayerCount);

        // The sides come back in the order they were written, so the scoreline reads the way
        // it did on the screen the result was entered on.
        Assert.Collection(
            match.Teams,
            teamA =>
            {
                Assert.Equal("Team A", teamA.Name);
                Assert.Equal(2, teamA.Score);

                var ivan = teamA.Players.Single(p => p.Name == "Ivan");
                Assert.Equal(2, ivan.Goals);
                Assert.Equal(0, ivan.Assists);

                var petro = teamA.Players.Single(p => p.Name == "Petro");
                Assert.Equal(0, petro.Goals);
                Assert.Equal(1, petro.Assists);
            },
            teamB =>
            {
                Assert.Equal("Team B", teamB.Name);
                Assert.Equal(1, teamB.Score);
                Assert.Equal("Olena", Assert.Single(teamB.Players).Name);
            });
    }

    [Fact]
    public async Task GetAllAsync_KeepsThePlayerIdsTheMatchWasWrittenWith()
    {
        using var directory = new TempDataDirectory();
        var repository = new CsvMatchRepository(directory.Path_);
        var written = NewMatch(Guid.NewGuid());

        await repository.AppendAsync(written);

        var match = Assert.Single(await repository.GetAllAsync());
        var ivan = match.Teams[0].Players.Single(p => p.Name == "Ivan");

        Assert.Equal(
            written.Teams[0].Players.Single(p => p.Player.Name == "Ivan").Player.Id,
            ivan.Id);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsTheMostRecentMatchFirst()
    {
        using var directory = new TempDataDirectory();
        var repository = new CsvMatchRepository(directory.Path_);
        var listId = Guid.NewGuid();

        var oldest = NewMatch(listId, new DateTime(2026, 1, 5, 18, 0, 0, DateTimeKind.Utc));
        var newest = NewMatch(listId, new DateTime(2026, 3, 2, 18, 0, 0, DateTimeKind.Utc));
        var middle = NewMatch(listId, new DateTime(2026, 2, 9, 18, 0, 0, DateTimeKind.Utc));

        // Deliberately not appended in date order - the file is written in the order games were
        // finished, and a phone whose clock was corrected can hold one out of sequence.
        await repository.AppendAsync(oldest);
        await repository.AppendAsync(newest);
        await repository.AppendAsync(middle);

        var matches = await repository.GetAllAsync();

        Assert.Equal(new[] { newest.Id, middle.Id, oldest.Id }, matches.Select(m => m.Id));
    }

    [Fact]
    public async Task GetAllAsync_WithTwoMatchesFinishedInTheSameTick_PutsTheLaterOneFirst()
    {
        using var directory = new TempDataDirectory();
        var repository = new CsvMatchRepository(directory.Path_);
        var listId = Guid.NewGuid();
        var sameMoment = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc);

        var first = NewMatch(listId, sameMoment);
        var second = NewMatch(listId, sameMoment);

        await repository.AppendAsync(first);
        await repository.AppendAsync(second);

        var matches = await repository.GetAllAsync();

        // Nothing separates them but the order they were written in, and "most recent first"
        // must not quietly become "oldest first" because the timestamps tie.
        Assert.Equal(new[] { second.Id, first.Id }, matches.Select(m => m.Id));
    }

    [Fact]
    public async Task GetAllAsync_KeepsEveryListsMatches()
    {
        using var directory = new TempDataDirectory();
        var repository = new CsvMatchRepository(directory.Path_);

        var sunday = Guid.NewGuid();
        var work = Guid.NewGuid();

        await repository.AppendAsync(NewMatch(sunday));
        await repository.AppendAsync(NewMatch(work));

        var matches = await repository.GetAllAsync();

        // Filtering by list is the screen's decision, not the repository's - it hands back
        // everything and says which list each game came from.
        Assert.Equal(2, matches.Count);
        Assert.Single(matches, m => m.ListId == sunday);
        Assert.Single(matches, m => m.ListId == work);
    }

    [Fact]
    public async Task GetAllAsync_ASideEmptiedByAMove_ComesBackScoredButWithNobodyOnIt()
    {
        using var directory = new TempDataDirectory();
        var repository = new CsvMatchRepository(directory.Path_);

        var teamA = new MatchTeam { Name = "Team A" };
        var teamB = new MatchTeam { Name = "Team B" };

        var scorer = teamA.Add(NewPlayer("Ivan"));
        teamA.AddGoal(scorer);

        // The only player on Team A is sent across, taking the goal with him. The side is still
        // half of the scoreline, so the file keeps a row for it against the empty guid.
        MatchRecord.Move(scorer, teamA, teamB);

        await repository.AppendAsync(new MatchRecord { ListId = Guid.NewGuid(), Teams = { teamA, teamB } });

        var match = Assert.Single(await repository.GetAllAsync());
        var empty = match.Teams.Single(t => t.Name == "Team A");

        // That row is not a player, and must not come back as one with a blank name.
        Assert.Empty(empty.Players);
        Assert.Equal(0, empty.Score);
        Assert.Equal(1, match.PlayerCount);
    }

    [Fact]
    public async Task GetAllAsync_AScoreNobodyWasNamedFor_IsReportedAsUnattributed()
    {
        using var directory = new TempDataDirectory();
        var repository = new CsvMatchRepository(directory.Path_);

        var teamA = new MatchTeam { Name = "Team A" };
        var teamB = new MatchTeam { Name = "Team B" };

        teamA.Add(NewPlayer("Ivan"));
        teamB.Add(NewPlayer("Olena"));

        // Three goals tapped straight onto the scoreboard with nobody named for any of them.
        teamA.IncrementScore();
        teamA.IncrementScore();
        teamA.IncrementScore();

        await repository.AppendAsync(new MatchRecord { ListId = Guid.NewGuid(), Teams = { teamA, teamB } });

        var match = Assert.Single(await repository.GetAllAsync());

        Assert.True(match.HasUnattributedGoals);
        Assert.Equal(3, match.UnattributedGoals);
        Assert.Equal(3, match.Teams[0].Score);
        Assert.Equal(0, match.Teams[0].AttributedGoals);
    }

    [Fact]
    public async Task GetAllAsync_ReadsTimestampsWrittenUnderAnotherCulture()
    {
        using var directory = new TempDataDirectory();
        var repository = new CsvMatchRepository(directory.Path_);
        var match = NewMatch(Guid.NewGuid());

        var culture = CultureInfo.CurrentCulture;

        try
        {
            // Written under one calendar and read back under another: the phone's language can
            // be changed between finishing a game and looking at it again.
            CultureInfo.CurrentCulture = new CultureInfo("ar-SA");
            await repository.AppendAsync(match);

            CultureInfo.CurrentCulture = new CultureInfo("uk-UA");

            var readBack = Assert.Single(await repository.GetAllAsync());

            Assert.Equal(match.PlayedAt, readBack.PlayedAt);
            Assert.Equal(DateTimeKind.Utc, readBack.PlayedAt.Kind);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public async Task GetAllAsync_SkipsRowsItCouldNotHaveWritten()
    {
        using var directory = new TempDataDirectory();
        var repository = new CsvMatchRepository(directory.Path_);

        var matchId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var playedAt = new DateTime(2026, 5, 4, 17, 30, 0, DateTimeKind.Utc)
            .ToString("O", CultureInfo.InvariantCulture);

        await WriteRawAsync(
            directory,
            Header,
            $"{matchId},{playedAt},{listId},Team A,1,{Guid.NewGuid()},Ivan,1,0",
            "not-a-guid,nonsense,,Team A,1,,Nobody,1,0",
            $"{matchId},{playedAt},{listId},Team A,1",
            $"{matchId},{playedAt},{listId},Team A,1,{Guid.NewGuid()},Petro,-1,0",
            $"{matchId},{playedAt},{listId},Team A,1,{Guid.NewGuid()},Olena,x,0",
            Header,
            $"{matchId},{playedAt},{listId},Team B,0,{Guid.NewGuid()},Maria,0,0");

        var match = Assert.Single(await repository.GetAllAsync());

        // The two readable players survive; the short row, the unparseable one, the negative
        // tally and both header rows are dropped without taking the match with them.
        Assert.Equal(2, match.PlayerCount);
        Assert.Equal("Ivan", Assert.Single(match.Teams[0].Players).Name);
        Assert.Equal("Maria", Assert.Single(match.Teams[1].Players).Name);
    }

    [Fact]
    public async Task GetAllAsync_ARowSplitIntoTooManyCells_IsDroppedRatherThanMisread()
    {
        using var directory = new TempDataDirectory();
        var repository = new CsvMatchRepository(directory.Path_);

        var matchId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var playedAt = new DateTime(2026, 5, 4, 17, 30, 0, DateTimeKind.Utc)
            .ToString("O", CultureInfo.InvariantCulture);

        await WriteRawAsync(
            directory,
            Header,
            $"{matchId},{playedAt},{listId},Team A,1,{Guid.NewGuid()},Ivan,1,0",

            // A name that somehow carries a comma. Reading the first nine cells would credit
            // the goal to "Petro" and lose the rest of the row, so the row goes instead.
            $"{matchId},{playedAt},{listId},Team A,1,{Guid.NewGuid()},Petro,Jr,1,0");

        var match = Assert.Single(await repository.GetAllAsync());

        Assert.Equal("Ivan", Assert.Single(match.Teams[0].Players).Name);
    }

    [Fact]
    public async Task GetAllAsync_WithNothingReadableInTheFile_ReturnsNothing()
    {
        using var directory = new TempDataDirectory();
        var repository = new CsvMatchRepository(directory.Path_);

        await WriteRawAsync(directory, Header);

        // A file holding only its header is what an install that has written nothing yet looks
        // like if a finish failed part way through. It is empty, not broken.
        Assert.Empty(await repository.GetAllAsync());
    }

    [Fact]
    public async Task GetAllAsync_AfterAppending_SeesTheNewMatch()
    {
        using var directory = new TempDataDirectory();
        var repository = new CsvMatchRepository(directory.Path_);
        var listId = Guid.NewGuid();

        await repository.AppendAsync(NewMatch(listId));
        Assert.Single(await repository.GetAllAsync());

        // Nothing is cached between the two: finishing a game and going straight to the history
        // has to show it.
        await repository.AppendAsync(NewMatch(listId));
        Assert.Equal(2, (await repository.GetAllAsync()).Count);
    }
}
