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
    /// Expected format: Name,Speed,TechnicalSkills,Stamina
    /// </summary>
    public IEnumerable<Player> ParsePlayers(string csvContent)
    {
        if (string.IsNullOrWhiteSpace(csvContent))
        {
            return [];
        }

        var players = new List<Player>();
        var lines = csvContent.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        int skippedRows = 0;

        // Skip header row
        for (int i = 1; i < lines.Length; i++)
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
                _logger.LogWarning("Skipping line {LineNumber}: Expected 4 columns, found {ColumnCount}. Content: {LineContent}",
                    lineNumber, parts.Length, line);
                skippedRows++;
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

                // Validate skill levels
                if (!player.AreSkillLevelsValid())
                {
                    _logger.LogWarning("Skipping line {LineNumber}: Invalid skill levels for player '{PlayerName}'. Speed={Speed}, Technical={Technical}, Stamina={Stamina}",
                        lineNumber, player.Name, player.Speed, player.TechnicalSkills, player.Stamina);
                    skippedRows++;
                    continue;
                }

                players.Add(player);
            }
            catch (FormatException ex)
            {
                _logger.LogError(ex, "Skipping line {LineNumber}: Failed to parse numeric value. Content: {LineContent}",
                    lineNumber, line);
                skippedRows++;
            }
            catch (OverflowException ex)
            {
                _logger.LogError(ex, "Skipping line {LineNumber}: Numeric value out of range. Content: {LineContent}",
                    lineNumber, line);
                skippedRows++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Skipping line {LineNumber}: Unexpected error parsing line. Content: {LineContent}",
                    lineNumber, line);
                skippedRows++;
            }
        }

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

        return players;
    }

    /// <summary>
    /// Serializes a collection of Player objects into CSV format.
    /// Applies defense-in-depth by sanitizing values to prevent CSV injection.
    /// </summary>
    public string SerializePlayers(IEnumerable<Player> players)
    {
        var sb = new StringBuilder();

        // Write header
        sb.AppendLine("Name,Speed,TechnicalSkills,Stamina");

        // Write player data
        foreach (var player in players)
        {
            // Sanitize the name to prevent CSV injection (defense-in-depth)
            string sanitizedName = SanitizeCsvValue(player.Name);
            sb.AppendLine($"{sanitizedName},{player.Speed},{player.TechnicalSkills},{player.Stamina}");
        }

        return sb.ToString();
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
