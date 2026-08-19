namespace TeamBalancer.Core.Services.Csv;

using System.Globalization;
using System.Text;
using TeamBalancer.Core.Models;
using TeamBalancer.Core.Services.Interfaces;

/// <summary>
/// Implements the player list store using a CSV file, <c>lists.csv</c>, holding one row per
/// list. The players of each list live in a file of their own, which this repository creates
/// when a list is added and removes when it is deleted.
/// </summary>
/// <remarks>
/// The upgrade from single-list storage runs here, in <c>EnsureInitializedAsync</c>, rather
/// than in a separate startup service. Two reasons: the trigger is exactly "lists.csv has not
/// been written yet", which is this class's own state, and lazily initializing here means the
/// migration cannot be ordered after the first read of the lists - every entry point goes
/// through it.
/// </remarks>
public class CsvPlayerListRepository : IPlayerListRepository
{
    /// <summary>
    /// The header row of lists.csv.
    /// </summary>
    private const string Header = "Id,Name,CreatedAt,UpdatedAt";

    /// <summary>
    /// The name given to the list that migration creates for a pre-multi-list squad. It is
    /// stored data rather than UI text, so it is not translated - the user can rename it.
    /// </summary>
    public const string DefaultListName = "My Players";

    private readonly ICsvParser _csvParser;
    private readonly string _dataDirectory;
    private readonly string _filePath;
    private List<PlayerListInfo> _lists;
    private Task? _initialization;

