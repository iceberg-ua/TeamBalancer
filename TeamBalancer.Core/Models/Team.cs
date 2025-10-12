namespace TeamBalancer.Core.Models;

/// <summary>
/// Represents a football team with its assigned players and calculated statistics.
/// </summary>
public class Team
{
    /// <summary>
    /// Initializes a new instance of the Team class with default values.
    /// </summary>
    public Team()
    {
        Id = Guid.NewGuid();
        Players = [];
    }

    /// <summary>
    /// Gets or sets the unique identifier for the team.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the team name (e.g., "Team A", "Team B").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the list of players assigned to this team.
    /// </summary>
    public List<Player> Players { get; set; }

    /// <summary>
    /// Gets the total number of players in the team.
    /// </summary>
    public int PlayerCount => Players.Count;

    /// <summary>
    /// Gets the average speed level of all players in the team.
    /// </summary>
    public double AverageSpeed => Players.Count > 0
        ? Players.Average(p => p.Speed)
        : 0;

    /// <summary>
    /// Gets the average technical skills level of all players in the team.
    /// </summary>
    public double AverageTechnicalSkills => Players.Count > 0
        ? Players.Average(p => p.TechnicalSkills)
        : 0;

    /// <summary>
    /// Gets the average stamina level of all players in the team.
    /// </summary>
    public double AverageStamina => Players.Count > 0
        ? Players.Average(p => p.Stamina)
        : 0;

    /// <summary>
    /// Gets the overall team skill level calculated as the average of all players' overall skill levels.
    /// </summary>
    public double OverallTeamSkill => Players.Count > 0
        ? Players.Average(p => p.OverallSkillLevel)
        : 0;

    /// <summary>
    /// Gets the total combined skill points of all players in the team.
    /// </summary>
    public double TotalSkillPoints => Players.Sum(p => p.OverallSkillLevel);

    /// <summary>
    /// Adds a player to the team.
    /// </summary>
    /// <param name="player">The player to add.</param>
    public void AddPlayer(Player player)
    {
        if (player != null && !Players.Contains(player))
        {
            Players.Add(player);
        }
    }

    /// <summary>
    /// Removes a player from the team.
    /// </summary>
    /// <param name="player">The player to remove.</param>
    /// <returns>True if the player was removed, false otherwise.</returns>
    public bool RemovePlayer(Player player)
    {
        return Players.Remove(player);
    }

    /// <summary>
    /// Clears all players from the team.
    /// </summary>
    public void ClearPlayers()
    {
        Players.Clear();
    }

    /// <summary>
    /// Returns a string representation of the team.
    /// </summary>
    public override string ToString()
    {
        return $"{Name} - {PlayerCount} players (Overall: {OverallTeamSkill:F2})";
    }
}
