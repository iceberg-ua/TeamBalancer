namespace TeamBalancer.Core.Services.Interfaces;

/// <summary>
/// Remembers which player list the user was last working with, across restarts. Implemented by
/// the host app, which knows where local settings belong on its platform.
/// </summary>
public interface ICurrentListPreference
{
    /// <summary>
    /// Reads the stored list identifier.
    /// </summary>
    /// <returns>The id of the list the user last switched to, or null when they never have.</returns>
    Guid? Read();

    /// <summary>
    /// Stores the identifier of the list the user switched to.
    /// </summary>
    /// <param name="listId">The list's unique identifier.</param>
    void Write(Guid listId);
}
