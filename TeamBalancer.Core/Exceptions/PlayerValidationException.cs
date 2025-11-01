namespace TeamBalancer.Core.Exceptions;

using TeamBalancer.Core.Models;

/// <summary>
/// Exception thrown when player validation fails.
/// </summary>
public class PlayerValidationException : Exception
{
    /// <summary>
    /// Gets the player that failed validation.
    /// </summary>
    public Player? Player { get; }

    /// <summary>
    /// Gets the validation error type.
    /// </summary>
    public ValidationErrorType ErrorType { get; }

    public PlayerValidationException(string message) : base(message)
    {
    }

    public PlayerValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public PlayerValidationException(string message, Player? player, ValidationErrorType errorType)
        : base(message)
    {
        Player = player;
        ErrorType = errorType;
    }
}

/// <summary>
/// Types of player validation errors.
/// </summary>
public enum ValidationErrorType
{
    InvalidName,
    InvalidSkillLevels,
    DuplicatePlayer,
    Other
}
