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
    /// <returns>
    /// What became of every row: how many players were added, and how many were skipped for
    /// each of the reasons a row can be skipped.
    /// </returns>
    Task<PlayerImportResult> ImportPlayersAsync(string csvContent);
}
