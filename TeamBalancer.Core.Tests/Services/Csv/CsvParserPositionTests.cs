namespace TeamBalancer.Core.Tests.Services.Csv;

using Microsoft.Extensions.Logging.Abstractions;
using TeamBalancer.Core.Models;
using TeamBalancer.Core.Services.Csv;
using TeamBalancer.Core.Tests.TestSupport;

/// <summary>
/// Covers position parsing and serialization in <see cref="CsvParser"/> (Phase 1).
/// </summary>
public class CsvParserPositionTests
{
    private const string ExportHeader = "Name,Speed,TechnicalSkills,Stamina,PrimaryPosition,SecondaryPosition";
    private const string StorageHeader = ExportHeader + ",IsSelected";
    private const string LegacyHeader = "Name,Speed,TechnicalSkills,Stamina";

    private readonly CsvParser _parser = new(NullLogger<CsvParser>.Instance);

    [Fact]
    public void ParsePlayers_ExportFormat_ReadsBothPositionColumns()
    {
        var csv = $"{ExportHeader}\nKeeper,3,2,1,Goalkeeper,Defender\n";

        var player = Assert.Single(_parser.ParsePlayers(csv));

        Assert.Equal("Keeper", player.Name);
        Assert.Equal(Position.Goalkeeper, player.PrimaryPosition);
        Assert.Equal(Position.Defender, player.SecondaryPosition);
    }

    [Fact]
    public void ParsePlayers_StorageFormat_ReadsPositionsAndSelection()
    {
        var csv = $"{StorageHeader}\nWinger,3,3,2,Forward,Midfielder,False\n";

        var player = Assert.Single(_parser.ParsePlayers(csv));

        Assert.Equal(Position.Forward, player.PrimaryPosition);
        Assert.Equal(Position.Midfielder, player.SecondaryPosition);
        Assert.False(player.IsSelected);
    }

    [Fact]
    public void ParsePlayers_EmptySecondaryColumn_LeavesSecondaryNull()
    {
        var csv = $"{ExportHeader}\nSolo,2,2,2,Defender,\n";

        var player = Assert.Single(_parser.ParsePlayers(csv));

        Assert.Equal(Position.Defender, player.PrimaryPosition);
        Assert.Null(player.SecondaryPosition);
    }

    [Fact]
    public void ParsePlayers_PositionNamesAreCaseInsensitive()
    {
        var csv = $"{ExportHeader}\nLower,2,2,2,goalkeeper,MIDFIELDER\n";

        var player = Assert.Single(_parser.ParsePlayers(csv));

        Assert.Equal(Position.Goalkeeper, player.PrimaryPosition);
        Assert.Equal(Position.Midfielder, player.SecondaryPosition);
    }

    [Fact]
    public void ParsePlayers_LegacyFourColumnFormat_DefaultsPositionsWithoutThrowing()
    {
        // Pre-Phase-1 CSVs carry no position columns at all.
        var csv = $"{LegacyHeader}\nOldTimer,3,2,1\nSecondGuy,1,2,3\n";

        var players = _parser.ParsePlayers(csv).ToList();

        Assert.Equal(2, players.Count);
        Assert.All(players, p =>
        {
            Assert.Equal(Position.Unspecified, p.PrimaryPosition);
            Assert.Null(p.SecondaryPosition);
        });
    }

    [Fact]
    public void ParsePlayers_LegacyStorageFormat_TreatsFifthColumnAsSelection()
    {
        // Storage CSVs written before position support kept IsSelected in the 5th column.
        var csv = $"{LegacyHeader},IsSelected\nOldStored,2,2,2,False\n";

        var player = Assert.Single(_parser.ParsePlayers(csv));

        Assert.Equal(Position.Unspecified, player.PrimaryPosition);
        Assert.Null(player.SecondaryPosition);
        Assert.False(player.IsSelected);
    }

