namespace TeamBalancer.Core.Tests.Models;

using TeamBalancer.Core.Models;
using TeamBalancer.Core.Tests.TestSupport;

/// <summary>
/// Covers <see cref="Player.IsPositionValid"/> (Phase 1 data model).
/// </summary>
public class PlayerPositionTests
{
    [Fact]
    public void IsPositionValid_PrimaryUnspecified_ReturnsFalse()
    {
        var player = TestPlayers.Create("Unset", Position.Unspecified);

        Assert.False(player.IsPositionValid());
    }

    [Fact]
    public void IsPositionValid_PrimaryUnspecifiedWithSecondarySet_ReturnsFalse()
    {
        // A secondary position cannot stand in for a missing primary one.
        var player = TestPlayers.Create("OnlySecond", Position.Unspecified, Position.Midfielder);

        Assert.False(player.IsPositionValid());
    }

    [Theory]
    [InlineData(Position.Goalkeeper)]
    [InlineData(Position.Defender)]
    [InlineData(Position.Midfielder)]
    [InlineData(Position.Forward)]
    public void IsPositionValid_SecondaryEqualsPrimary_ReturnsFalse(Position position)
    {
        var player = TestPlayers.Create("Duplicate", position, position);

        Assert.False(player.IsPositionValid());
    }

    [Theory]
    [InlineData(Position.Goalkeeper, Position.Defender)]
    [InlineData(Position.Defender, Position.Midfielder)]
    [InlineData(Position.Midfielder, Position.Forward)]
    [InlineData(Position.Forward, Position.Goalkeeper)]
    public void IsPositionValid_DistinctPrimaryAndSecondary_ReturnsTrue(Position primary, Position secondary)
    {
        var player = TestPlayers.Create("Distinct", primary, secondary);

        Assert.True(player.IsPositionValid());
    }

    [Theory]
    [InlineData(Position.Goalkeeper)]
    [InlineData(Position.Defender)]
    [InlineData(Position.Midfielder)]
    [InlineData(Position.Forward)]
    public void IsPositionValid_PrimarySetSecondaryNull_ReturnsTrue(Position primary)
    {
        var player = TestPlayers.Create("PrimaryOnly", primary, secondary: null);

        Assert.True(player.IsPositionValid());
    }

    [Fact]
    public void NewPlayer_DefaultsToUnspecifiedPrimaryAndNullSecondary()
    {
        // Data predating position support must land on these defaults.
        var player = new Player { Name = "Fresh" };

        Assert.Equal(Position.Unspecified, player.PrimaryPosition);
        Assert.Null(player.SecondaryPosition);
    }
}
