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
    /// Gets or sets the player's main position on the pitch.
    /// Defaults to <see cref="Position.Unspecified"/> for data that predates position support.
    /// </summary>
    public Position PrimaryPosition { get; set; } = Position.Unspecified;

    /// <summary>
    /// Gets or sets the player's optional fallback position.
    /// Null when the player has no secondary position.
    /// </summary>
    public Position? SecondaryPosition { get; set; }

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
    /// Gets or sets whether the player is selected for team creation.
    /// Persisted to CSV storage but excluded from import/export.
    /// </summary>
    public bool IsSelected { get; set; }

    /// <summary>
    /// Initializes a new instance of the Player class with default values.
    /// </summary>
    public Player()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
        IsActive = true;
        IsSelected = true;
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
    /// Validates that the player has a real primary position and that the secondary position,
    /// when set, differs from it.
    /// </summary>
    /// <returns>True if the positions are valid, false otherwise.</returns>
    public bool IsPositionValid()
    {
        if (PrimaryPosition == Position.Unspecified)
        {
            return false;
        }

        if (SecondaryPosition.HasValue && SecondaryPosition.Value == PrimaryPosition)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates that the player name doesn't contain invalid characters that would break CSV format
    /// or allow CSV injection attacks. The rules themselves live in <see cref="CsvSafeName"/>,
    /// which player list names are held to as well.
    /// </summary>
    /// <returns>True if the name is valid, false otherwise.</returns>
    public bool IsNameValid() => CsvSafeName.IsValid(Name);

    /// <summary>
    /// Returns a string representation of the player.
    /// </summary>
    public override string ToString()
    {
        return $"{Name} (Speed: {Speed}, Tech: {TechnicalSkills}, Stamina: {Stamina}, Overall: {OverallSkillLevel:F1})";
    }
}
