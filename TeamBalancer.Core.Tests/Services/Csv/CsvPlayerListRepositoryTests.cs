namespace TeamBalancer.Core.Tests.Services.Csv;

using Microsoft.Extensions.Logging.Abstractions;
using TeamBalancer.Core.Services.Csv;
using TeamBalancer.Core.Services.Interfaces;
using TeamBalancer.Core.Tests.TestSupport;

/// <summary>
/// Covers the list metadata store: adding, renaming and deleting lists, the rule that the last
/// one has to stay, and the round trip through lists.csv.
/// </summary>
public class CsvPlayerListRepositoryTests
{
    private const string StorageHeader = "Name,Speed,TechnicalSkills,Stamina,PrimaryPosition,SecondaryPosition,IsSelected";

    private static IPlayerListRepository CreateRepository(TempDataDirectory directory) =>
        new CsvPlayerListRepository(new CsvParser(NullLogger<CsvParser>.Instance), directory.Path_);

    [Fact]
    public async Task GetAllAsync_FirstRun_ReturnsTheOneDefaultList()
    {
        using var directory = new TempDataDirectory();
        var repository = CreateRepository(directory);

        var list = Assert.Single(await repository.GetAllAsync());

        Assert.Equal(PlayerListFiles.DefaultListId, list.Id);
        Assert.Equal(CsvPlayerListRepository.DefaultListName, list.Name);
        Assert.True(File.Exists(directory.ListsPath));
    }

    [Fact]
    public async Task AddAsync_ReturnsTheListAndGivesItAnEmptyPlayerFile()
    {
        using var directory = new TempDataDirectory();
        var repository = CreateRepository(directory);

        var added = await repository.AddAsync("Sunday League");

        Assert.Equal("Sunday League", added.Name);
        Assert.NotEqual(Guid.Empty, added.Id);
        Assert.NotEqual(PlayerListFiles.DefaultListId, added.Id);
        Assert.Equal(added.CreatedAt, added.UpdatedAt);

        // The file has to exist and be empty, or the first switch to this list would fall back
        // to the embedded starter roster instead of showing no players.
        var playerFilePath = directory.PlayerFilePath(added.Id);
        Assert.True(File.Exists(playerFilePath));
        Assert.Equal($"{StorageHeader}{Environment.NewLine}", await File.ReadAllTextAsync(playerFilePath));
    }

