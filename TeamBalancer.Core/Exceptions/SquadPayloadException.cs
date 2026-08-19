namespace TeamBalancer.Core.Exceptions;

/// <summary>
/// Thrown when text scanned from a QR code is not a squad this app can read - a code belonging
/// to something else entirely, a payload damaged in transit, or one written by a newer version
/// of the app than the one scanning it.
/// </summary>
public class SquadPayloadException : Exception
{
    /// <summary>
    /// Initializes a new instance of the SquadPayloadException class.
    /// </summary>
    /// <param name="message">The message describing what was wrong with the payload.</param>
    public SquadPayloadException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the SquadPayloadException class.
    /// </summary>
    /// <param name="message">The message describing what was wrong with the payload.</param>
    /// <param name="innerException">The error that made the payload unreadable.</param>
    public SquadPayloadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
