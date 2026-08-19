namespace TeamBalancer.Core.Tests.Services.Csv;

using Microsoft.Extensions.Logging.Abstractions;
using TeamBalancer.Core.Models;
using TeamBalancer.Core.Services.Csv;
using TeamBalancer.Core.Services.Interfaces;
using TeamBalancer.Core.Tests.TestSupport;

/// <summary>
/// Covers importing into a list that already holds players. Sharing a squad makes this the
/// ordinary case rather than the exception: the organiser adjusts a few ratings and sends the
/// same squad again, and under the old add-only rule every player came back as a duplicate and
/// nothing changed at all.
/// </summary>
public class CsvImportMergeTests
{
    private const string StorageHeader = "Name,Speed,TechnicalSkills,Stamina,PrimaryPosition,SecondaryPosition,IsSelected";
    private const string ExportHeader = "Name,Speed,TechnicalSkills,Stamina,PrimaryPosition,SecondaryPosition";

    private static (CsvImportExportService Service, IPlayerRepository Repository) CreateStack(string filePath)
    {
        ICsvParser parser = new CsvParser(NullLogger<CsvParser>.Instance);
        IPlayerRepository repository = new CsvPlayerRepository(parser, filePath);
        var service = new CsvImportExportService(repository, parser, NullLogger<CsvImportExportService>.Instance);

        return (service, repository);
    }

    [Fact]
    public async Task Merge_ExistingPlayerWithNewRatings_IsUpdatedRatherThanSkipped()
    {
        using var file = new TempCsvFile($"{StorageHeader}\nAlice,1,1,1,Defender,,True\n");
        var (service, repository) = CreateStack(file.Path_);

        var result = await service.ImportPlayersAsync(
            $"{ExportHeader}\nAlice,3,2,3,Midfielder,Forward\n",
            ImportMode.Merge);

        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.DuplicateCount);
        Assert.Equal(0, result.ImportedCount);

        var alice = await repository.GetByNameAsync("Alice");
        Assert.NotNull(alice);
        Assert.Equal(3, alice!.Speed);
        Assert.Equal(2, alice.TechnicalSkills);
        Assert.Equal(3, alice.Stamina);
        Assert.Equal(Position.Midfielder, alice.PrimaryPosition);
        Assert.Equal(Position.Forward, alice.SecondaryPosition);
    }

    [Fact]
    public async Task Merge_ExistingPlayerWithIdenticalRatings_CountsAsUnchanged()
    {
        using var file = new TempCsvFile($"{StorageHeader}\nAlice,3,2,1,Defender,,True\n");
        var (service, _) = CreateStack(file.Path_);

        var result = await service.ImportPlayersAsync(
            $"{ExportHeader}\nAlice,3,2,1,Defender,\n",
            ImportMode.Merge);

        Assert.Equal(1, result.UnchangedCount);
        Assert.Equal(0, result.UpdatedCount);
        Assert.True(result.IsEntirelyUnchanged);
    }

    [Fact]
    public async Task Merge_PlayerMissingFromTheImport_IsKept()
    {
        // The sender not having someone is not the same as saying they left. Nothing a merge
        // does ever removes a player.
        using var file = new TempCsvFile($"{StorageHeader}\nAlice,1,1,1,Defender,,True\nOleh,2,2,2,Forward,,True\n");
        var (service, repository) = CreateStack(file.Path_);

        await service.ImportPlayersAsync($"{ExportHeader}\nAlice,3,3,3,Defender,\n", ImportMode.Merge);

        Assert.NotNull(await repository.GetByNameAsync("Oleh"));
    }

    [Fact]
    public async Task Merge_AddsNewPlayersAndUpdatesExistingOnes()
    {
        using var file = new TempCsvFile($"{StorageHeader}\nAlice,1,1,1,Defender,,True\n");
        var (service, repository) = CreateStack(file.Path_);

        var result = await service.ImportPlayersAsync(
            $"{ExportHeader}\nAlice,3,3,3,Defender,\nTaras,2,2,2,Forward,\n",
            ImportMode.Merge);

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(2, result.TotalRows);
        Assert.Equal(0, result.SkippedCount);

        Assert.Equal(2, (await repository.GetAllAsync()).Count());
    }

    [Fact]
    public async Task Merge_MatchesNamesRegardlessOfCase()
    {
        using var file = new TempCsvFile($"{StorageHeader}\nAlice,1,1,1,Defender,,True\n");
        var (service, repository) = CreateStack(file.Path_);

        var result = await service.ImportPlayersAsync(
            $"{ExportHeader}\nALICE,3,3,3,Defender,\n",
            ImportMode.Merge);

        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.ImportedCount);

        // The name already in the list wins: it is what the user typed, and the import matched
        // it rather than renaming it.
        var alice = await repository.GetByNameAsync("Alice");
        Assert.NotNull(alice);
        Assert.Equal("Alice", alice!.Name);
    }

    [Fact]
    public async Task Merge_LeavesSelectionAlone()
    {
        // Selection is about who is playing tonight, which is this phone's business and not
        // something a squad arriving from someone else gets to decide.
        using var file = new TempCsvFile($"{StorageHeader}\nAlice,1,1,1,Defender,,True\n");
        var (service, repository) = CreateStack(file.Path_);

        await service.ImportPlayersAsync($"{ExportHeader}\nAlice,3,3,3,Defender,\n", ImportMode.Merge);

        var alice = await repository.GetByNameAsync("Alice");
        Assert.NotNull(alice);
        Assert.True(alice!.IsSelected);
    }

    [Fact]
    public async Task Merge_UpdatesSurviveReload()
    {
        // A merge that only changed ratings adds no players, so saving on the added count alone
        // would drop every update the next time the list was read.
        using var file = new TempCsvFile($"{StorageHeader}\nAlice,1,1,1,Defender,,True\n");
        var (service, _) = CreateStack(file.Path_);

        await service.ImportPlayersAsync($"{ExportHeader}\nAlice,3,3,3,Midfielder,\n", ImportMode.Merge);

        var (_, reloaded) = CreateStack(file.Path_);
        var alice = await reloaded.GetByNameAsync("Alice");

        Assert.NotNull(alice);
        Assert.Equal(3, alice!.Speed);
        Assert.Equal(Position.Midfielder, alice.PrimaryPosition);
    }

    [Fact]
    public async Task AddOnly_RemainsTheDefaultAndStillSkipsDuplicates()
    {
        // Importing into a list the app has just created for the purpose has nothing to
        // reconcile, and the existing behaviour is what that wants.
        using var file = new TempCsvFile($"{StorageHeader}\nAlice,1,1,1,Defender,,True\n");
        var (service, repository) = CreateStack(file.Path_);

        var result = await service.ImportPlayersAsync($"{ExportHeader}\nAlice,3,3,3,Forward,\n");

        Assert.Equal(1, result.DuplicateCount);
        Assert.Equal(0, result.UpdatedCount);
        Assert.True(result.IsEntirelyDuplicates);

        var alice = await repository.GetByNameAsync("Alice");
        Assert.NotNull(alice);
        Assert.Equal(1, alice!.Speed);
    }
}
