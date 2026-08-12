namespace TeamBalancer.Core.Tests.Services.Csv;

using Microsoft.Extensions.Logging.Abstractions;
using TeamBalancer.Core.Models;
using TeamBalancer.Core.Services.Csv;
using TeamBalancer.Core.Services.Interfaces;
using TeamBalancer.Core.Tests.TestSupport;

/// <summary>
/// Covers the repository the screens actually inject: which list it opens on, what switching
/// lists does to the players it serves, and what deleting the active list does to it.
/// </summary>
public class ActivePlayerRepositoryTests
{
    private const string StorageHeader = "Name,Speed,TechnicalSkills,Stamina,PrimaryPosition,SecondaryPosition,IsSelected";

    private static (ActivePlayerRepository Active, IPlayerListRepository Lists, FakeCurrentListPreference Preference)
        CreateStack(TempDataDirectory directory, Guid? storedPreference = null)
    {
        ICsvParser parser = new CsvParser(NullLogger<CsvParser>.Instance);
        IPlayerListRepository lists = new CsvPlayerListRepository(parser, directory.Path_);
        var preference = new FakeCurrentListPreference(storedPreference);
        var active = new ActivePlayerRepository(parser, lists, preference, directory.Path_);

        return (active, lists, preference);
    }

    /// <summary>
    /// Writes a list's player file directly, standing in for players added on an earlier run.
    /// </summary>
    private static Task WritePlayersAsync(string filePath, params string[] names)
    {
        var rows = names.Select(name => $"{name},2,2,2,Midfielder,,True");

        return File.WriteAllTextAsync(filePath, $"{StorageHeader}\n{string.Join('\n', rows)}\n");
    }

    [Fact]
    public async Task NoStoredPreference_OpensTheOldestList()
    {
        using var directory = new TempDataDirectory();
        directory.WriteLegacyPlayerFile($"{StorageHeader}\nAndriy,2,2,2,Midfielder,,True\n");

        var (active, lists, _) = CreateStack(directory);
        var sunday = await lists.AddAsync("Sunday");
        await WritePlayersAsync(directory.PlayerFilePath(sunday.Id), "Bohdan");

        var players = await active.GetAllAsync();

        Assert.Equal(PlayerListFiles.DefaultListId, active.CurrentListId);
        Assert.Equal("Andriy", Assert.Single(players).Name);
    }

    [Fact]
    public async Task StoredPreference_OpensTheListItNames()
    {
        using var directory = new TempDataDirectory();
        directory.WriteLegacyPlayerFile($"{StorageHeader}\nAndriy,2,2,2,Midfielder,,True\n");

        // The list has to exist before the repository under test resolves, so it is added
        // through a stack that is then thrown away - only the files matter.
        var (_, setupLists, _) = CreateStack(directory);
        var sunday = await setupLists.AddAsync("Sunday");
        await WritePlayersAsync(directory.PlayerFilePath(sunday.Id), "Bohdan");

        var (active, _, _) = CreateStack(directory, storedPreference: sunday.Id);

        var players = await active.GetAllAsync();

        Assert.Equal(sunday.Id, active.CurrentListId);
        Assert.Equal("Bohdan", Assert.Single(players).Name);
    }

    /// <summary>
    /// A stored id can name a list that is gone - deleted on this device, or, once lists are
    /// shared, on another one. Falling back beats opening nothing.
    /// </summary>
    [Fact]
    public async Task StalePreference_FallsBackToTheOldestList()
    {
        using var directory = new TempDataDirectory();
        directory.WriteLegacyPlayerFile($"{StorageHeader}\nAndriy,2,2,2,Midfielder,,True\n");

        var (active, _, _) = CreateStack(directory, storedPreference: Guid.NewGuid());

        var players = await active.GetAllAsync();

        Assert.Equal(PlayerListFiles.DefaultListId, active.CurrentListId);
        Assert.Equal("Andriy", Assert.Single(players).Name);
    }

    [Fact]
    public async Task SwitchListAsync_SwapsThePlayersAndFiresListChanged()
    {
        using var directory = new TempDataDirectory();
        directory.WriteLegacyPlayerFile($"{StorageHeader}\nAndriy,2,2,2,Midfielder,,True\n");

        var (active, lists, _) = CreateStack(directory);
        var sunday = await lists.AddAsync("Sunday");
        await WritePlayersAsync(directory.PlayerFilePath(sunday.Id), "Bohdan", "Vitaliy");

        var changes = 0;
        active.ListChanged += () => changes++;

        Assert.Equal("Andriy", Assert.Single(await active.GetAllAsync()).Name);

        await active.SwitchListAsync(sunday.Id);

        var players = (await active.GetAllAsync()).Select(p => p.Name).ToList();

        Assert.Equal(1, changes);
        Assert.Equal(sunday.Id, active.CurrentListId);
        Assert.Equal(new[] { "Bohdan", "Vitaliy" }, players);
    }

