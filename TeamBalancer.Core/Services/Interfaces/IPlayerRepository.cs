namespace TeamBalancer.Core.Services.Interfaces;

using TeamBalancer.Core.Models;

/// <summary>
/// Defines the contract for player data access operations.
/// This interface can be implemented by CSV, SQLite, or any other data source.
/// </summary>
public interface IPlayerRepository
{
    /// <summary>
    /// Retrieves all players from the data source.
    /// </summary>
    /// <returns>A collection of all players.</returns>
    Task<IEnumerable<Player>> GetAllAsync();

    /// <summary>
    /// Retrieves a player by their unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the player.</param>
    /// <returns>The player if found, null otherwise.</returns>
    Task<Player?> GetByIdAsync(Guid id);

    /// <summary>
    /// Adds a new player to the data source.
    /// </summary>
    /// <param name="player">The player to add.</param>
    /// <returns>The added player with any generated values.</returns>
    Task<Player> AddAsync(Player player);

    /// <summary>
    /// Updates an existing player in the data source.
    /// </summary>
    /// <param name="player">The player with updated values.</param>
    /// <returns>The updated player.</returns>
    Task<Player> UpdateAsync(Player player);

    /// <summary>
    /// Deletes a player from the data source.
    /// </summary>
    /// <param name="id">The unique identifier of the player to delete.</param>
    /// <returns>True if the player was deleted, false otherwise.</returns>
    Task<bool> DeleteAsync(Guid id);

    /// <summary>
    /// Saves all pending changes to the data source.
    /// </summary>
    /// <returns>The number of changes saved.</returns>
    Task<int> SaveChangesAsync();
}
