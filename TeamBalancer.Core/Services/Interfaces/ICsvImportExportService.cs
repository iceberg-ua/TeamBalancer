namespace TeamBalancer.Core.Services.Interfaces;

using TeamBalancer.Core.Models;

/// <summary>
/// Defines the contract for CSV import and export operations.
/// </summary>
public interface ICsvImportExportService
{
    /// <summary>
    /// Exports all active players to CSV format.
    /// </summary>
    /// <returns>CSV content as a string.</returns>
    Task<string> ExportPlayersAsync();

    /// <summary>
    /// Imports players from CSV content.
    /// </summary>
    /// <param name="csvContent">The CSV content to import.</param>
    /// <returns>Number of players imported successfully.</returns>
    Task<int> ImportPlayersAsync(string csvContent);
}
