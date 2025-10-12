namespace TeamBalancer.Core.Services.Interfaces;

using TeamBalancer.Core.Models;

/// <summary>
/// Defines the contract for CSV parsing operations.
/// </summary>
public interface ICsvParser
{
    /// <summary>
    /// Parses CSV content into a collection of Player objects.
    /// </summary>
    /// <param name="csvContent">The CSV content as a string.</param>
    /// <returns>A collection of parsed players.</returns>
    IEnumerable<Player> ParsePlayers(string csvContent);

    /// <summary>
    /// Serializes a collection of Player objects into CSV format.
    /// </summary>
    /// <param name="players">The players to serialize.</param>
    /// <returns>CSV formatted string.</returns>
    string SerializePlayers(IEnumerable<Player> players);
}
