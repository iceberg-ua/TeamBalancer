namespace TeamBalancer.Core.Services.Csv;

using System.Globalization;
using System.Text;
using TeamBalancer.Core.Models;
using TeamBalancer.Core.Services.Interfaces;

/// <summary>
/// Stores finished matches in <c>matches.csv</c>, alongside the player files, one row per
/// player per match, and reads them back for the history screen.
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

    /// <summary>
    /// The number of columns <see cref="Header"/> names. A row that has any other number of
    /// them is not one this repository wrote.
    /// </summary>
    private const int ColumnCount = 9;

    /// <summary>
    /// The number of sides a result has. A match is two sides playing each other, so rows that
    /// yield fewer are not a result - see <see cref="Parse"/>.
    /// </summary>
    private const int SidesPerMatch = 2;

    /// <summary>
    /// The line endings a row can be separated by. The file is written with the platform's,
    /// but it can be carried between one platform and another, so reading accepts either.
    /// </summary>
    private static readonly char[] NewLineCharacters = ['\r', '\n'];

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

            // An empty file needs the header as much as a missing one does. A file left behind
            // at zero bytes - a first write that failed part way, or storage the phone reclaimed
            // the contents of - would otherwise be filled with rows no reader can name.
            var existing = new FileInfo(_filePath);

            var content = Serialize(match, writeHeader: !existing.Exists || existing.Length == 0);

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
                //
                // The empty guid rather than an empty cell: PlayerId is a guid column in a file
                // that is only ever appended to, so a reader meets this row for as long as the
                // file lives and needs a value it can parse and then recognise as nobody. The
                // name beside it stays blank - a stand-in name would have to be one no player
                // could be called, and the id has already said this row is not a player.
                sb.AppendLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{matchColumns},{Guid.Empty},,0,0"));
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

    /// <inheritdoc />
    public async Task<IReadOnlyList<FinishedMatch>> GetAllAsync()
    {
        var contents = await ReadFileAsync();

        return contents is null ? [] : Parse(contents);
    }

    /// <inheritdoc />
    public async Task<FinishedMatch?> GetByIdAsync(Guid matchId)
    {
        var contents = await ReadFileAsync();

        if (contents is null)
        {
            return null;
        }

        // The whole file is still read - a flat file cannot be seeked by match - but only the
        // rows carrying this id are built into anything. Opening one game out of a season is
        // then a scan rather than a season's worth of objects thrown away to keep one.
        var rows = new List<Row>();

        foreach (var line in contents.Split(NewLineCharacters, StringSplitOptions.RemoveEmptyEntries))
        {
            if (TryParseRow(line, out var row) && row.MatchId == matchId)
            {
                rows.Add(row);
            }
        }

        if (rows.Count == 0)
        {
            return null;
        }

        var match = BuildMatch(rows);

        // Held to the same rule the history list is read under: rows that do not make a result
        // are not one, whichever way they were asked for.
        return IsResult(match) ? match : null;
    }

    /// <summary>
    /// Reads the file, or answers null when the app has never written one.
    /// </summary>
    /// <remarks>
    /// Takes the same lock the appends take. A finish is a read-then-write of this file, and a
    /// read landing in the middle of one would see a match with only half its rows and show it
    /// as a game somebody sat out.
    /// </remarks>
    private async Task<string?> ReadFileAsync()
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        await _writeLock.WaitAsync();

        try
        {
            return await File.ReadAllTextAsync(_filePath);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// One row of matches.csv, read back into its columns.
    /// </summary>
    private sealed record Row(
        Guid MatchId,
        DateTime PlayedAt,
        Guid ListId,
        string TeamName,
        int Score,
        Guid PlayerId,
        string PlayerName,
        int Goals,
        int Assists);

    /// <summary>
    /// Reads the whole file into matches, newest first.
    /// </summary>
    /// <remarks>
    /// Rows are grouped back into matches by <c>MatchId</c> and then into sides by team name,
    /// both in the order they appear in the file, which is the order they were written in.
    /// Nothing here trusts the file to be well formed: it is plain text in a directory the
    /// user's other tools can reach, and one bad row must cost that row rather than the
    /// history. Rows that end up making less than a match cost the match, though: a game with
    /// only one side is not a result, and settling that here is what lets every screen read
    /// both halves of a scoreline without checking first that the second half is there.
    /// </remarks>
    private static List<FinishedMatch> Parse(string contents)
    {
        var order = new List<Guid>();
        var grouped = new Dictionary<Guid, List<Row>>();

        foreach (var line in contents.Split(NewLineCharacters, StringSplitOptions.RemoveEmptyEntries))
        {
            // The header falls out here along with anything damaged - "MatchId" is not a guid -
            // so it needs no check of its own. That also means a stray second header, which an
            // append should never write but a hand-edited file may hold, costs nothing.
            if (!TryParseRow(line, out var row))
            {
                continue;
            }

            if (!grouped.TryGetValue(row.MatchId, out var rows))
            {
                rows = [];
                grouped[row.MatchId] = rows;
                order.Add(row.MatchId);
            }

            rows.Add(row);
        }

        var matches = order.ConvertAll(id => BuildMatch(grouped[id])).FindAll(IsResult);

        // Reversed before the sort rather than after it: OrderByDescending is stable, so two
        // matches finished within the same tick would otherwise come back oldest first, which
        // is the one thing "most recent first" must not do.
        matches.Reverse();

        return [.. matches.OrderByDescending(m => m.PlayedAt)];
    }

    /// <summary>
    /// Gets whether what was read back is a result rather than the remains of one.
    /// </summary>
    /// <remarks>
    /// Nothing this repository writes can fail this: a match is appended from a split, and a
    /// split is two sides. It fails on a file edited by hand, or one whose every row for a
    /// side was damaged badly enough to be dropped - and a lone side shown as a game against a
    /// blank opponent that lost 0 would be a result the app invented.
    /// </remarks>
    /// <param name="match">The match read back.</param>
    private static bool IsResult(FinishedMatch match) => match.Teams.Count >= SidesPerMatch;

    /// <summary>
    /// Builds a match from the rows that carry its id.
    /// </summary>
    /// <param name="rows">The match's rows, in the order the file holds them.</param>
    private static FinishedMatch BuildMatch(List<Row> rows)
    {
        var order = new List<string>();
        var sides = new Dictionary<string, List<Row>>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            if (!sides.TryGetValue(row.TeamName, out var sideRows))
            {
                sideRows = [];
                sides[row.TeamName] = sideRows;
                order.Add(row.TeamName);
            }

            sideRows.Add(row);
        }

        // The match columns are repeated on every row, so any row of the match answers for it.
        var first = rows[0];

        return new FinishedMatch
        {
            Id = first.MatchId,
            PlayedAt = first.PlayedAt,
            ListId = first.ListId,
            Teams = [.. order.Select(name => BuildTeam(name, sides[name]))]
        };
    }

    /// <summary>
    /// Builds one side from its rows.
    /// </summary>
    /// <param name="name">The team name the rows share.</param>
    /// <param name="rows">The side's rows, in the order the file holds them.</param>
    private static FinishedTeam BuildTeam(string name, List<Row> rows) => new()
    {
        Name = name,

        // The score is repeated on every row of the side, so the first row carries it.
        Score = rows[0].Score,

        // A side left with nobody on it is written as one row against the empty guid, because
        // the score belongs to the side rather than to anyone on it. That row is not a player
        // and must not become a nameless entry in the line-up.
        Players =
        [
            .. rows
                .Where(row => row.PlayerId != Guid.Empty)
                .Select(row => new FinishedPlayer
                {
                    Id = row.PlayerId,
                    Name = row.PlayerName,
                    Goals = row.Goals,
                    Assists = row.Assists
                })
        ]
    };

    /// <summary>
    /// Reads one line into its columns, rejecting anything this repository could not have
    /// written.
    /// </summary>
    /// <param name="line">The line to read.</param>
    /// <param name="row">The row read, when the line holds one.</param>
    /// <returns>True when the line is a match row.</returns>
    private static bool TryParseRow(string line, out Row row)
    {
        row = null!;

        var cells = line.Split(',');

        // Exactly the columns written, not merely enough of them. Names cannot carry a comma -
        // CsvSafeName sees to that wherever one is entered, and team names are generated - so a
        // row that has split into more cells than it should is damaged rather than escaped, and
        // reading its first nine cells would quietly attribute goals to the wrong player.
        if (cells.Length != ColumnCount)
        {
            return false;
        }

        if (!Guid.TryParse(cells[0], out var matchId) ||
            !Guid.TryParse(cells[2], out var listId) ||
            !Guid.TryParse(cells[5], out var playerId))
        {
            return false;
        }

        // Round-trip, invariant, exactly as written - so a phone whose culture has since
        // changed still reads back the timestamps it wrote under the old one.
        if (!DateTime.TryParseExact(
                cells[1],
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var playedAt))
        {
            return false;
        }

        if (!TryParseCount(cells[4], out var score) ||
            !TryParseCount(cells[7], out var goals) ||
            !TryParseCount(cells[8], out var assists))
        {
            return false;
        }

        row = new Row(matchId, playedAt, listId, cells[3], score, playerId, cells[6], goals, assists);

        return true;
    }

    /// <summary>
    /// Reads a tally or a score: a whole number that cannot be negative.
    /// </summary>
    /// <remarks>
    /// A negative is refused rather than clamped. Nothing in the app can write one, so a row
    /// holding one has been edited by hand into something that is no longer a record of a
    /// game, and taking the rest of its numbers at face value would be reading a result out of
    /// a row that has already proved it is not one.
    /// </remarks>
    /// <param name="cell">The cell to read.</param>
    /// <param name="value">The number read.</param>
    /// <returns>True when the cell holds a count.</returns>
    private static bool TryParseCount(string cell, out int value) =>
        int.TryParse(cell, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= 0;
}
