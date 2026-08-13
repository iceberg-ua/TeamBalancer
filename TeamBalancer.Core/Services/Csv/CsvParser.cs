namespace TeamBalancer.Core.Services.Csv;

using System.Text;
using Microsoft.Extensions.Logging;
using TeamBalancer.Core.Exceptions;
using TeamBalancer.Core.Models;
using TeamBalancer.Core.Services.Interfaces;

/// <summary>
/// Implements CSV parsing and serialization for Player objects.
/// </summary>
public class CsvParser : ICsvParser
{
    private readonly ILogger<CsvParser> _logger;

    public CsvParser(ILogger<CsvParser> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    /// <summary>
    /// Parses CSV content into a collection of Player objects.
    /// Expected export format: Name,Speed,TechnicalSkills,Stamina,PrimaryPosition,SecondaryPosition
    /// Expected storage format: Name,Speed,TechnicalSkills,Stamina,PrimaryPosition,SecondaryPosition,IsSelected
    /// Only the first four columns are required; CSVs written before position support
    /// (Name,Speed,TechnicalSkills,Stamina[,IsSelected]) are still parsed, with positions
    /// defaulting to Unspecified/null. Position values are parsed leniently - an unrecognised
    /// value never causes the row to be skipped.
    /// The header row is optional: a file that begins with a player is read from its first
    /// line, so an imported CSV written without titles keeps all of its players.
    /// </summary>
    public IEnumerable<Player> ParsePlayers(string csvContent) =>
        ParsePlayersWithDiagnostics(csvContent).Players;

    /// <inheritdoc />
    public CsvParseResult ParsePlayersWithDiagnostics(string csvContent)
    {
        if (string.IsNullOrWhiteSpace(csvContent))
        {
            return new CsvParseResult([], 0, 0);
        }

        var players = new List<Player>();
        var lines = csvContent.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        int unreadableRows = 0;
        int invalidSkillRows = 0;

        // Every file the app writes starts with a header, but an imported one need not: a CSV
        // typed by hand or saved out of a spreadsheet can begin straight in with a player.
        // Skipping the first line unconditionally silently cost such a file its first player,
        // so it is only skipped when it does not itself read as a player row.
        int firstDataRow = lines.Length > 0 && LooksLikePlayerRow(lines[0]) ? 0 : 1;

        for (int i = firstDataRow; i < lines.Length; i++)
        {
            var lineNumber = i + 1; // +1 for human-readable line numbers
            var line = lines[i].Trim();

            if (string.IsNullOrWhiteSpace(line))
            {
                _logger.LogDebug("Skipping empty line {LineNumber}", lineNumber);
                continue;
            }

            var parts = line.Split(',');
            if (parts.Length < 4)
            {
                _logger.LogWarning("Skipping line {LineNumber}: Expected at least 4 columns, found {ColumnCount}. Content: {LineContent}",
                    lineNumber, parts.Length, line);
                unreadableRows++;
                continue;
            }

            try
            {
                var player = new Player
                {
                    Name = parts[0].Trim(),
                    Speed = int.Parse(parts[1].Trim()),
                    TechnicalSkills = int.Parse(parts[2].Trim()),
                    Stamina = int.Parse(parts[3].Trim())
                };

                // Parse optional PrimaryPosition column (5th column).
                // Missing, empty or unrecognised values fall back to Unspecified.
                if (parts.Length >= 5 && TryParsePosition(parts[4], out var primaryPosition))
                {
                    player.PrimaryPosition = primaryPosition;
                }

                // Parse optional SecondaryPosition column (6th column). An empty value means
                // "no secondary position"; an unrecognised one is reported but not fatal.
                if (parts.Length >= 6)
                {
                    var secondaryValue = parts[5].Trim();
                    if (secondaryValue.Length > 0)
                    {
                        if (TryParsePosition(secondaryValue, out var secondaryPosition))
                        {
                            player.SecondaryPosition = secondaryPosition;
                        }
                        else
                        {
                            _logger.LogWarning("Line {LineNumber}: Unrecognised secondary position '{Value}' for player '{PlayerName}'. Leaving it unset.",
                                lineNumber, secondaryValue, player.Name);
                        }
                    }
                }

                // Parse optional IsSelected column (7th column, used in storage CSV)
                if (parts.Length >= 7 && bool.TryParse(parts[6].Trim(), out var isSelected))
                {
                    player.IsSelected = isSelected;
                }
                else if (parts.Length == 5 && bool.TryParse(parts[4].Trim(), out var legacyIsSelected))
                {
                    // Storage CSV written before position support kept IsSelected in the 5th
                    // column. A boolean there is unambiguous - no position name parses as one.
                    player.IsSelected = legacyIsSelected;
                }

                // Validate skill levels
                if (!player.AreSkillLevelsValid())
                {
                    _logger.LogWarning("Skipping line {LineNumber}: Invalid skill levels for player '{PlayerName}'. Speed={Speed}, Technical={Technical}, Stamina={Stamina}",
                        lineNumber, player.Name, player.Speed, player.TechnicalSkills, player.Stamina);
                    invalidSkillRows++;
                    continue;
                }

                players.Add(player);
            }
            catch (FormatException ex)
            {
                _logger.LogError(ex, "Skipping line {LineNumber}: Failed to parse numeric value. Content: {LineContent}",
                    lineNumber, line);
                unreadableRows++;
            }
            catch (OverflowException ex)
            {
                _logger.LogError(ex, "Skipping line {LineNumber}: Numeric value out of range. Content: {LineContent}",
                    lineNumber, line);
                unreadableRows++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Skipping line {LineNumber}: Unexpected error parsing line. Content: {LineContent}",
                    lineNumber, line);
                unreadableRows++;
            }
        }

        var skippedRows = unreadableRows + invalidSkillRows;
        if (skippedRows > 0)
        {
            _logger.LogInformation("CSV parsing completed: {ValidPlayers} players loaded, {SkippedRows} rows skipped",
                players.Count, skippedRows);
        }
        else
        {
            _logger.LogInformation("CSV parsing completed: {ValidPlayers} players loaded successfully",
                players.Count);
        }

        return new CsvParseResult(players, unreadableRows, invalidSkillRows);
    }

