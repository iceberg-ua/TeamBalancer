namespace TeamBalancer.Core.Tests.Services.Csv;

using System.Globalization;
using TeamBalancer.Core.Models;
using TeamBalancer.Core.Services.Csv;
using TeamBalancer.Core.Tests.TestSupport;

/// <summary>
/// Covers matches.csv: the shape of the rows a finished match becomes, the header that has to
/// lead the file, the append that must never turn into an overwrite, and the one rewrite the
/// file ever gets - deleting a match - which must take that match's rows and nothing else. A
/// row written wrong today is a row every future reader has to cope with, which is why the
/// shape is pinned here rather than checked by eye once.
/// </summary>
public class CsvMatchRepositoryTests
{
    private const string Header = "MatchId,PlayedAt,ListId,Team,Score,PlayerId,PlayerName,Goals,Assists";

    private const int ColumnCount = 9;

    private static Player NewPlayer(string name) => new()
    {
        Name = name,
        Speed = 2,
        TechnicalSkills = 2,
        Stamina = 2,
        PrimaryPosition = Position.Midfielder
    };

    /// <summary>
    /// A one-a-side match: Ivan scores for Team A and Petro assists nothing for Team B.
    /// </summary>
    private static MatchRecord NewMatch(Guid listId)
    {
        var teamA = new MatchTeam { Name = "Team A" };
        var teamB = new MatchTeam { Name = "Team B" };

        teamA.AddGoal(teamA.Add(NewPlayer("Ivan")));
        teamB.Add(NewPlayer("Petro"));

        return new MatchRecord { ListId = listId, Teams = { teamA, teamB } };
    }

    private static string[] NonEmptyLines(string contents) =>
        contents.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public async Task AppendAsync_FirstMatch_WritesTheHeaderAndARowPerPlayer()
    {
        using var directory = new TempDataDirectory();
        var repository = new CsvMatchRepository(directory.Path_);
        var listId = Guid.NewGuid();
        var match = NewMatch(listId);

        await repository.AppendAsync(match);

        var lines = NonEmptyLines(directory.Read(CsvMatchRepository.MatchesFileName));

        Assert.Equal(3, lines.Length);
        Assert.Equal(Header, lines[0]);

        // The name is stored next to the id rather than looked up later: renaming or deleting
        // a player afterwards must not rewrite a game that has already been played.
        var ivan = lines[1].Split(',');

        Assert.Equal(ColumnCount, ivan.Length);
        Assert.Equal(match.Id.ToString(), ivan[0]);
        Assert.Equal(listId.ToString(), ivan[2]);
        Assert.Equal("Team A", ivan[3]);
        Assert.Equal("1", ivan[4]);
        Assert.Equal("Ivan", ivan[6]);
        Assert.Equal("1", ivan[7]);
        Assert.Equal("0", ivan[8]);

        var petro = lines[2].Split(',');

        Assert.Equal("Team B", petro[3]);
        Assert.Equal("0", petro[4]);
        Assert.Equal("Petro", petro[6]);
    }

    [Fact]
    public async Task AppendAsync_SecondMatch_AddsToTheFileRatherThanReplacingIt()
    {
        using var directory = new TempDataDirectory();
        var repository = new CsvMatchRepository(directory.Path_);

        var first = NewMatch(Guid.NewGuid());
        var second = NewMatch(Guid.NewGuid());

        await repository.AppendAsync(first);
        await repository.AppendAsync(second);

        var lines = NonEmptyLines(directory.Read(CsvMatchRepository.MatchesFileName));

        // One header and two players per match. The header is not repeated: a second copy of
        // it in the middle of the file is a row no reader can parse as a match.
        Assert.Equal(5, lines.Length);
        Assert.Equal(Header, lines[0]);
        Assert.Single(lines, line => line == Header);

        Assert.Equal(2, lines.Count(line => line.StartsWith(first.Id.ToString(), StringComparison.Ordinal)));
        Assert.Equal(2, lines.Count(line => line.StartsWith(second.Id.ToString(), StringComparison.Ordinal)));
    }

