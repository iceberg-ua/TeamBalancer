namespace TeamBalancer.Core.Tests.Services.Csv;

using Microsoft.Extensions.Logging.Abstractions;
using TeamBalancer.Core.Models;
using TeamBalancer.Core.Services.Csv;
using TeamBalancer.Core.Services.Interfaces;
using TeamBalancer.Core.Tests.TestSupport;

/// <summary>
/// Covers importing into a list the app has just created and switched to, which is what
/// receiving a shared squad does.
/// </summary>
/// <remarks>
/// This sequence used to lose every player. Switching lists raises ListChanged, the home screen
/// answers it by reloading its players, and that reload landed in the middle of the import -
/// both of them finding an unopened repository and both loading the file. The reload's copy of
/// the (empty) new list won, replacing the players the import had already added, and the save
/// that followed wrote the empty list to disk. The repository caches its load now, so the two
/// share one, but the sequence is worth a test of its own: nothing about it looks dangerous from
/// the screen that performs it.
/// </remarks>
public class ImportAfterListSwitchTests
{
    private const string ExportHeader = "Name,Speed,TechnicalSkills,Stamina,PrimaryPosition,SecondaryPosition";

    private static (ActivePlayerRepository Active, IPlayerListRepository Lists, CsvImportExportService Import)
        CreateStack(TempDataDirectory directory)
    {
        ICsvParser parser = new CsvParser(NullLogger<CsvParser>.Instance);
        IPlayerListRepository lists = new CsvPlayerListRepository(parser, directory.Path_);
        var active = new ActivePlayerRepository(parser, lists, new FakeCurrentListPreference(), directory.Path_);
        var import = new CsvImportExportService(active, parser, NullLogger<CsvImportExportService>.Instance);

        return (active, lists, import);
    }

    [Fact]
    public async Task CreateSwitchAndImport_PlayersLandInTheNewList()
    {
        using var directory = new TempDataDirectory();
        var (active, lists, import) = CreateStack(directory);

        var created = await lists.AddAsync("Sunday League");
        await active.SwitchListAsync(created.Id);

        var result = await import.ImportPlayersAsync($"{ExportHeader}\nAlice,3,2,1,Defender,\nBob,2,2,2,Forward,\n");

        Assert.Equal(2, result.ImportedCount);
        Assert.Equal(2, (await active.GetAllAsync()).Count());
        Assert.Equal(created.Id, active.CurrentListId);
    }

    [Fact]
    public async Task CreateSwitchAndImport_WithAListChangedHandlerReloading_KeepsThePlayers()
    {
        // The handler stands in for the home screen, which reloads its players the moment the
        // active list changes. Reading on the event is the whole point: it puts a second reader
        // on the new list's repository while the import is still filling it.
        using var directory = new TempDataDirectory();
        var (active, lists, import) = CreateStack(directory);

        var reloads = new List<Task<IEnumerable<Player>>>();
        active.ListChanged += () => reloads.Add(active.GetAllAsync());

        var created = await lists.AddAsync("Sunday League");
        await active.SwitchListAsync(created.Id);

        var result = await import.ImportPlayersAsync($"{ExportHeader}\nAlice,3,2,1,Defender,\nBob,2,2,2,Forward,\n");
        await Task.WhenAll(reloads);

        Assert.Equal(2, result.ImportedCount);
        Assert.Equal(2, (await active.GetAllAsync()).Count());
    }

    [Fact]
    public async Task CreateSwitchAndImport_PlayersSurviveReload()
    {
        // The save at the end of an import is what the race actually corrupted: it wrote the
        // emptied in-memory list over the file. Reading the file back is the only way to catch
        // that, because the objects in memory can look right while the file does not.
        using var directory = new TempDataDirectory();
        var (active, lists, import) = CreateStack(directory);

        active.ListChanged += () => _ = active.GetAllAsync();

        var created = await lists.AddAsync("Sunday League");
        await active.SwitchListAsync(created.Id);
        await import.ImportPlayersAsync($"{ExportHeader}\nAlice,3,2,1,Defender,\nBob,2,2,2,Forward,\n");

        var onDisk = await File.ReadAllTextAsync(directory.PlayerFilePath(created.Id));

        Assert.Contains("Alice", onDisk);
        Assert.Contains("Bob", onDisk);
    }

    [Fact]
    public async Task ConcurrentReadsDuringAnImport_DoNotDiscardWhatWasAdded()
    {
        // The narrow version of the same defect, with nothing else in the way: a read racing an
        // import against a repository neither has opened yet.
        using var directory = new TempDataDirectory();
        var (active, lists, import) = CreateStack(directory);

        var created = await lists.AddAsync("Sunday League");
        await active.SwitchListAsync(created.Id);

        var importing = import.ImportPlayersAsync($"{ExportHeader}\nAlice,3,2,1,Defender,\nBob,2,2,2,Forward,\n");
        var reading = active.GetAllAsync();

        await Task.WhenAll(importing, reading);

        Assert.Equal(2, (await active.GetAllAsync()).Count());
    }

    [Fact]
    public async Task ImportIntoAnExistingList_MergesIntoTheOneSwitchedTo()
    {
        // The other half of the destination dialog: an existing list, reached by switching to
        // it, merged rather than added to.
        using var directory = new TempDataDirectory();
        var (active, lists, import) = CreateStack(directory);

        var target = await lists.AddAsync("Work 5-a-side");
        await active.SwitchListAsync(target.Id);
        await import.ImportPlayersAsync($"{ExportHeader}\nAlice,1,1,1,Defender,\n");

        var other = await lists.AddAsync("Sunday League");
        await active.SwitchListAsync(other.Id);
        await active.SwitchListAsync(target.Id);

        var result = await import.ImportPlayersAsync(
            $"{ExportHeader}\nAlice,3,3,3,Midfielder,\nTaras,2,2,2,Forward,\n",
            ImportMode.Merge);

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(1, result.UpdatedCount);

        var alice = await active.GetByNameAsync("Alice");
        Assert.NotNull(alice);
        Assert.Equal(3, alice!.Speed);
    }
}