    /// <summary>
    /// Serializes a collection of Player objects into CSV format.
    /// Export format: Name,Speed,TechnicalSkills,Stamina,PrimaryPosition,SecondaryPosition
    /// Storage format: Name,Speed,TechnicalSkills,Stamina,PrimaryPosition,SecondaryPosition,IsSelected
    /// Positions are written as their enum names; a null secondary position is written as an
    /// empty value. Applies defense-in-depth by sanitizing values to prevent CSV injection.
    /// </summary>
    public string SerializePlayers(IEnumerable<Player> players, bool includeSelection = false)
    {
        var sb = new StringBuilder();

        // Write header
        sb.AppendLine(includeSelection
            ? "Name,Speed,TechnicalSkills,Stamina,PrimaryPosition,SecondaryPosition,IsSelected"
            : "Name,Speed,TechnicalSkills,Stamina,PrimaryPosition,SecondaryPosition");

        // Write player data
        foreach (var player in players)
        {
            // Sanitize the name to prevent CSV injection (defense-in-depth).
            // Positions come from a fixed enum, so they need no sanitization.
            string sanitizedName = SanitizeCsvValue(player.Name);
            string secondaryPosition = player.SecondaryPosition?.ToString() ?? string.Empty;
            sb.AppendLine(includeSelection
                ? $"{sanitizedName},{player.Speed},{player.TechnicalSkills},{player.Stamina},{player.PrimaryPosition},{secondaryPosition},{player.IsSelected}"
                : $"{sanitizedName},{player.Speed},{player.TechnicalSkills},{player.Stamina},{player.PrimaryPosition},{secondaryPosition}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Decides whether a line carries player data rather than column titles. The three skill
    /// columns settle it: a header spells them out, a player row has numbers in them. Testing
    /// the data rather than matching the header text means a file whose columns are titled in
    /// another language is still recognised as having a header.
    /// </summary>
    /// <param name="line">The raw first line of the file.</param>
    /// <returns>True if the line reads as a player, false if it reads as a header.</returns>
    private static bool LooksLikePlayerRow(string line)
    {
        var parts = line.Trim().Split(',');
        if (parts.Length < 4)
        {
            return false;
        }

        return int.TryParse(parts[1].Trim(), out _)
            && int.TryParse(parts[2].Trim(), out _)
            && int.TryParse(parts[3].Trim(), out _);
    }

    /// <summary>
    /// Attempts to parse a CSV cell into a <see cref="Position"/>, case-insensitively.
    /// </summary>
    /// <param name="value">The raw cell value.</param>
    /// <param name="position">The parsed position when successful.</param>
    /// <returns>True if the value maps to a defined position, false otherwise.</returns>
    private static bool TryParsePosition(string value, out Position position)
    {
        position = Position.Unspecified;

        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        return Enum.TryParse(trimmed, ignoreCase: true, out position) && Enum.IsDefined(position);
    }

    /// <summary>
    /// Sanitizes a CSV value to prevent CSV injection attacks.
    /// This is defense-in-depth - validation should already prevent bad data,
    /// but we sanitize on output as an additional security layer.
    /// </summary>
    /// <param name="value">The value to sanitize.</param>
    /// <returns>A sanitized value safe for CSV output.</returns>
    private string SanitizeCsvValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        // If the value starts with a potentially dangerous character, prepend a single quote
        // This is the standard CSV injection prevention technique
        char firstChar = value[0];
        if (firstChar == '=' || firstChar == '+' || firstChar == '-' || firstChar == '@' ||
            firstChar == '\t' || firstChar == '\r')
        {
            _logger.LogWarning("Sanitizing potentially dangerous CSV value starting with '{Char}': {Value}", firstChar, value);
            return "'" + value;
        }

        // If the value contains commas, quotes, or newlines, wrap it in quotes and escape internal quotes
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            // Escape quotes by doubling them (CSV standard)
            string escaped = value.Replace("\"", "\"\"");
            return $"\"{escaped}\"";
        }

        return value;
    }
}