    [Fact]
    public async Task AppendAsync_OverAFileLeftEmpty_StillWritesTheHeader()
    {
        using var directory = new TempDataDirectory();
        var filePath = Path.Combine(directory.Path_, CsvMatchRepository.MatchesFileName);

        // A first write that failed part way, or storage the phone reclaimed the contents of.
        // The file exists, so a check for existence alone would skip the header for good.
        await File.WriteAllTextAsync(filePath, string.Empty);

        var repository = new CsvMatchRepository(directory.Path_);

        await repository.AppendAsync(NewMatch(Guid.NewGuid()));

        var lines = NonEmptyLines(directory.Read(CsvMatchRepository.MatchesFileName));

        Assert.Equal(Header, lines[0]);
        Assert.Equal(3, lines.Length);
    }

    [Fact]
    public async Task AppendAsync_ASideEmptiedByAMove_IsWrittenAsNobodyRatherThanABlankId()
    {
        using var directory = new TempDataDirectory();
        var repository = new CsvMatchRepository(directory.Path_);

        var teamA = new MatchTeam { Name = "Team A" };
        var teamB = new MatchTeam { Name = "Team B" };

        var scorer = teamA.Add(NewPlayer("Ivan"));
        teamA.AddGoal(scorer);

        // The only player on Team A is sent across, taking the goal with him.
        MatchRecord.Move(scorer, teamA, teamB);

        await repository.AppendAsync(new MatchRecord { ListId = Guid.NewGuid(), Teams = { teamA, teamB } });

        var lines = NonEmptyLines(directory.Read(CsvMatchRepository.MatchesFileName));
        var emptySide = lines.Single(line => line.Contains(",Team A,", StringComparison.Ordinal)).Split(',');

        // The row is written regardless - the score belongs to the side, not to anyone on it -
        // but PlayerId is a guid column, and a reader meets this row for as long as the file
        // lives. It has to hold something parseable that says nobody.
        Assert.Equal(ColumnCount, emptySide.Length);
        Assert.Equal(Guid.Empty, Guid.Parse(emptySide[5]));
        Assert.Equal("0", emptySide[7]);
        Assert.Equal("0", emptySide[8]);
    }

    [Fact]
    public async Task AppendAsync_WritesATimestampThatSurvivesThePhoneCulture()
    {
        using var directory = new TempDataDirectory();
        var repository = new CsvMatchRepository(directory.Path_);
        var match = NewMatch(Guid.NewGuid());

        var culture = CultureInfo.CurrentCulture;

        try
        {
            // A calendar that is not Gregorian and a separator that is not a full stop. The
            // year has to come out Gregorian rather than Hijri, and the timestamp must not
            // pick up a comma that would split the row into an extra column.
            CultureInfo.CurrentCulture = new CultureInfo("ar-SA");

            await repository.AppendAsync(match);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }

        var row = NonEmptyLines(directory.Read(CsvMatchRepository.MatchesFileName))[1].Split(',');

        Assert.Equal(ColumnCount, row.Length);

        Assert.True(DateTime.TryParseExact(
            row[1],
            "O",
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var readBack));

        Assert.Equal(match.PlayedAt, readBack);
    }

    [Fact]
    public async Task AppendAsync_WritesIntoADirectoryTheAppHasNeverUsed()
    {
        using var directory = new TempDataDirectory();
        var nested = Path.Combine(directory.Path_, "never-written-to");
        var repository = new CsvMatchRepository(nested);

        await repository.AppendAsync(NewMatch(Guid.NewGuid()));

        Assert.True(File.Exists(Path.Combine(nested, CsvMatchRepository.MatchesFileName)));
    }

