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
    /// Imports players from CSV content, adding new players to the repository. Rows that cannot
    /// be imported are counted by reason rather than merely dropped, so the caller can tell the
    /// user what happened to the rest of their file.
    /// </summary>
    public async Task<PlayerImportResult> ImportPlayersAsync(string csvContent, ImportMode mode = ImportMode.AddOnly)
    {
        if (string.IsNullOrWhiteSpace(csvContent))
            throw new ArgumentException("CSV content cannot be empty.", nameof(csvContent));

        _logger.LogInformation("Starting player import from CSV in {Mode} mode", mode);

        var parsed = _csvParser.ParsePlayersWithDiagnostics(csvContent);
        int importedCount = 0;
        int invalidNameCount = 0;
        int invalidSkillsCount = 0;
        int duplicateCount = 0;
        int updatedCount = 0;
        int unchangedCount = 0;
        int errorCount = 0;
        int truncatedCount = 0;
        int numberedCount = 0;

        foreach (var player in parsed.Players)
        {
            try
            {
                // A name that is merely too long is shortened rather than dropped: losing the
                // player entirely is a worse answer than losing the tail of their name, which
                // they can edit afterwards. Re-validating is what keeps this narrow - a name
                // rejected for anything else is still rejected once shortened.
                var wasTruncated = false;
                var wasNumbered = false;
                if (!player.IsNameValid() && CsvSafeName.IsValid(CsvSafeName.Truncate(player.Name)))
                {
                    _logger.LogInformation("Shortening player name '{OriginalName}' to fit the {MaxLength} character limit",
                        player.Name, CsvSafeName.MaxLength);
                    player.Name = CsvSafeName.Truncate(player.Name);
                    wasTruncated = true;
                }

                // Validate player before adding
                if (!player.IsNameValid())
                {
                    _logger.LogWarning("Skipping player with invalid name: '{PlayerName}'", player.Name);
                    invalidNameCount++;
                    continue;
                }

                if (!player.AreSkillLevelsValid())
                {
                    _logger.LogWarning("Skipping player '{PlayerName}' with invalid skill levels: Speed={Speed}, Technical={Technical}, Stamina={Stamina}",
                        player.Name, player.Speed, player.TechnicalSkills, player.Stamina);
                    invalidSkillsCount++;
                    continue;
                }

                // Check if player with same name already exists
                var existingPlayer = await _playerRepository.GetByNameAsync(player.Name);
                if (existingPlayer != null)
                {
                    // Two long names sharing their opening characters shorten to the same thing,
                    // so the collision is one this import created rather than a player the file
                    // genuinely repeats. Numbering them keeps both. A name that was not
                    // shortened is a real duplicate and is still skipped.
                    var distinctName = wasTruncated
                        ? await FindDistinctNameAsync(player.Name)
                        : null;

                    if (distinctName is null)
                    {
                        // The row and the player in the list are the same person. Under
                        // AddOnly that is the end of it; a merge takes the sender's ratings,
                        // which is the whole point of receiving a squad a second time.
                        if (mode == ImportMode.AddOnly)
                        {
                            _logger.LogWarning("Skipping player '{PlayerName}' - a player with this name already exists", player.Name);
                            duplicateCount++;
                            continue;
                        }

                        if (ApplyTo(existingPlayer, player))
                        {
                            await _playerRepository.UpdateAsync(existingPlayer);
                            _logger.LogDebug("Updated player '{PlayerName}' from the import", existingPlayer.Name);
                            updatedCount++;
                        }
                        else
                        {
                            unchangedCount++;
                        }

                        continue;
                    }

                    _logger.LogInformation("Numbering shortened name '{PlayerName}' as '{DistinctName}' to keep it apart from the player already in the list",
                        player.Name, distinctName);
                    player.Name = distinctName;
                    wasNumbered = true;
                }

                // Imported players default to deselected
                player.IsSelected = false;
                await _playerRepository.AddAsync(player);
                _logger.LogDebug("Successfully imported player '{PlayerName}'", player.Name);
                importedCount++;

                // Counted only now: a shortened name that then collided with an existing player
                // was skipped as a duplicate, and reporting it as shortened would overstate what
                // actually changed in the list.
                if (wasTruncated)
                {
                    truncatedCount++;
                }

                if (wasNumbered)
                {
                    numberedCount++;
                }
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Validation error for player '{PlayerName}': {ErrorMessage}",
                    player.Name, ex.Message);
                errorCount++;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Player '{PlayerName}' already exists or operation invalid: {ErrorMessage}",
                    player.Name, ex.Message);
                errorCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error importing player '{PlayerName}': {ErrorMessage}",
                    player.Name, ex.Message);
                errorCount++;
            }
        }

        var result = new PlayerImportResult
        {
            ImportedCount = importedCount,
            UnreadableCount = parsed.UnreadableRowCount,
            InvalidNameCount = invalidNameCount,
            // The parser rejects out-of-range skills before a player ever reaches the loop
            // above, so both counts have to be added up to account for every such row.
            InvalidSkillsCount = parsed.InvalidSkillRowCount + invalidSkillsCount,
            DuplicateCount = duplicateCount,
            UpdatedCount = updatedCount,
            UnchangedCount = unchangedCount,
            ErrorCount = errorCount,
            TruncatedCount = truncatedCount,
            NumberedCount = numberedCount
        };

        // Save all changes at once. Updates count as changes too - a merge that only adjusted
        // ratings adds no players, and saving only on ImportedCount would drop every one of
        // them the next time the list is loaded.
        if (importedCount > 0 || updatedCount > 0)
        {
            await _playerRepository.SaveChangesAsync();
            _logger.LogInformation("Player import completed: {ImportedCount} added, {UpdatedCount} updated, {SkippedCount} skipped",
                result.ImportedCount, result.UpdatedCount, result.SkippedCount);
        }
        else
        {
            _logger.LogWarning("Player import completed: nothing changed, {SkippedCount} skipped",
                result.SkippedCount);
        }

        return result;
    }

    /// <summary>
    /// Copies an imported player's ratings and positions onto the player already in the list.
    /// The name is deliberately left alone: it is what matched the two in the first place, and
    /// the one in the list may differ in case or spacing from the one in the file.
    /// </summary>
    /// <param name="existing">The player already in the list, updated in place.</param>
    /// <param name="incoming">The player the import carried.</param>
    /// <returns>True if anything actually changed, false if the two already agreed.</returns>
    private static bool ApplyTo(Player existing, Player incoming)
    {
        var changed = existing.Speed != incoming.Speed
            || existing.TechnicalSkills != incoming.TechnicalSkills
            || existing.Stamina != incoming.Stamina
            || existing.PrimaryPosition != incoming.PrimaryPosition
            || existing.SecondaryPosition != incoming.SecondaryPosition;

        if (!changed)
        {
            return false;
        }

        existing.Speed = incoming.Speed;
        existing.TechnicalSkills = incoming.TechnicalSkills;
        existing.Stamina = incoming.Stamina;
        existing.PrimaryPosition = incoming.PrimaryPosition;
        existing.SecondaryPosition = incoming.SecondaryPosition;

        return true;
    }

    /// <summary>
    /// Finds a free variant of a shortened name by replacing its last character with a digit,
    /// so two players whose full names differ only past the length limit can both be kept.
    /// </summary>
    /// <param name="truncatedName">The shortened name that is already taken.</param>
    /// <returns>
    /// A name no player holds yet, or null when every digit is taken - at which point the row
    /// really is indistinguishable from one already in the list and is better skipped than
    /// imported under a name that says nothing about who it is.
    /// </returns>
    private async Task<string?> FindDistinctNameAsync(string truncatedName)
    {
        // One character shorter, leaving exactly the room the digit needs.
        var stem = CsvSafeName.Truncate(truncatedName, CsvSafeName.MaxLength - 1);

        // Starts at 2: the player already holding the plain shortened name is the first one.
        for (var suffix = 2; suffix <= 9; suffix++)
        {
            var candidate = stem + suffix;

            if (!CsvSafeName.IsValid(candidate))
            {
                continue;
            }

            if (await _playerRepository.GetByNameAsync(candidate) is null)
            {
                return candidate;
            }
        }

        return null;
    }
}
