namespace TeamBalancer.Core.Services.Csv;

using System.Text;
using TeamBalancer.Core.Models;
using TeamBalancer.Core.Services.Interfaces;

/// <summary>
/// Implements CSV parsing and serialization for Player objects.
/// </summary>
public class CsvParser : ICsvParser
{
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

        // Skip header row
        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var parts = line.Split(',');
            if (parts.Length < 4)
                continue;

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
                if (player.AreSkillLevelsValid())
                {
                    players.Add(player);
                }
            }
            catch
            {
                // Skip invalid rows
                continue;
            }
        }

        return players;
    }

    /// <summary>
    /// Serializes a collection of Player objects into CSV format.
    /// </summary>
    public string SerializePlayers(IEnumerable<Player> players)
    {
        var sb = new StringBuilder();
        
        // Write header
        sb.AppendLine("Name,Speed,TechnicalSkills,Stamina");

        // Write player data
        foreach (var player in players)
        {
            sb.AppendLine($"{player.Name},{player.Speed},{player.TechnicalSkills},{player.Stamina}");
        }

        return sb.ToString();
    }
}
