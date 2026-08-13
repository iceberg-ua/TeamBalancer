namespace TeamBalancer.Core.Tests.TestSupport;

using TeamBalancer.Core.Models;

/// <summary>
/// Factory helpers for building players in tests. Names are kept short because
/// <see cref="Player.IsNameValid"/> caps them at <see cref="CsvSafeName.MaxLength"/> characters.
/// </summary>
internal static class TestPlayers
{
    /// <summary>
    /// Creates a single player. Skills default to a middling 2/2/2 so that tests which only
    /// care about positions produce zero skill variance.
    /// </summary>
    public static Player Create(
        string name,
        Position primary = Position.Unspecified,
        Position? secondary = null,
        int speed = 2,
        int technical = 2,
        int stamina = 2)
    {
        return new Player
        {
            Name = name,
            Speed = speed,
            TechnicalSkills = technical,
            Stamina = stamina,
            PrimaryPosition = primary,
            SecondaryPosition = secondary
        };
    }

    /// <summary>
    /// Creates <paramref name="count"/> players sharing a primary position, named
    /// "{prefix}1", "{prefix}2" and so on.
    /// </summary>
    public static List<Player> CreateMany(
        int count,
        Position primary,
        string prefix,
        int speed = 2,
        int technical = 2,
        int stamina = 2)
    {
        return Enumerable.Range(1, count)
            .Select(i => Create($"{prefix}{i}", primary, speed: speed, technical: technical, stamina: stamina))
            .ToList();
    }

    /// <summary>
    /// Counts players on a team whose primary position matches.
    /// </summary>
    public static int CountAt(this Team team, Position position)
    {
        return team.Players.Count(p => p.PrimaryPosition == position);
    }
}