    [Fact]
    public async Task AddAsync_ListsAreReturnedOldestFirst()
    {
        using var directory = new TempDataDirectory();
        var repository = CreateRepository(directory);

        var first = await repository.AddAsync("Sunday");
        var second = await repository.AddAsync("Work");

        var lists = (await repository.GetAllAsync()).ToList();

        Assert.Equal(3, lists.Count);
        Assert.Equal(PlayerListFiles.DefaultListId, lists[0].Id);
        Assert.Equal(first.Id, lists[1].Id);
        Assert.Equal(second.Id, lists[2].Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Has,Comma")]
    [InlineData("Has\"Quote")]
    [InlineData("=Formula")]
    [InlineData(" Leading")]
    [InlineData("Sixteen chars!!!!")]
    public async Task AddAsync_InvalidName_Throws(string name)
    {
        using var directory = new TempDataDirectory();
        var repository = CreateRepository(directory);

        await Assert.ThrowsAsync<ArgumentException>(() => repository.AddAsync(name));

        // A rejected name must not leave a half-created list behind.
        Assert.Single(await repository.GetAllAsync());
    }

    [Fact]
    public async Task RenameAsync_ChangesTheNameAndTouchesNoFileButListsCsv()
    {
        using var directory = new TempDataDirectory();
        var repository = CreateRepository(directory);

        var added = await repository.AddAsync("Sunday");
        await File.WriteAllTextAsync(directory.PlayerFilePath(added.Id), $"{StorageHeader}\nKeeper,2,2,2,Goalkeeper,,True\n");

        var renamed = await repository.RenameAsync(added.Id, "Sunday XI");

        Assert.Equal("Sunday XI", renamed.Name);
        Assert.True(renamed.UpdatedAt >= renamed.CreatedAt);
        Assert.Contains("Keeper", await File.ReadAllTextAsync(directory.PlayerFilePath(added.Id)));
    }

    [Fact]
    public async Task RenameAsync_UnknownList_Throws()
    {
        using var directory = new TempDataDirectory();
        var repository = CreateRepository(directory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.RenameAsync(Guid.NewGuid(), "Anything"));
    }

    [Fact]
    public async Task RenameAsync_InvalidName_ThrowsAndKeepsTheOldName()
    {
        using var directory = new TempDataDirectory();
        var repository = CreateRepository(directory);

        var added = await repository.AddAsync("Sunday");

        await Assert.ThrowsAsync<ArgumentException>(() => repository.RenameAsync(added.Id, "Has,Comma"));

        var reloaded = await repository.GetByIdAsync(added.Id);
        Assert.Equal("Sunday", reloaded!.Name);
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheRowAndThePlayerFile()
    {
        using var directory = new TempDataDirectory();
        var repository = CreateRepository(directory);

        var added = await repository.AddAsync("Sunday");
        var playerFilePath = directory.PlayerFilePath(added.Id);
        Assert.True(File.Exists(playerFilePath));

        await repository.DeleteAsync(added.Id);

        Assert.Null(await repository.GetByIdAsync(added.Id));
        Assert.False(File.Exists(playerFilePath));
    }

    /// <summary>
    /// The legacy file is the one player file a delete leaves behind, so an install rolled
    /// back to a pre-multi-list build still finds the user's original squad.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_DefaultList_LeavesTheLegacyPlayerFileOnDisk()
    {
        using var directory = new TempDataDirectory();
        directory.WriteLegacyPlayerFile($"{StorageHeader}\nAndriy,3,3,2,Forward,,True\n");
        var repository = CreateRepository(directory);

        await repository.AddAsync("Sunday");
        await repository.DeleteAsync(PlayerListFiles.DefaultListId);

        Assert.Null(await repository.GetByIdAsync(PlayerListFiles.DefaultListId));
        Assert.Contains("Andriy", directory.Read(PlayerListFiles.LegacyPlayerFileName));
    }

    [Fact]
    public async Task DeleteAsync_LastRemainingList_Throws()
    {
        using var directory = new TempDataDirectory();
        var repository = CreateRepository(directory);

        var only = Assert.Single(await repository.GetAllAsync());

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.DeleteAsync(only.Id));

        Assert.Single(await repository.GetAllAsync());
    }

    [Fact]
    public async Task DeleteAsync_UnknownList_Throws()
    {
        using var directory = new TempDataDirectory();
        var repository = CreateRepository(directory);

        await repository.AddAsync("Sunday");

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.DeleteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Lists_SurviveAReloadFromDisk()
    {
        using var directory = new TempDataDirectory();
        var repository = CreateRepository(directory);

        var sunday = await repository.AddAsync("Sunday");
        var work = await repository.AddAsync("Work");
        await repository.RenameAsync(work.Id, "Work 5-a-side");
        await repository.DeleteAsync(PlayerListFiles.DefaultListId);

        var reloaded = (await CreateRepository(directory).GetAllAsync()).ToList();

        Assert.Equal(2, reloaded.Count);
        Assert.Equal(sunday.Id, reloaded[0].Id);
        Assert.Equal("Sunday", reloaded[0].Name);
        Assert.Equal(work.Id, reloaded[1].Id);
        Assert.Equal("Work 5-a-side", reloaded[1].Name);

        // Timestamps have to come back as the UTC instants they were written as, not be
        // reinterpreted in the machine's local time.
        Assert.Equal(DateTimeKind.Utc, reloaded[0].CreatedAt.Kind);
        Assert.Equal(sunday.CreatedAt, reloaded[0].CreatedAt);
    }

    [Fact]
    public async Task ListsFile_UsesTheDocumentedHeader()
    {
        using var directory = new TempDataDirectory();
        var repository = CreateRepository(directory);

        await repository.GetAllAsync();

        Assert.StartsWith("Id,Name,CreatedAt,UpdatedAt", directory.Read(PlayerListFiles.ListsFileName));
    }

    /// <summary>
    /// A lists.csv that has been emptied or hand-edited into unusable rows would otherwise
    /// leave the app with no list at all, which nothing downstream can work with.
    /// </summary>
    [Fact]
    public async Task GetAllAsync_UnusableListsFile_FallsBackToTheDefaultList()
    {
        using var directory = new TempDataDirectory();
        await File.WriteAllTextAsync(directory.ListsPath, "Id,Name,CreatedAt,UpdatedAt\nnot-a-guid,Broken,nonsense,nonsense\n");

        var list = Assert.Single(await CreateRepository(directory).GetAllAsync());

        Assert.Equal(PlayerListFiles.DefaultListId, list.Id);
    }
}
