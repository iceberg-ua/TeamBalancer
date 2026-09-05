namespace TeamBalancer.Core.Services.Csv;

using System.Globalization;
using System.Text;
using TeamBalancer.Core.Models;
using TeamBalancer.Core.Services.Interfaces;

/// <summary>
/// Stores finished matches in <c>matches.csv</c>, alongside the player files, one row per
/// player per match.
/// </summary>
/// <remarks>
/// A match is a shape a flat file does not hold naturally - a match has two sides, a side has
/// players, a player has a tally - so it is flattened by repeating the match and team columns
/// on every player's row. Grouping the rows by <c>MatchId</c> and then by <c>Team</c> puts it
/// back together. The alternative, a row per match with the line-ups packed into a cell, would
/// need an escaping scheme of its own inside a format that already has one.
///
/// The file is only ever appended to, which is what makes a finish a single cheap write no
/// matter how many matches have been played before it.
/// </remarks>
public class CsvMatchRepository : IMatchRepository
{
    /// <summary>
    /// The file holding every finished match.
    /// </summary>
    public const string MatchesFileName = "matches.csv";

    /// <summary>
    /// The header row of matches.csv.
    /// </summary>
    private const string Header = "MatchId,PlayedAt,ListId,Team,Score,PlayerId,PlayerName,Goals,Assists";

    private readonly string _dataDirectory;
    private readonly string _filePath;

    /// <summary>
    /// Serializes appends. Two matches cannot be finished at once through the UI, but an
    /// append is a read-then-write of the same file and the cost of holding a lock over it is
    /// nothing next to the cost of interleaving two of them.
    /// </summary>
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the CsvMatchRepository class.
    /// </summary>
    /// <param name="dataDirectory">The directory the app keeps its data in.</param>
    public CsvMatchRepository(string dataDirectory)
    {
        _dataDirectory = dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory));
        _filePath = Path.Combine(dataDirectory, MatchesFileName);
    }

    /// <inheritdoc />
    public async Task AppendAsync(MatchRecord match)
    {
        ArgumentNullException.ThrowIfNull(match);

        await _writeLock.WaitAsync();

        try
        {
            EnsureDataDirectoryExists();

            var content = Serialize(match, writeHeader: !File.Exists(_filePath));

            await File.AppendAllTextAsync(_filePath, content);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Renders a match as the rows it occupies in the file.
    /// </summary>
    /// <param name="match">The match to render.</param>
    /// <param name="writeHeader">Whether to lead with the header row, for a file being created.</param>
    private static string Serialize(MatchRecord match, bool writeHeader)
    {
        var sb = new StringBuilder();

        if (writeHeader)
        {
            sb.AppendLine(Header);
        }

        // Round-trip format in the invariant culture, so a phone set to a comma decimal
        // separator or a non-Gregorian calendar still writes a timestamp every reader can
        // parse - and one with no comma in it to break the row.
        var playedAt = match.PlayedAt.ToString("O", CultureInfo.InvariantCulture);

        foreach (var team in match.Teams)
        {
            // Team names are generated ("Team A", "Team B") and player names are validated by
            // CsvSafeName wherever one is entered, so no value on a row can carry a comma, a
            // quote or a newline that would need escaping here. This is the same assumption
            // lists.csv is written under.
            var matchColumns = string.Create(
                CultureInfo.InvariantCulture,
                $"{match.Id},{playedAt},{match.ListId},{team.Name},{team.Score}");

            if (team.Players.Count == 0)
            {
                // A side can be left empty by moving its last player across. The score belongs
                // to the side rather than to anyone on it, so the row is written regardless -
                // dropping it would lose a result that is still half of the scoreline.
                sb.AppendLine($"{matchColumns},,,0,0");
                continue;
            }

            foreach (var participant in team.Players)
            {
                // The name is stored next to the id rather than looked up later: a player
                // renamed or deleted from their list must not rewrite or erase who played in
                // a match that has already been finished.
                sb.AppendLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{matchColumns},{participant.Player.Id},{participant.Player.Name},{participant.Goals},{participant.Assists}"));
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Creates the data directory if the app has never written to it.
    /// </summary>
    private void EnsureDataDirectoryExists()
    {
        if (!string.IsNullOrEmpty(_dataDirectory) && !Directory.Exists(_dataDirectory))
        {
            Directory.CreateDirectory(_dataDirectory);
        }
    }
}
