namespace TeamBalancer.Core.Models;

/// <summary>
/// Represents the position a football player occupies on the pitch.
/// </summary>
public enum Position
{
    /// <summary>
    /// No position has been assigned. Placeholder value used only for data that predates
    /// position support; it is never a valid user-facing choice.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// Goalkeeper.
    /// </summary>
    Goalkeeper = 1,

    /// <summary>
    /// Defender.
    /// </summary>
    Defender = 2,

    /// <summary>
    /// Midfielder.
    /// </summary>
    Midfielder = 3,

    /// <summary>
    /// Forward.
    /// </summary>
    Forward = 4
}
