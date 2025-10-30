namespace TeamBalancer.Core.Services.Csv;

using System.Reflection;
using TeamBalancer.Core.Models;
using TeamBalancer.Core.Services.Interfaces;

/// <summary>
/// Implements player repository using CSV file storage.
/// </summary>
public class CsvPlayerRepository : IPlayerRepository
{
    private readonly ICsvParser _csvParser;
    private readonly string _filePath;
    private List<Player> _players;
    private bool _isInitialized;

    /// <summary>
    /// Initializes a new instance of the CsvPlayerRepository class.
    /// </summary>
    /// <param name="csvParser">The CSV parser to use.</param>
    /// <param name="filePath">The path to the CSV file.</param>
    public CsvPlayerRepository(ICsvParser csvParser, string filePath)
    {
        _csvParser = csvParser ?? throw new ArgumentNullException(nameof(csvParser));
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        _players = [];
        _isInitialized = false;
    }

    /// <summary>
    /// Ensures the repository is initialized by loading data from CSV.
    /// If the file doesn't exist, it copies the embedded resource.
    /// </summary>
    private async Task EnsureInitializedAsync()
    {
        if (_isInitialized)
            return;

        // If file doesn't exist, copy from embedded resource
        if (!File.Exists(_filePath))
        {
            await CopyEmbeddedResourceAsync();
        }

        // Load players from file
        if (File.Exists(_filePath))
        {
            var csvContent = await File.ReadAllTextAsync(_filePath);
            _players = [.. _csvParser.ParsePlayers(csvContent)];
        }

        _isInitialized = true;
    }

    /// <summary>
    /// Copies the embedded CSV resource to the target file path.
    /// </summary>
    private async Task CopyEmbeddedResourceAsync()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "TeamBalancer.Core.Data.players.csv";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream != null)
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var fileStream = File.Create(_filePath);
            await stream.CopyToAsync(fileStream);
        }
    }

    /// <summary>
    /// Retrieves all players from the data source.
    /// </summary>
    public async Task<IEnumerable<Player>> GetAllAsync()
    {
        await EnsureInitializedAsync();
        return _players.Where(p => p.IsActive).ToList();
    }

    /// <summary>
    /// Retrieves a player by their unique identifier.
    /// </summary>
    public async Task<Player?> GetByIdAsync(Guid id)
    {
        await EnsureInitializedAsync();
        return _players.FirstOrDefault(p => p.Id == id && p.IsActive);
    }

    /// <summary>
    /// Adds a new player to the data source.
    /// </summary>
    public async Task<Player> AddAsync(Player player)
    {
        await EnsureInitializedAsync();

        ArgumentNullException.ThrowIfNull(player);

        if (!player.IsNameValid())
            throw new ArgumentException("Player name cannot be empty or contain commas.", nameof(player));

        if (!player.AreSkillLevelsValid())
            throw new ArgumentException("Player skill levels must be between 1 and 3.", nameof(player));

        // Ensure new ID if not set
        if (player.Id == Guid.Empty)
            player.Id = Guid.NewGuid();

        player.CreatedAt = DateTime.UtcNow;
        player.IsActive = true;

        _players.Add(player);

        return player;
    }

    /// <summary>
    /// Updates an existing player in the data source.
    /// </summary>
    public async Task<Player> UpdateAsync(Player player)
    {
        await EnsureInitializedAsync();

        if (player == null)
            throw new ArgumentNullException(nameof(player));

        if (!player.IsNameValid())
            throw new ArgumentException("Player name cannot be empty or contain commas.", nameof(player));

        if (!player.AreSkillLevelsValid())
            throw new ArgumentException("Player skill levels must be between 1 and 3.", nameof(player));

        var existingPlayer = _players.FirstOrDefault(p => p.Id == player.Id);
        if (existingPlayer == null)
            throw new InvalidOperationException($"Player with ID {player.Id} not found.");

        existingPlayer.Name = player.Name;
        existingPlayer.Speed = player.Speed;
        existingPlayer.TechnicalSkills = player.TechnicalSkills;
        existingPlayer.Stamina = player.Stamina;
        existingPlayer.UpdatedAt = DateTime.UtcNow;
        existingPlayer.IsActive = player.IsActive;

        return existingPlayer;
    }

    /// <summary>
    /// Deletes a player from the data source (soft delete).
    /// </summary>
    public async Task<bool> DeleteAsync(Guid id)
    {
        await EnsureInitializedAsync();

        var player = _players.FirstOrDefault(p => p.Id == id);
        if (player == null)
            return false;

        player.IsActive = false;
        player.UpdatedAt = DateTime.UtcNow;

        return true;
    }

    /// <summary>
    /// Saves all pending changes to the CSV file.
    /// </summary>
    public async Task<int> SaveChangesAsync()
    {
        await EnsureInitializedAsync();

        // Only save active players to CSV
        var activePlayers = _players.Where(p => p.IsActive);
        var csvContent = _csvParser.SerializePlayers(activePlayers);
        
        await File.WriteAllTextAsync(_filePath, csvContent);

        return activePlayers.Count();
    }
}
