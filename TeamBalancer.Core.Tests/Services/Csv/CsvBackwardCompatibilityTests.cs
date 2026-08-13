namespace TeamBalancer.Core.Tests.Services.Csv;

using Microsoft.Extensions.Logging.Abstractions;
using TeamBalancer.Core.Models;
using TeamBalancer.Core.Services.Csv;
using TeamBalancer.Core.Services.Interfaces;
using TeamBalancer.Core.Tests.TestSupport;

/// <summary>
/// End-to-end import/export checks through <see cref="CsvImportExportService"/>, covering the
/// pre-position CSV format that shipped before Phase 1.
/// </summary>
public class CsvBackwardCompatibilityTests
{
    private const string StorageHeader = "Name,Speed,TechnicalSkills,Stamina,PrimaryPosition,SecondaryPosition,IsSelected";

    /// <summary>
    /// Path to the checked-in pre-Phase-1 sample (Name,Speed,TechnicalSkills,Stamina), copied
    /// next to the test assembly at build time.
    /// </summary>
    private static string LegacyFixturePath =>
        Path.Combine(AppContext.BaseDirectory, "TestData", "players-legacy.csv");

    private static (CsvImportExportService Service, IPlayerRepository Repository) CreateStack(string filePath)
    {
        ICsvParser parser = new CsvParser(NullLogger<CsvParser>.Instance);
        IPlayerRepository repository = new CsvPlayerRepository(parser, filePath);
        var service = new CsvImportExportService(repository, parser, NullLogger<CsvImportExportService>.Instance);

        return (service, repository);
    }

    [Fact]
    public void LegacyFixture_IsPresentAndUsesThePrePositionHeader()
    {
        Assert.True(File.Exists(LegacyFixturePath), $"Missing fixture: {LegacyFixturePath}");

        var firstLine = File.ReadLines(LegacyFixturePath).First();

        Assert.Equal("Name,Speed,TechnicalSkills,Stamina", firstLine);
    }

    [Fact]
    public async Task ImportPlayersAsync_LegacyCsvFile_ImportsEveryRowAsUnspecified()
    {
        using var file = new TempCsvFile($"{StorageHeader}\n");
        var (service, repository) = CreateStack(file.Path_);

        var legacyCsv = await File.ReadAllTextAsync(LegacyFixturePath);

        var imported = await service.ImportPlayersAsync(legacyCsv);

        Assert.Equal(5, imported.ImportedCount);
        Assert.Equal(0, imported.SkippedCount);

        var players = (await repository.GetAllAsync()).ToList();
        Assert.Equal(5, players.Count);
        Assert.All(players, p =>
        {
            Assert.Equal(Position.Unspecified, p.PrimaryPosition);
            Assert.Null(p.SecondaryPosition);
        });
        Assert.Contains(players, p => p.Name == "Andriy" && p.Speed == 3 && p.TechnicalSkills == 3 && p.Stamina == 2);
    }

    [Fact]
    public async Task ImportPlayersAsync_LegacyCsv_SurvivesReloadFromDisk()
    {
        using var file = new TempCsvFile($"{StorageHeader}\n");
        var (service, _) = CreateStack(file.Path_);

        await service.ImportPlayersAsync(await File.ReadAllTextAsync(LegacyFixturePath));

        // The repository rewrites the file in the new storage format on save.
        var (_, reloaded) = CreateStack(file.Path_);
        var players = (await reloaded.GetAllAsync()).ToList();

        Assert.Equal(5, players.Count);
        Assert.All(players, p => Assert.Equal(Position.Unspecified, p.PrimaryPosition));
        Assert.StartsWith(StorageHeader, file.Read());
    }

