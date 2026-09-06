namespace TeamBalancer.Core.Services.Interfaces;

/// <summary>
/// A player repository that reads and writes whichever list is currently active, and can be
/// pointed at a different one. Screens that only care about players keep injecting
/// <see cref="IPlayerRepository"/> and never learn that lists exist; the list switcher injects
/// this instead.
/// </summary>
public interface IActivePlayerRepository : IPlayerRepository
{
    /// <summary>
    /// Gets the identifier of the list currently being read and written, once something has
    /// resolved which list that is. It is <see cref="Guid.Empty"/> before then, so a screen
    /// that reads it without having read players first gets an answer that matches no list.
    /// Use <see cref="GetCurrentListIdAsync"/> unless the screen is already holding players.
    /// </summary>
    Guid CurrentListId { get; }

    /// <summary>
    /// Resolves which list is active, if that has not happened yet, and gets its identifier.
    /// </summary>
    /// <remarks>
    /// The answer to <see cref="CurrentListId"/> without the order of arrival mattering. A
    /// screen reached before the home screen has loaded - a deep link, or a restart landing on
    /// the route it was left on - has nothing to make the repository resolve, and asking for
    /// the identifier is a poor reason to read a whole squad's players.
    /// </remarks>
    /// <returns>The identifier of the active list.</returns>
    Task<Guid> GetCurrentListIdAsync();

    /// <summary>
    /// Raised after the active list changes, so mounted screens can reload their players.
    /// </summary>
    event Action? ListChanged;

    /// <summary>
    /// Makes another list the active one. Pending changes to the outgoing list are saved
    /// first, so selections made since the app started survive the switch.
    /// </summary>
    /// <param name="listId">The unique identifier of the list to switch to.</param>
    Task SwitchListAsync(Guid listId);

    /// <summary>
    /// Deletes a list, switching away from it first when it is the active one.
    /// </summary>
    /// <remarks>
    /// Deleting the active list has to switch before the row and file are removed, and the two
    /// steps are kept together here rather than left to each caller: a caller that got the
    /// order wrong would leave this repository pointing at a file that no longer exists.
    /// </remarks>
    /// <param name="listId">The unique identifier of the list to delete.</param>
    Task DeleteListAsync(Guid listId);
}
