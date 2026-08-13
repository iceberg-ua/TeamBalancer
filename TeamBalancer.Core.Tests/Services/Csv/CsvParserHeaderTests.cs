namespace TeamBalancer.Core.Tests.Services.Csv;

using Microsoft.Extensions.Logging.Abstractions;
using TeamBalancer.Core.Models;
using TeamBalancer.Core.Services.Csv;

/// <summary>
/// Covers the optional header row in <see cref="CsvParser"/>. Files the app writes always have
/// one; imported files need not, and the first player of a headerless file used to be dropped.
/// </summary>
public class CsvParserHeaderTests
{
    private const string ExportHeader = "Name,Speed,TechnicalSkills,Stamina,PrimaryPosition,SecondaryPosition";
    private const string StorageHeader = ExportHeader + ",IsSelected";
    private const string LegacyHeader = "Name,Speed,TechnicalSkills,Stamina";

    private readonly CsvParser _parser = new(NullLogger<CsvParser>.Instance);

    [Fact]
    public void ParsePlayers_NoHeader_KeepsTheFirstPlayer()
    {
        var csv = "Alice,3,2,1,Defender,Midfielder\nBob,2,2,2,Forward,\n";

        var players = _parser.ParsePlayers(csv).ToList();

        Assert.Equal(2, players.Count);
        Assert.Equal("Alice", players[0].Name);
        Assert.Equal(Position.Defender, players[0].PrimaryPosition);
        Assert.Equal("Bob", players[1].Name);
    }

    [Fact]
    public void ParsePlayers_NoHeaderSinglePlayer_ReadsThatPlayer()
    {
        var player = Assert.Single(_parser.ParsePlayers("Solo,1,2,3\n"));

        Assert.Equal("Solo", player.Name);
        Assert.Equal(1, player.Speed);
        Assert.Equal(2, player.TechnicalSkills);
        Assert.Equal(3, player.Stamina);
    }

    [Fact]
    public void ParsePlayers_NoHeaderLegacyFourColumns_KeepsTheFirstPlayer()
    {
        var players = _parser.ParsePlayers("Alice,3,2,1\nBob,2,2,2\n").ToList();

        Assert.Equal(2, players.Count);
        Assert.Equal("Alice", players[0].Name);
    }

    [Theory]
    [InlineData(ExportHeader)]
    [InlineData(StorageHeader)]
    [InlineData(LegacyHeader)]
    public void ParsePlayers_HeaderPresent_IsStillSkipped(string header)
    {
        var csv = $"{header}\nAlice,3,2,1\n";

        var player = Assert.Single(_parser.ParsePlayers(csv));

        Assert.Equal("Alice", player.Name);
    }

    [Fact]
    public void ParsePlayers_HeaderWithLeadingSpaces_IsStillSkipped()
    {
        var csv = "Name, Speed, TechnicalSkills, Stamina\nAlice, 3, 2, 1\n";

        var player = Assert.Single(_parser.ParsePlayers(csv));

        Assert.Equal("Alice", player.Name);
    }

    [Fact]
    public void ParsePlayers_HeaderlessRoundTripOfSerializedPlayers_LosesNobody()
    {
        var original = new List<Player>
        {
            new() { Name = "Alice", Speed = 3, TechnicalSkills = 2, Stamina = 1, PrimaryPosition = Position.Defender },
            new() { Name = "Bob", Speed = 2, TechnicalSkills = 2, Stamina = 2 },
        };

        // Strip the header the way a user editing the export in a spreadsheet might.
        var exported = _parser.SerializePlayers(original);
        var withoutHeader = string.Join('\n', exported.Split('\n').Skip(1));

        var players = _parser.ParsePlayers(withoutHeader).ToList();

        Assert.Equal(2, players.Count);
        Assert.Equal(["Alice", "Bob"], players.Select(p => p.Name));
    }
}