    /// <summary>
    /// Initializes a new instance of the CsvPlayerListRepository class.
    /// </summary>
    /// <param name="csvParser">The CSV parser used to seed a new list's empty player file.</param>
    /// <param name="dataDirectory">The directory holding lists.csv and the player files.</param>
    public CsvPlayerListRepository(ICsvParser csvParser, string dataDirectory)
    {
        _csvParser = csvParser ?? throw new ArgumentNullException(nameof(csvParser));
        _dataDirectory = dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory));
        _filePath = PlayerListFiles.ListsFilePath(dataDirectory);
        _lists = [];
    }

    /// <summary>
    /// Ensures the repository is initialized by loading lists.csv, creating it on first run.
    /// </summary>
    /// <remarks>
    /// The load is cached rather than flagged as done at the end of it, for the same reason as
    /// in <see cref="CsvPlayerRepository"/>: reading the file is asynchronous, so a flag leaves
    /// a window in which a second caller loads it again and assigns its result over the list
    /// the first caller had already added to.
    /// </remarks>
    /// <returns>The load, shared by every caller that arrives before it finishes.</returns>
    private Task EnsureInitializedAsync() => _initialization ??= LoadAsync();

    /// <summary>
    /// Reads lists.csv, creating the default list on a first run or an upgrade.
    /// </summary>
    private async Task LoadAsync()
    {
        try
        {
            await LoadCoreAsync();
        }
        catch
        {
            // Not cached when it fails, so a momentarily locked file does not leave every later
            // call holding the same error.
            _initialization = null;
            throw;
        }
    }

    /// <summary>
    /// The body of the load, split out so the caching wrapper stays readable.
    /// </summary>
    private async Task LoadCoreAsync()
    {
        if (File.Exists(_filePath))
        {
            var csvContent = await File.ReadAllTextAsync(_filePath);
            _lists = ParseLists(csvContent);
        }

        // Both a fresh install and an upgrade from single-list storage land here, and both
        // want the same outcome: one list named after the convention, mapped to players.csv.
        // On an upgrade that file already holds the user's squad and is deliberately left
        // exactly as it is; on a fresh install CsvPlayerRepository creates it from the
        // embedded starter roster the first time anything reads it, as it always has.
        //
        // An existing but unusable lists.csv - emptied, or hand-edited into rows that no
        // longer parse - is treated the same way. The app cannot run without at least one
        // list, and re-adopting players.csv loses nothing.
        if (_lists.Count == 0)
        {
            var now = DateTime.UtcNow;
            _lists.Add(new PlayerListInfo
            {
                Id = PlayerListFiles.DefaultListId,
                Name = DefaultListName,
                CreatedAt = now,
                UpdatedAt = now
            });

            await SaveAsync();
        }
    }

    /// <summary>
    /// Retrieves every player list, oldest first.
    /// </summary>
    public async Task<IEnumerable<PlayerListInfo>> GetAllAsync()
    {
        await EnsureInitializedAsync();
        return _lists.OrderBy(l => l.CreatedAt).ToList();
    }

    /// <summary>
    /// Retrieves a list by its unique identifier.
    /// </summary>
    public async Task<PlayerListInfo?> GetByIdAsync(Guid id)
    {
        await EnsureInitializedAsync();
        return _lists.FirstOrDefault(l => l.Id == id);
    }

    /// <summary>
    /// Creates a new, empty player list.
    /// </summary>
    public async Task<PlayerListInfo> AddAsync(string name)
    {
        await EnsureInitializedAsync();

        ArgumentNullException.ThrowIfNull(name);
        ValidateName(name, nameof(name));

        var now = DateTime.UtcNow;
        var list = new PlayerListInfo
        {
            Id = Guid.NewGuid(),
            Name = name,
            CreatedAt = now,
            UpdatedAt = now
        };

        // Write the list's player file straight away, empty. Without it CsvPlayerRepository
        // would find no file when the user first switches over and fall back to the embedded
        // starter roster - which is what a first run wants, and not what a list the user just
        // asked for wants.
        await CreateEmptyPlayerFileAsync(list.Id);

        _lists.Add(list);
        await SaveAsync();

        return list;
    }

    /// <summary>
    /// Renames an existing list. Metadata only - no player file is touched.
    /// </summary>
    public async Task<PlayerListInfo> RenameAsync(Guid id, string newName)
    {
        await EnsureInitializedAsync();

        ArgumentNullException.ThrowIfNull(newName);
        ValidateName(newName, nameof(newName));

        var list = _lists.FirstOrDefault(l => l.Id == id)
            ?? throw new InvalidOperationException($"Player list with ID {id} not found.");

        list.Name = newName;
        list.UpdatedAt = DateTime.UtcNow;

        await SaveAsync();

        return list;
    }

    /// <summary>
    /// Deletes a list's metadata row and the CSV file holding its players.
    /// </summary>
    public async Task DeleteAsync(Guid id)
    {
        await EnsureInitializedAsync();

        var list = _lists.FirstOrDefault(l => l.Id == id)
            ?? throw new InvalidOperationException($"Player list with ID {id} not found.");

        if (_lists.Count == 1)
            throw new InvalidOperationException("The last player list cannot be deleted.");

        // The legacy file is the one exception: it is left on disk. Deleting it would mean an
        // install rolled back to a pre-multi-list build - which reads players.csv and nothing
        // else - coming up with the embedded starter roster instead of the user's own players.
        // An orphaned file of a few hundred bytes is the cheaper outcome.
        if (list.Id != PlayerListFiles.DefaultListId)
        {
            var playerFilePath = PlayerListFiles.PlayerFilePath(_dataDirectory, list.Id);
            if (File.Exists(playerFilePath))
            {
                File.Delete(playerFilePath);
            }
        }

        _lists.Remove(list);

        await SaveAsync();
    }

    /// <summary>
    /// Rejects a list name that would break the CSV format or carry a spreadsheet formula.
    /// </summary>
    /// <param name="name">The name to validate.</param>
    /// <param name="parameterName">The argument the name arrived in.</param>
    private static void ValidateName(string name, string parameterName)
    {
        if (!CsvSafeName.IsValid(name))
            throw new ArgumentException($"List name is invalid. Name cannot be empty, contain special characters (,\"\\n\\r), start with formula characters (=+-@), or exceed {CsvSafeName.MaxLength} characters.", parameterName);
    }

    /// <summary>
    /// Writes an empty player file - header only - for a newly created list.
    /// </summary>
    /// <param name="listId">The list to create the file for.</param>
    private async Task CreateEmptyPlayerFileAsync(Guid listId)
    {
        var playerFilePath = PlayerListFiles.PlayerFilePath(_dataDirectory, listId);
        if (File.Exists(playerFilePath))
            return;

        EnsureDataDirectoryExists();

        // Serializing no players yields exactly the storage header, so the empty file is
        // written in the same format CsvPlayerRepository saves in rather than a copy of it.
        await File.WriteAllTextAsync(playerFilePath, _csvParser.SerializePlayers([], includeSelection: true));
    }

    /// <summary>
    /// Writes every list back to lists.csv.
    /// </summary>
    private async Task SaveAsync()
    {
        EnsureDataDirectoryExists();

        var sb = new StringBuilder();
        sb.AppendLine(Header);

        foreach (var list in _lists.OrderBy(l => l.CreatedAt))
        {
            var createdAt = list.CreatedAt.ToString("O", CultureInfo.InvariantCulture);
            var updatedAt = list.UpdatedAt.ToString("O", CultureInfo.InvariantCulture);

            // Names are validated on the way in, so none of these values can carry a comma,
            // a quote or a newline that would need escaping here.
            sb.AppendLine($"{list.Id},{list.Name},{createdAt},{updatedAt}");
        }

        await File.WriteAllTextAsync(_filePath, sb.ToString());
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

    /// <summary>
    /// Parses lists.csv. A row that does not have all four columns, or whose id or dates do
    /// not parse, is dropped rather than allowed to fail the load: losing one damaged row
    /// still leaves the user their other lists.
    /// </summary>
    /// <param name="csvContent">The contents of lists.csv.</param>
    /// <returns>The lists the file describes.</returns>
    private static List<PlayerListInfo> ParseLists(string csvContent)
    {
        var lists = new List<PlayerListInfo>();

        if (string.IsNullOrWhiteSpace(csvContent))
        {
            return lists;
        }

        var lines = csvContent.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        // Skip header row
        for (int i = 1; i < lines.Length; i++)
        {
            var parts = lines[i].Trim().Split(',');
            if (parts.Length != 4)
            {
                continue;
            }

            if (!Guid.TryParse(parts[0].Trim(), out var id) ||
                !TryParseTimestamp(parts[2], out var createdAt) ||
                !TryParseTimestamp(parts[3], out var updatedAt))
            {
                continue;
            }

            lists.Add(new PlayerListInfo
            {
                Id = id,
                Name = parts[1].Trim(),
                CreatedAt = createdAt,
                UpdatedAt = updatedAt
            });
        }

        return lists;
    }

    /// <summary>
    /// Parses a round-trip formatted timestamp, keeping it in UTC rather than letting it be
    /// reinterpreted in the device's local time.
    /// </summary>
    /// <param name="value">The raw cell value.</param>
    /// <param name="timestamp">The parsed timestamp when successful.</param>
    /// <returns>True if the value is a timestamp, false otherwise.</returns>
    private static bool TryParseTimestamp(string value, out DateTime timestamp) =>
        DateTime.TryParse(
            value.Trim(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out timestamp);
}
