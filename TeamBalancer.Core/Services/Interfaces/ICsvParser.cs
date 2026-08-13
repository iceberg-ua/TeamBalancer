namespace TeamBalancer.Core.Services.Interfaces;

using TeamBalancer.Core.Models;

/// <summary>
/// Defines the contract for CSV parsing operations.
/// </summary>
public interface ICsvParser
{
    /// <summary>
    /// Parses CSV content into a collection of Player objects.
    /// Only the first four columns (Name,Speed,TechnicalSkills,Stamina) are required, so CSVs
    /// written before position support are still accepted.
    /// </summary>
    /// <param name="csvContent">The CSV content as a string.</param>
    /// <returns>A collection of parsed players.</returns>
    IEnumerable<Player> ParsePlayers(string csvContent);

    /// <summary>
    /// Parses CSV content the same way <see cref="ParsePlayers"/> does, additionally reporting
    /// how many rows were dropped as unreadable. Import uses this so it can account for every
    /// row of the user's file; storage loading does not care and uses the simpler overload.
    /// </summary>
    /// <param name="csvContent">The CSV content as a string.</param>
    /// <returns>The players parsed, and the number of rows that could not be read.</returns>
    CsvParseResult ParsePlayersWithDiagnostics(string csvContent);

    /// <summary>
    /// Serializes a collection of Player objects into CSV format:
    /// Name,Speed,TechnicalSkills,Stamina,PrimaryPosition,SecondaryPosition[,IsSelected]
    /// </summary>
    /// <param name="players">The players to serialize.</param>
    /// <param name="includeSelection">Whether to include the IsSelected column (for storage only, not for export).</param>
    /// <returns>CSV formatted string.</returns>
    string SerializePlayers(IEnumerable<Player> players, bool includeSelection = false);
}