    [Fact]
    public async Task ExportThenImport_PreservesNamesSkillsAndBothPositions()
    {
        using var sourceFile = new TempCsvFile($"{StorageHeader}\n");
        var (sourceService, sourceRepository) = CreateStack(sourceFile.Path_);

        var seeded = new List<Player>
        {
            TestPlayers.Create("Keeper", Position.Goalkeeper, Position.Defender, 3, 2, 1),
            TestPlayers.Create("Anchor", Position.Defender, secondary: null, 1, 3, 2),
            TestPlayers.Create("Engine", Position.Midfielder, Position.Forward, 2, 3, 3),
            TestPlayers.Create("Poacher", Position.Forward, secondary: null, 3, 3, 2),
            TestPlayers.Create("Nomad", Position.Unspecified, secondary: null, 2, 2, 2)
        };

        foreach (var player in seeded)
        {
            await sourceRepository.AddAsync(player);
        }

        await sourceRepository.SaveChangesAsync();

        var exported = await sourceService.ExportPlayersAsync();

        // Import into a completely separate repository: importing into the same one would be
        // rejected as duplicate names.
        using var targetFile = new TempCsvFile($"{StorageHeader}\n");
        var (targetService, targetRepository) = CreateStack(targetFile.Path_);

        var importedCount = await targetService.ImportPlayersAsync(exported);
        Assert.Equal(seeded.Count, importedCount.ImportedCount);

        var result = (await targetRepository.GetAllAsync()).ToList();
        Assert.Equal(seeded.Count, result.Count);

        foreach (var original in seeded)
        {
            var copy = Assert.Single(result, p => p.Name == original.Name);

            Assert.Equal(original.Speed, copy.Speed);
            Assert.Equal(original.TechnicalSkills, copy.TechnicalSkills);
            Assert.Equal(original.Stamina, copy.Stamina);
            Assert.Equal(original.PrimaryPosition, copy.PrimaryPosition);
            Assert.Equal(original.SecondaryPosition, copy.SecondaryPosition);
        }
    }

    /// <summary>
    /// Selection state is deliberately outside the export contract: the export header omits
    /// IsSelected and <see cref="CsvImportExportService.ImportPlayersAsync"/> deselects
    /// everything it imports. This test pins that intent so a future change to the export
    /// format has to be a conscious one.
    /// </summary>
    [Fact]
    public async Task ExportThenImport_LeavesImportedPlayersDeselected()
    {
        using var sourceFile = new TempCsvFile($"{StorageHeader}\n");
        var (sourceService, sourceRepository) = CreateStack(sourceFile.Path_);

        var selected = TestPlayers.Create("Chosen", Position.Midfielder);
        selected.IsSelected = true;
        await sourceRepository.AddAsync(selected);
        await sourceRepository.SaveChangesAsync();

        var exported = await sourceService.ExportPlayersAsync();
        Assert.DoesNotContain("IsSelected", exported);

        using var targetFile = new TempCsvFile($"{StorageHeader}\n");
        var (targetService, targetRepository) = CreateStack(targetFile.Path_);
        await targetService.ImportPlayersAsync(exported);

        var imported = (await targetRepository.GetAllAsync()).Single();

        Assert.Equal(Position.Midfielder, imported.PrimaryPosition);
        Assert.False(imported.IsSelected);
    }

    [Fact]
    public async Task ImportPlayersAsync_MixedOldAndNewRows_ImportsBoth()
    {
        using var file = new TempCsvFile($"{StorageHeader}\n");
        var (service, repository) = CreateStack(file.Path_);

        // A hand-edited CSV where only some rows carry position columns.
        var mixed = "Name,Speed,TechnicalSkills,Stamina,PrimaryPosition,SecondaryPosition\n"
                  + "WithPos,2,2,2,Defender,Midfielder\n"
                  + "NoPos,3,1,2\n";

        var imported = await service.ImportPlayersAsync(mixed);

        Assert.Equal(2, imported.ImportedCount);

        var players = (await repository.GetAllAsync()).ToList();
        var withPosition = Assert.Single(players, p => p.Name == "WithPos");
        var withoutPosition = Assert.Single(players, p => p.Name == "NoPos");

        Assert.Equal(Position.Defender, withPosition.PrimaryPosition);
        Assert.Equal(Position.Midfielder, withPosition.SecondaryPosition);
        Assert.Equal(Position.Unspecified, withoutPosition.PrimaryPosition);
        Assert.Null(withoutPosition.SecondaryPosition);
    }
}
