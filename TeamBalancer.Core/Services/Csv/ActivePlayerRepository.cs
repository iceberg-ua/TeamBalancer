namespace TeamBalancer.Core.Services.Csv;

using TeamBalancer.Core.Models;
using TeamBalancer.Core.Services.Interfaces;

/// <summary>
/// Implements the player repository for whichever list is active, by holding a
/// <see cref="CsvPlayerRepository"/> pointed at that list's file and forwarding every call to
/// it. Switching lists replaces the inner repository, which is why the screens injecting
/// <see cref="IPlayerRepository"/> never had to learn that lists exist.
/// </summary>
public class ActivePlayerRepository : IActivePlayerRepository
{
    private readonly ICsvParser _csvParser;
    private readonly IPlayerListRepository _listRepository;
    private readonly ICurrentListPreference _preference;
    private readonly string _dataDirectory;

    private CsvPlayerRepository? _inner;
    private Guid _currentListId;

    /// <summary>
    /// Initializes a new instance of the ActivePlayerRepository class.
    /// </summary>
    /// <param name="csvParser">The CSV parser handed to each list's repository.</param>
    /// <param name="listRepository">The store the active list is resolved against.</param>
    /// <param name="preference">Remembers which list was last active.</param>
    /// <param name="dataDirectory">The directory holding the player files.</param>
    public ActivePlayerRepository(
        ICsvParser csvParser,
        IPlayerListRepository listRepository,
        ICurrentListPreference preference,
        string dataDirectory)
    {
        _csvParser = csvParser ?? throw new ArgumentNullException(nameof(csvParser));
        _listRepository = listRepository ?? throw new ArgumentNullException(nameof(listRepository));
        _preference = preference ?? throw new ArgumentNullException(nameof(preference));
        _dataDirectory = dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory));
    }

    /// <inheritdoc />
    public event Action? ListChanged;

    /// <summary>
    /// Gets the identifier of the list currently being read and written. It is
    /// <see cref="Guid.Empty"/> until the first call resolves which list that is.
    /// </summary>
    public Guid CurrentListId => _currentListId;

    /// <inheritdoc />
    public async Task<Guid> GetCurrentListIdAsync()
    {
        await EnsureInitializedAsync();

        return _currentListId;
    }

    /// <summary>
    /// Makes another list the active one and remembers the choice.
    /// </summary>
    public async Task SwitchListAsync(Guid listId)
    {
        await EnsureInitializedAsync();

        var list = await _listRepository.GetByIdAsync(listId)
            ?? throw new InvalidOperationException($"Player list with ID {listId} not found.");

        // Store the choice even when it is the list already in use, for the same reason the
        // language switcher does: choosing the list the app happened to open on is how a user
        // pins it, and without a stored preference the next launch resolves it all over again.
        _preference.Write(list.Id);

        if (list.Id == _currentListId)
        {
            return;
        }

        // Selection changes are held in memory until something saves them - normally the app
        // going to the background. Switching lists drops the repository holding them, so they
        // are flushed here; otherwise the list the user is leaving would silently forget who
        // they had ticked.
        await _inner!.SaveChangesAsync();

        _currentListId = list.Id;
        _inner = CreateRepositoryFor(list.Id);

        ListChanged?.Invoke();
    }

    /// <summary>
    /// Deletes a list, switching away from it first when it is the active one.
    /// </summary>
    public async Task DeleteListAsync(Guid listId)
    {
        await EnsureInitializedAsync();

        if (listId == _currentListId)
        {
            var replacement = (await _listRepository.GetAllAsync())
                .FirstOrDefault(l => l.Id != listId)
                ?? throw new InvalidOperationException("The last player list cannot be deleted.");

            await SwitchListAsync(replacement.Id);
        }

        await _listRepository.DeleteAsync(listId);
    }

    /// <summary>
    /// Retrieves all players from the active list.
    /// </summary>
    public async Task<IEnumerable<Player>> GetAllAsync()
    {
        var repository = await ResolveAsync();
        return await repository.GetAllAsync();
    }

    /// <summary>
    /// Retrieves a player of the active list by their unique identifier.
    /// </summary>
    public async Task<Player?> GetByIdAsync(Guid id)
    {
        var repository = await ResolveAsync();
        return await repository.GetByIdAsync(id);
    }

    /// <summary>
    /// Retrieves a player of the active list by their name (case-insensitive).
    /// </summary>
    public async Task<Player?> GetByNameAsync(string name)
    {
        var repository = await ResolveAsync();
        return await repository.GetByNameAsync(name);
    }

    /// <summary>
    /// Adds a new player to the active list.
    /// </summary>
    public async Task<Player> AddAsync(Player player)
    {
        var repository = await ResolveAsync();
        return await repository.AddAsync(player);
    }

    /// <summary>
    /// Updates an existing player of the active list.
    /// </summary>
    public async Task<Player> UpdateAsync(Player player)
    {
        var repository = await ResolveAsync();
        return await repository.UpdateAsync(player);
    }

    /// <summary>
    /// Deletes a player from the active list (soft delete).
    /// </summary>
    public async Task<bool> DeleteAsync(Guid id)
    {
        var repository = await ResolveAsync();
        return await repository.DeleteAsync(id);
    }

    /// <summary>
    /// Saves the active list's pending changes to its CSV file.
    /// </summary>
    public async Task<int> SaveChangesAsync()
    {
        var repository = await ResolveAsync();
        return await repository.SaveChangesAsync();
    }

    /// <summary>
    /// Returns the repository for the active list, resolving which list that is on first use.
    /// </summary>
    private async Task<IPlayerRepository> ResolveAsync()
    {
        await EnsureInitializedAsync();
        return _inner!;
    }

    /// <summary>
    /// Resolves the list to open with: the one the user last switched to, falling back to the
    /// oldest list when they never chose one or when the one they chose is gone - which is
    /// what a list deleted on another device will look like once lists are shared.
    /// </summary>
    private async Task EnsureInitializedAsync()
    {
        if (_inner is not null)
            return;

        var lists = (await _listRepository.GetAllAsync()).ToList();
        if (lists.Count == 0)
            throw new InvalidOperationException("No player lists exist. The list repository must always keep at least one.");

        var storedId = _preference.Read();
        var list = lists.FirstOrDefault(l => l.Id == storedId) ?? lists[0];

        _currentListId = list.Id;
        _inner = CreateRepositoryFor(list.Id);
    }

    /// <summary>
    /// Creates the repository that reads and writes one list's player file.
    /// </summary>
    /// <param name="listId">The list to open.</param>
    /// <returns>A repository pointed at that list's file.</returns>
    private CsvPlayerRepository CreateRepositoryFor(Guid listId) =>
        new(_csvParser, PlayerListFiles.PlayerFilePath(_dataDirectory, listId));
}
