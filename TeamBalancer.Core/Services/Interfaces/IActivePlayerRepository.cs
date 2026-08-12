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
    /// Gets the identifier of the list currently being read and written.
    /// </summary>
    Guid CurrentListId { get; }

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