    [Fact]
    public void ParsePlayers_UnrecognisedPrimaryPosition_FallsBackToUnspecified()
    {
        var csv = $"{ExportHeader}\nSweeper,2,2,2,Sweeper,\n";

        var player = Assert.Single(_parser.ParsePlayers(csv));

        Assert.Equal(Position.Unspecified, player.PrimaryPosition);
        Assert.Null(player.SecondaryPosition);
    }

    [Fact]
    public void ParsePlayers_UnrecognisedSecondaryPosition_LeavesSecondaryNull()
    {
        var csv = $"{ExportHeader}\nOdd,2,2,2,Defender,Libero\n";

        var player = Assert.Single(_parser.ParsePlayers(csv));

        Assert.Equal(Position.Defender, player.PrimaryPosition);
        Assert.Null(player.SecondaryPosition);
    }

    [Fact]
    public void ParsePlayers_MalformedPositionValues_DoNotSkipTheRow()
    {
        // The row itself must survive: only the position data is discarded.
        var csv = $"{ExportHeader}\nBadBoth,3,1,2,???,!!!\n";

        var player = Assert.Single(_parser.ParsePlayers(csv));

        Assert.Equal("BadBoth", player.Name);
        Assert.Equal(3, player.Speed);
        Assert.Equal(1, player.TechnicalSkills);
        Assert.Equal(2, player.Stamina);
        Assert.Equal(Position.Unspecified, player.PrimaryPosition);
        Assert.Null(player.SecondaryPosition);
    }

    [Fact]
    public void SerializePlayers_WritesEnumNamesAndEmptySecondary()
    {
        var players = new List<Player>
        {
            TestPlayers.Create("Both", Position.Defender, Position.Midfielder),
            TestPlayers.Create("PrimaryOnly", Position.Forward)
        };

        var csv = _parser.SerializePlayers(players);

        Assert.Contains("Both,2,2,2,Defender,Midfielder", csv);
        Assert.Contains("PrimaryOnly,2,2,2,Forward,", csv);
    }

    [Fact]
    public void SerializePlayers_RoundTrip_PreservesPositionFields()
    {
        var original = new List<Player>
        {
            TestPlayers.Create("Keeper", Position.Goalkeeper, Position.Defender, 3, 2, 1),
            TestPlayers.Create("PrimeOnly", Position.Midfielder, secondary: null, 1, 3, 2),
            TestPlayers.Create("NoPosition", Position.Unspecified, secondary: null, 2, 2, 3),
            TestPlayers.Create("Attacker", Position.Forward, Position.Midfielder, 3, 3, 3)
        };

        var roundTripped = _parser.ParsePlayers(_parser.SerializePlayers(original)).ToList();

        Assert.Equal(original.Count, roundTripped.Count);
        for (int i = 0; i < original.Count; i++)
        {
            Assert.Equal(original[i].Name, roundTripped[i].Name);
            Assert.Equal(original[i].Speed, roundTripped[i].Speed);
            Assert.Equal(original[i].TechnicalSkills, roundTripped[i].TechnicalSkills);
            Assert.Equal(original[i].Stamina, roundTripped[i].Stamina);
            Assert.Equal(original[i].PrimaryPosition, roundTripped[i].PrimaryPosition);
            Assert.Equal(original[i].SecondaryPosition, roundTripped[i].SecondaryPosition);
        }
    }

    [Fact]
    public void SerializePlayers_StorageRoundTrip_PreservesPositionsAndSelection()
    {
        var original = TestPlayers.Create("Stored", Position.Defender, Position.Forward);
        original.IsSelected = false;

        var csv = _parser.SerializePlayers([original], includeSelection: true);
        var result = Assert.Single(_parser.ParsePlayers(csv));

        Assert.Equal(Position.Defender, result.PrimaryPosition);
        Assert.Equal(Position.Forward, result.SecondaryPosition);
        Assert.False(result.IsSelected);
    }
}