    [Fact]
    public async Task AppendAsync_WithNoMatch_Throws()
    {
        using var directory = new TempDataDirectory();
        var repository = new CsvMatchRepository(directory.Path_);

        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.AppendAsync(null!));
    }

    // ---- Deleting a match ----

    [Fact]
    public async Task DeleteAsync_RemovesThatMatchAndLeavesTheOthers()
    {
        using var directory = new TempDataDirectory();
        var repository = new CsvMatchRepository(directory.Path_);

        var first = NewMatch(Guid.NewGuid());
        var second = NewMatch(Guid.NewGuid());

        await repository.AppendAsync(first);
        await repository.AppendAsync(second);

        Assert.True(await repository.DeleteAsync(first.Id));

        var remaining = Assert.Single(await repository.GetAllAsync());
        Assert.Equal(second.Id, remaining.Id);
        Assert.Null(await repository.GetByIdAsync(first.Id));

        // The header stays, once, and the copy the rewrite went through is not left beside
        // the file.
        var lines = NonEmptyLines(directory.Read(CsvMatchRepository.MatchesFileName));

        Assert.Equal(3, lines.Length);
        Assert.Equal(Header, lines[0]);
        Assert.DoesNotContain(lines, line => line.StartsWith(first.Id.ToString(), StringComparison.Ordinal));
        Assert.Single(Directory.GetFiles(directory.Path_));
    }

    [Fact]
    public async Task DeleteAsync_AMatchThatIsNotThere_ReturnsFalseAndLeavesTheFileAlone()
    {
        using var directory = new TempDataDirectory();
        var repository = new CsvMatchRepository(directory.Path_);

        await repository.AppendAsync(NewMatch(Guid.NewGuid()));
        var before = directory.Read(CsvMatchRepository.MatchesFileName);

        Assert.False(await repository.DeleteAsync(Guid.NewGuid()));
        Assert.Equal(before, directory.Read(CsvMatchRepository.MatchesFileName));
    }

    [Fact]
    public async Task DeleteAsync_WithNoFileYet_ReturnsFalseAndCreatesNone()
    {
        using var directory = new TempDataDirectory();
        var repository = new CsvMatchRepository(directory.Path_);

        Assert.False(await repository.DeleteAsync(Guid.NewGuid()));
        Assert.False(File.Exists(Path.Combine(directory.Path_, CsvMatchRepository.MatchesFileName)));
    }

    [Fact]
    public async Task DeleteAsync_KeepsLinesItCannotRead()
    {
        using var directory = new TempDataDirectory();
        var filePath = Path.Combine(directory.Path_, CsvMatchRepository.MatchesFileName);

        // A line nobody can parse as a match - edited by hand, or damaged. It is not this
        // repository's to throw away just because another match is being deleted.
        const string damaged = "not,a,match,row";
        await File.WriteAllTextAsync(filePath, Header + Environment.NewLine + damaged + Environment.NewLine);

        var repository = new CsvMatchRepository(directory.Path_);
        var doomed = NewMatch(Guid.NewGuid());

        await repository.AppendAsync(doomed);
        await repository.AppendAsync(NewMatch(Guid.NewGuid()));

        Assert.True(await repository.DeleteAsync(doomed.Id));

        var lines = NonEmptyLines(directory.Read(CsvMatchRepository.MatchesFileName));

        Assert.Contains(damaged, lines);
        Assert.Single(await repository.GetAllAsync());
    }

    [Fact]
    public async Task DeleteAsync_TheLastMatch_LeavesAFileTheNextFinishCanStillAppendTo()
    {
        using var directory = new TempDataDirectory();
        var repository = new CsvMatchRepository(directory.Path_);

        var only = NewMatch(Guid.NewGuid());
        await repository.AppendAsync(only);

        Assert.True(await repository.DeleteAsync(only.Id));
        Assert.Empty(await repository.GetAllAsync());

        // The header survives the delete, so the next match lands under it rather than in a
        // file whose first row no reader can name.
        var next = NewMatch(Guid.NewGuid());
        await repository.AppendAsync(next);

        var lines = NonEmptyLines(directory.Read(CsvMatchRepository.MatchesFileName));

        Assert.Equal(Header, lines[0]);
        Assert.Single(lines, line => line == Header);
        Assert.Equal(next.Id, Assert.Single(await repository.GetAllAsync()).Id);
    }
}
