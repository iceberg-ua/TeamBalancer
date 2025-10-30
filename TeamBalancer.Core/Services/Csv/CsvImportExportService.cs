namespace TeamBalancer.Core.Services.Csv;

using TeamBalancer.Core.Models;
using TeamBalancer.Core.Services.Interfaces;

/// <summary>
/// Implements CSV import and export operations for players.
/// </summary>
public class CsvImportExportService : ICsvImportExportService
{
    private readonly IPlayerRepository _playerRepository;
    private readonly ICsvParser _csvParser;

    public CsvImportExportService(IPlayerRepository playerRepository, ICsvParser csvParser)
    {
        _playerRepository = playerRepository ?? throw new ArgumentNullException(nameof(playerRepository));
        _csvParser = csvParser ?? throw new ArgumentNullException(nameof(csvParser));
    }

    /// <summary>
    /// Exports all active players to CSV format.
    /// </summary>
    public async Task<string> ExportPlayersAsync()
    {
        var players = await _playerRepository.GetAllAsync();
        return _csvParser.SerializePlayers(players);
    }

    /// <summary>
    /// Imports players from CSV content, adding new players to the repository.
    /// </summary>
    public async Task<int> ImportPlayersAsync(string csvContent)
    {
        if (string.IsNullOrWhiteSpace(csvContent))
            throw new ArgumentException("CSV content cannot be empty.", nameof(csvContent));

        var players = _csvParser.ParsePlayers(csvContent);
        int importedCount = 0;

        foreach (var player in players)
        {
            try
            {
                // Validate player before adding
                if (player.IsNameValid() && player.AreSkillLevelsValid())
                {
                    await _playerRepository.AddAsync(player);
                    importedCount++;
                }
            }
            catch
            {
                // Skip players that fail validation or already exist
                continue;
            }
        }

        // Save all changes at once
        if (importedCount > 0)
        {
            await _playerRepository.SaveChangesAsync();
        }

        return importedCount;
    }
}
