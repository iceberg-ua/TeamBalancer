namespace TeamBalancer.Core.Tests.Services.Csv;

using Microsoft.Extensions.Logging.Abstractions;
using TeamBalancer.Core.Services.Csv;
using TeamBalancer.Core.Services.Interfaces;
using TeamBalancer.Core.Tests.TestSupport;

/// <summary>
/// Covers the upgrade from single-list storage. The whole point of the design is that it is a
/// metadata-only change: one row is added that adopts the player file already on disk, and
/// that file is not moved, renamed or rewritten.
/// </summary>
public class PlayerListMigrationTests
{
    private const string StorageHeader = "Name,Speed,TechnicalSkills,Stamina,PrimaryPosition,SecondaryPosition,IsSelected";

    /// <summary>
    /// The player file a pre-multi-list install leaves behind: a real squad, with the
    /// selection state the user last left it in.
    /// </summary>
    private const string LegacyPlayers =
        $"{StorageHeader}\n" +
        "Andriy,3,3,2,Forward,Midfielder,True\n" +
        "Bohdan,1,2,3,Defender,,False\n" +
        "Vitaliy,2,2,2,Goalkeeper,,True\n";

    private static ICsvParser CreateParser() => new CsvParser(NullLogger<CsvParser>.Instance);

    [Fact]
    public async Task UpgradeFromSingleList_ProducesOneListPointingAtTheLegacyFile()
    {
        using var directory = new TempDataDirectory();
        directory.WriteLegacyPlayerFile(LegacyPlayers);

        var lists = new CsvPlayerListRepository(CreateParser(), directory.Path_);

        var list = Assert.Single(await lists.GetAllAsync());

        Assert.Equal(PlayerListFiles.DefaultListId, list.Id);
        Assert.Equal(CsvPlayerListRepository.DefaultListName, list.Name);
        Assert.Equal(
            Path.Combine(directory.Path_, PlayerListFiles.LegacyPlayerFileName),
            PlayerListFiles.PlayerFilePath(directory.Path_, list.Id));
    }

    [Fact]
    public async Task UpgradeFromSingleList_LeavesThePlayerFileByteForByteUntouched()
    {
        using var directory = new TempDataDirectory();
        directory.WriteLegacyPlayerFile(LegacyPlayers);

        var lists = new CsvPlayerListRepository(CreateParser(), directory.Path_);
        await lists.GetAllAsync();

        Assert.Equal(LegacyPlayers, directory.Read(PlayerListFiles.LegacyPlayerFileName));
    }

    [Fact]
    public async Task UpgradeFromSingleList_KeepsEveryPlayerAndTheirSelection()
    {
        using var directory = new TempDataDirectory();
        directory.WriteLegacyPlayerFile(LegacyPlayers);

        var parser = CreateParser();
        IPlayerListRepository lists = new CsvPlayerListRepository(parser, directory.Path_);
        var active = new ActivePlayerRepository(parser, lists, new FakeCurrentListPreference(), directory.Path_);

        var players = (await active.GetAllAsync()).ToList();

        Assert.Equal(3, players.Count);
        Assert.Equal(PlayerListFiles.DefaultListId, active.CurrentListId);
        Assert.Contains(players, p => p.Name == "Andriy" && p.Speed == 3 && p.TechnicalSkills == 3 && p.Stamina == 2);
        Assert.True(Assert.Single(players, p => p.Name == "Vitaliy").IsSelected);
        Assert.False(Assert.Single(players, p => p.Name == "Bohdan").IsSelected);
    }

    [Fact]
    public async Task UpgradeFromSingleList_RunsOnceAndThenLeavesListsCsvAlone()
    {
        using var directory = new TempDataDirectory();
        directory.WriteLegacyPlayerFile(LegacyPlayers);

        var firstRun = new CsvPlayerListRepository(CreateParser(), directory.Path_);
        await firstRun.AddAsync("Sunday");

        // A second run reads the lists.csv the first one wrote. If it re-migrated, the list
        // added in between would be gone.
        var secondRun = new CsvPlayerListRepository(CreateParser(), directory.Path_);
        var lists = (await secondRun.GetAllAsync()).ToList();

        Assert.Equal(2, lists.Count);
        Assert.Equal(PlayerListFiles.DefaultListId, lists[0].Id);
        Assert.Equal("Sunday", lists[1].Name);
    }

    /// <summary>
    /// A fresh install has no player file either, and takes the same path: one list adopting
    /// players.csv, which CsvPlayerRepository then creates from the embedded starter roster
    /// exactly as it did before lists existed.
    /// </summary>
    [Fact]
    public async Task FreshInstall_WithNoFilesAtAll_StillProducesTheDefaultList()
    {
        using var directory = new TempDataDirectory();

        var parser = CreateParser();
        IPlayerListRepository lists = new CsvPlayerListRepository(parser, directory.Path_);
        var active = new ActivePlayerRepository(parser, lists, new FakeCurrentListPreference(), directory.Path_);

        var list = Assert.Single(await lists.GetAllAsync());
        await active.GetAllAsync();

        Assert.Equal(PlayerListFiles.DefaultListId, list.Id);
        Assert.True(File.Exists(directory.PlayerFilePath(PlayerListFiles.DefaultListId)));
    }
}
