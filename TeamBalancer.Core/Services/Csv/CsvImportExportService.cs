namespace TeamBalancer.Core.Services.Csv;

using Microsoft.Extensions.Logging;
using TeamBalancer.Core.Exceptions;
using TeamBalancer.Core.Models;
using TeamBalancer.Core.Services.Interfaces;

/// <summary>
/// Implements CSV import and export operations for players.
/// </summary>
public class CsvImportExportService : ICsvImportExportService
{
    private readonly IPlayerRepository _playerRepository;
    private readonly ICsvParser _csvParser;
    private readonly ILogger<CsvImportExportService> _logger;

    public CsvImportExportService(
        IPlayerRepository playerRepository,
        ICsvParser csvParser,
        ILogger<CsvImportExportService> logger)
    {
        _playerRepository = playerRepository ?? throw new ArgumentNullException(nameof(playerRepository));
        _csvParser = csvParser ?? throw new ArgumentNullException(nameof(csvParser));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

        _logger.LogInformation("Starting player import from CSV");

        var players = _csvParser.ParsePlayers(csvContent);
        int importedCount = 0;
        int skippedCount = 0;

        foreach (var player in players)
        {
            try
            {
                // Validate player before adding
                if (!player.IsNameValid())
                {
                    _logger.LogWarning("Skipping player with invalid name: '{PlayerName}'", player.Name);
                    skippedCount++;
                    continue;
                }

                if (!player.AreSkillLevelsValid())
                {
                    _logger.LogWarning("Skipping player '{PlayerName}' with invalid skill levels: Speed={Speed}, Technical={Technical}, Stamina={Stamina}",
                        player.Name, player.Speed, player.TechnicalSkills, player.Stamina);
                    skippedCount++;
                    continue;
                }

                await _playerRepository.AddAsync(player);
                _logger.LogDebug("Successfully imported player '{PlayerName}'", player.Name);
                importedCount++;
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Validation error for player '{PlayerName}': {ErrorMessage}",
                    player.Name, ex.Message);
                skippedCount++;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Player '{PlayerName}' already exists or operation invalid: {ErrorMessage}",
                    player.Name, ex.Message);
                skippedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error importing player '{PlayerName}': {ErrorMessage}",
                    player.Name, ex.Message);
                skippedCount++;
            }
        }

        // Save all changes at once
        if (importedCount > 0)
        {
            await _playerRepository.SaveChangesAsync();
            _logger.LogInformation("Player import completed: {ImportedCount} players imported, {SkippedCount} skipped",
                importedCount, skippedCount);
        }
        else
        {
            _logger.LogWarning("Player import completed: No players were imported, {SkippedCount} skipped",
                skippedCount);
        }

        return importedCount;
    }
}
