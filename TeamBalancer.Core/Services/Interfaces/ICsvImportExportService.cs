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
    /// Imports players from CSV content into the active list.
    /// </summary>
    /// <param name="csvContent">The CSV content to import.</param>
    /// <param name="mode">
    /// What to do about players the list already holds. Defaults to
    /// <see cref="ImportMode.AddOnly"/>, which is right for a list that was just created for
    /// this import and has nothing to collide with.
    /// </param>
    /// <returns>
    /// What became of every row: how many players were added, how many already in the list
    /// were updated or left unchanged, and how many were skipped for each of the reasons a row
    /// can be skipped.
    /// </returns>
    Task<PlayerImportResult> ImportPlayersAsync(string csvContent, ImportMode mode = ImportMode.AddOnly);
}
