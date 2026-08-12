namespace TeamBalancer.Core.Services.Interfaces;

using TeamBalancer.Core.Models;

/// <summary>
/// Defines the contract for the store of player lists - the named lists themselves, not the
/// players in them. Every list owns a separate player file, which this repository creates and
/// removes alongside the metadata it keeps.
/// </summary>
public interface IPlayerListRepository
{
    /// <summary>
    /// Retrieves every player list, oldest first.
    /// </summary>
    /// <returns>A collection of all lists, ordered by creation date.</returns>
    Task<IEnumerable<PlayerListInfo>> GetAllAsync();

    /// <summary>
    /// Retrieves a list by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the list.</param>
    /// <returns>The list if found, null otherwise.</returns>
    Task<PlayerListInfo?> GetByIdAsync(Guid id);

    /// <summary>
    /// Creates a new, empty player list.
    /// </summary>
    /// <param name="name">The name for the new list.</param>
    /// <returns>The created list.</returns>
    Task<PlayerListInfo> AddAsync(string name);

    /// <summary>
    /// Renames an existing list. This is metadata only - the list's players are untouched.
    /// </summary>
    /// <param name="id">The unique identifier of the list to rename.</param>
    /// <param name="newName">The new name for the list.</param>
    /// <returns>The renamed list.</returns>
    Task<PlayerListInfo> RenameAsync(Guid id, string newName);

    /// <summary>
    /// Deletes the list's metadata row and its backing CSV file (unless it is the
    /// legacy-named default list - see implementation notes). Throws
    /// InvalidOperationException if this is the last remaining list.
    /// </summary>
    /// <param name="id">The unique identifier of the list to delete.</param>
    Task DeleteAsync(Guid id);
}