    [Fact]
    public async Task SwitchListAsync_RemembersTheChoiceForTheNextLaunch()
    {
        using var directory = new TempDataDirectory();
        var (active, lists, preference) = CreateStack(directory);
        var sunday = await lists.AddAsync("Sunday");

        await active.SwitchListAsync(sunday.Id);

        Assert.Equal(sunday.Id, preference.Stored);
    }

    /// <summary>
    /// Selections and edits sit in memory until something saves them, normally the app going
    /// to the background. Switching drops the repository holding them, so the switch itself
    /// has to write them out first.
    /// </summary>
    [Fact]
    public async Task SwitchListAsync_SavesTheOutgoingListsPendingChanges()
    {
        using var directory = new TempDataDirectory();
        directory.WriteLegacyPlayerFile($"{StorageHeader}\n");

        var (active, lists, _) = CreateStack(directory);
        var sunday = await lists.AddAsync("Sunday");

        await active.AddAsync(TestPlayers.Create("Andriy", Position.Midfielder));

        await active.SwitchListAsync(sunday.Id);
        Assert.Empty(await active.GetAllAsync());

        await active.SwitchListAsync(PlayerListFiles.DefaultListId);

        Assert.Equal("Andriy", Assert.Single(await active.GetAllAsync()).Name);
    }

    [Fact]
    public async Task SwitchListAsync_ToTheListAlreadyOpen_DoesNotFire()
    {
        using var directory = new TempDataDirectory();
        var (active, _, preference) = CreateStack(directory);

        await active.GetAllAsync();

        var changes = 0;
        active.ListChanged += () => changes++;

        await active.SwitchListAsync(PlayerListFiles.DefaultListId);

        Assert.Equal(0, changes);

        // The choice is still stored: picking the list the app happened to open on is how a
        // user pins it.
        Assert.Equal(PlayerListFiles.DefaultListId, preference.Stored);
    }

    [Fact]
    public async Task SwitchListAsync_UnknownList_Throws()
    {
        using var directory = new TempDataDirectory();
        var (active, _, _) = CreateStack(directory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => active.SwitchListAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteListAsync_ActiveList_SwitchesToTheOldestRemainingListFirst()
    {
        using var directory = new TempDataDirectory();
        directory.WriteLegacyPlayerFile($"{StorageHeader}\nAndriy,2,2,2,Midfielder,,True\n");

        var (active, lists, _) = CreateStack(directory);
        var sunday = await lists.AddAsync("Sunday");
        await WritePlayersAsync(directory.PlayerFilePath(sunday.Id), "Bohdan");
        await active.SwitchListAsync(sunday.Id);

        var changes = 0;
        active.ListChanged += () => changes++;

        await active.DeleteListAsync(sunday.Id);

        Assert.Equal(1, changes);
        Assert.Equal(PlayerListFiles.DefaultListId, active.CurrentListId);
        Assert.Equal("Andriy", Assert.Single(await active.GetAllAsync()).Name);
        Assert.Null(await lists.GetByIdAsync(sunday.Id));
        Assert.False(File.Exists(directory.PlayerFilePath(sunday.Id)));
    }

    [Fact]
    public async Task DeleteListAsync_AnotherList_LeavesTheActiveOneAlone()
    {
        using var directory = new TempDataDirectory();
        directory.WriteLegacyPlayerFile($"{StorageHeader}\nAndriy,2,2,2,Midfielder,,True\n");

        var (active, lists, _) = CreateStack(directory);
        var sunday = await lists.AddAsync("Sunday");
        await active.GetAllAsync();

        var changes = 0;
        active.ListChanged += () => changes++;

        await active.DeleteListAsync(sunday.Id);

        Assert.Equal(0, changes);
        Assert.Equal(PlayerListFiles.DefaultListId, active.CurrentListId);
        Assert.Equal("Andriy", Assert.Single(await active.GetAllAsync()).Name);
    }

    [Fact]
    public async Task DeleteListAsync_LastRemainingList_Throws()
    {
        using var directory = new TempDataDirectory();
        var (active, lists, _) = CreateStack(directory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => active.DeleteListAsync(PlayerListFiles.DefaultListId));

        Assert.Single(await lists.GetAllAsync());
        Assert.Equal(PlayerListFiles.DefaultListId, active.CurrentListId);
    }
}
