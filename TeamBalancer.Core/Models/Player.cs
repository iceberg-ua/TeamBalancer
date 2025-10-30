namespace TeamBalancer.Core.Models;

/// <summary>
/// Represents a football player with their personal information and skill rating.
/// </summary>
public class Player
{
    /// <summary>
    /// Gets or sets the unique identifier for the player.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the player's name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the player's speed level (1-3 scale).
    /// 1 = Low, 2 = Medium, 3 = High
    /// </summary>
    public int Speed { get; set; }

    /// <summary>
    /// Gets or sets the player's technical skills level (1-3 scale).
    /// 1 = Low, 2 = Medium, 3 = High
    /// </summary>
    public int TechnicalSkills { get; set; }

    /// <summary>
    /// Gets or sets the player's stamina level (1-3 scale).
    /// 1 = Low, 2 = Medium, 3 = High
    /// </summary>
    public int Stamina { get; set; }

    /// <summary>
    /// Gets the overall skill level calculated as the average of all skill attributes.
    /// </summary>
    public double OverallSkillLevel => (Speed + TechnicalSkills + Stamina) / 3.0;

    /// <summary>
    /// Gets or sets the date when the player was added to the system.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the date when the player information was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets whether the player is currently active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Initializes a new instance of the Player class with default values.
    /// </summary>
    public Player()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
        IsActive = true;
    }

    /// <summary>
    /// Validates that all skill levels are within the valid range (1-3).
    /// </summary>
    /// <returns>True if all skill levels are valid, false otherwise.</returns>
    public bool AreSkillLevelsValid()
    {
        return Speed >= 1 && Speed <= 3 &&
               TechnicalSkills >= 1 && TechnicalSkills <= 3 &&
               Stamina >= 1 && Stamina <= 3;
    }

    /// <summary>
    /// Validates that the player name doesn't contain invalid characters that would break CSV format.
    /// </summary>
    /// <returns>True if the name is valid, false otherwise.</returns>
    public bool IsNameValid()
    {
        return !string.IsNullOrWhiteSpace(Name) && !Name.Contains(',');
    }

    /// <summary>
    /// Returns a string representation of the player.
    /// </summary>
    public override string ToString()
    {
        return $"{Name} (Speed: {Speed}, Tech: {TechnicalSkills}, Stamina: {Stamina}, Overall: {OverallSkillLevel:F1})";
    }
}
