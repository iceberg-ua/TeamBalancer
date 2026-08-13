namespace TeamBalancer.Core.Tests.Services.Csv;

using Microsoft.Extensions.Logging.Abstractions;
using TeamBalancer.Core.Models;
using TeamBalancer.Core.Services.Csv;
using TeamBalancer.Core.Services.Interfaces;
using TeamBalancer.Core.Tests.TestSupport;

/// <summary>
/// Covers what an import reports about the rows it did not take. Import used to return only the
/// number of players added, so a file that lost rows to long names or to players already in the
/// list still read as an unqualified success.
/// </summary>
public class CsvImportReportingTests
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
    public async Task ImportPlayersAsync_AllRowsGood_ReportsNothingSkipped()
    {
        using var file = new TempCsvFile($"{StorageHeader}\n");
        var (service, _) = CreateStack(file.Path_);

        var result = await service.ImportPlayersAsync($"{ExportHeader}\nAlice,3,2,1,Defender,\nBob,2,2,2,Forward,\n");

        Assert.Equal(2, result.ImportedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(2, result.TotalRows);
        Assert.False(result.IsEntirelyDuplicates);
    }

    [Fact]
    public async Task ImportPlayersAsync_NameTooLong_IsShortenedAndImported()
    {
        using var file = new TempCsvFile($"{StorageHeader}\n");
        var (service, repository) = CreateStack(file.Path_);

        var tooLong = new string('x', CsvSafeName.MaxLength + 5);
        var result = await service.ImportPlayersAsync($"{ExportHeader}\nAlice,3,2,1,Defender,\n{tooLong},2,2,2,Forward,\n");

        Assert.Equal(2, result.ImportedCount);
        Assert.Equal(1, result.TruncatedCount);
        Assert.Equal(0, result.InvalidNameCount);
        Assert.Equal(0, result.SkippedCount);

        var players = (await repository.GetAllAsync()).ToList();
        Assert.Single(players, p => p.Name == new string('x', CsvSafeName.MaxLength));
    }

    [Fact]
    public async Task ImportPlayersAsync_TruncationExposingATrailingSpace_TrimsIt()
    {
        using var file = new TempCsvFile($"{StorageHeader}\n");
        var (service, repository) = CreateStack(file.Path_);

        // 20 characters then a space: cutting at the limit would otherwise leave the name
        // ending in whitespace, which the name rules reject.
        var name = new string('x', CsvSafeName.MaxLength - 1) + " tail";
        var result = await service.ImportPlayersAsync($"{ExportHeader}\n{name},2,2,2,Forward,\n");

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(1, result.TruncatedCount);

        var player = Assert.Single(await repository.GetAllAsync());
        Assert.Equal(new string('x', CsvSafeName.MaxLength - 1), player.Name);
    }

    [Fact]
    public async Task ImportPlayersAsync_NameInvalidForAnotherReason_IsStillSkipped()
    {
        using var file = new TempCsvFile($"{StorageHeader}\n");
        var (service, _) = CreateStack(file.Path_);

        // Shortening cannot rescue a leading formula character.
        var result = await service.ImportPlayersAsync($"{ExportHeader}\n=Formula,2,2,2,Forward,\n");

        Assert.Equal(0, result.ImportedCount);
        Assert.Equal(1, result.InvalidNameCount);
        Assert.Equal(0, result.TruncatedCount);
    }

    [Fact]
    public async Task ImportPlayersAsync_TwoLongNamesSharingAPrefix_KeepsBothByNumberingTheSecond()
    {
        using var file = new TempCsvFile($"{StorageHeader}\n");
        var (service, repository) = CreateStack(file.Path_);

        // Both cut down to the same 20 characters, so the second needs a digit to stay apart.
        var prefix = new string('x', CsvSafeName.MaxLength);
        var result = await service.ImportPlayersAsync(
            $"{ExportHeader}\n{prefix}AAA,2,2,2,Forward,\n{prefix}BBB,3,3,3,Defender,\n");

        Assert.Equal(2, result.ImportedCount);
        Assert.Equal(2, result.TruncatedCount);
        Assert.Equal(1, result.NumberedCount);
        Assert.Equal(0, result.DuplicateCount);

        string[] expected = [new string('x', CsvSafeName.MaxLength), new string('x', CsvSafeName.MaxLength - 1) + "2"];
        var names = (await repository.GetAllAsync()).Select(p => p.Name).Order().ToList();
        Assert.Equal(expected.Order(), names);
    }

    [Fact]
    public async Task ImportPlayersAsync_NumberedNames_StayWithinTheLengthLimit()
    {
        using var file = new TempCsvFile($"{StorageHeader}\n");
        var (service, repository) = CreateStack(file.Path_);

        var prefix = new string('x', CsvSafeName.MaxLength);
        await service.ImportPlayersAsync(
            $"{ExportHeader}\n{prefix}AAA,2,2,2,Forward,\n{prefix}BBB,3,3,3,Defender,\n{prefix}CCC,1,1,1,Forward,\n");

        var players = await repository.GetAllAsync();
        Assert.All(players, p => Assert.True(p.Name.Length <= CsvSafeName.MaxLength, $"'{p.Name}' is {p.Name.Length} characters"));
        Assert.All(players, p => Assert.True(CsvSafeName.IsValid(p.Name)));
    }

    [Fact]
    public async Task ImportPlayersAsync_ManySharingAPrefix_NumbersThemInSequence()
    {
        using var file = new TempCsvFile($"{StorageHeader}\n");
        var (service, repository) = CreateStack(file.Path_);

        var prefix = new string('x', CsvSafeName.MaxLength);
        var rows = string.Concat(Enumerable.Range(0, 4).Select(i => $"{prefix}Suffix{i},2,2,2,Forward,\n"));

        var result = await service.ImportPlayersAsync($"{ExportHeader}\n{rows}");

        Assert.Equal(4, result.ImportedCount);
        Assert.Equal(3, result.NumberedCount);

        var stem = new string('x', CsvSafeName.MaxLength - 1);
        string[] expected = [new string('x', CsvSafeName.MaxLength), stem + "2", stem + "3", stem + "4"];
        var names = (await repository.GetAllAsync()).Select(p => p.Name).Order().ToList();
        Assert.Equal(expected.Order(), names);
    }

    [Fact]
    public async Task ImportPlayersAsync_ShortenedNameCollidingWithAPlayerAlreadyInTheList_IsNumberedNotSkipped()
    {
        using var file = new TempCsvFile($"{StorageHeader}\n");
        var (service, repository) = CreateStack(file.Path_);

        var existing = new string('x', CsvSafeName.MaxLength);
        await service.ImportPlayersAsync($"{ExportHeader}\n{existing},2,2,2,Forward,\n");

        var result = await service.ImportPlayersAsync($"{ExportHeader}\n{existing}AAA,3,3,3,Defender,\n");

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(1, result.NumberedCount);
        Assert.Equal(0, result.DuplicateCount);
        Assert.Equal(2, (await repository.GetAllAsync()).Count());
    }

    [Fact]
    public async Task ImportPlayersAsync_ShortNameRepeated_IsStillAPlainDuplicate()
    {
        using var file = new TempCsvFile($"{StorageHeader}\n");
        var (service, _) = CreateStack(file.Path_);

        // Numbering is only for collisions that shortening caused. A name the file genuinely
        // repeats must not quietly become "Alice2".
        var result = await service.ImportPlayersAsync($"{ExportHeader}\nAlice,2,2,2,Forward,\nAlice,3,3,3,Defender,\n");

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(1, result.DuplicateCount);
        Assert.Equal(0, result.NumberedCount);
    }

    [Fact]
    public async Task ImportPlayersAsync_MoreCollisionsThanDigits_SkipsTheOnesItCannotName()
    {
        using var file = new TempCsvFile($"{StorageHeader}\n");
        var (service, _) = CreateStack(file.Path_);

        // Digits 2-9 give eight variants beyond the first, so the tenth has nowhere to go.
        var prefix = new string('x', CsvSafeName.MaxLength);
        var rows = string.Concat(Enumerable.Range(0, 10).Select(i => $"{prefix}Suffix{i},2,2,2,Forward,\n"));

        var result = await service.ImportPlayersAsync($"{ExportHeader}\n{rows}");

        Assert.Equal(9, result.ImportedCount);
        Assert.Equal(8, result.NumberedCount);
        Assert.Equal(1, result.DuplicateCount);
    }

    [Fact]
    public async Task ImportPlayersAsync_SkillOutOfRange_CountsItSeparatelyFromTheName()
    {
        using var file = new TempCsvFile($"{StorageHeader}\n");
        var (service, _) = CreateStack(file.Path_);

        // 7 parses as a number, so the row is readable; it fails the 1-3 range instead.
        var result = await service.ImportPlayersAsync($"{ExportHeader}\nAlice,3,2,1,Defender,\nBob,7,2,2,Forward,\n");

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(1, result.InvalidSkillsCount);
        Assert.Equal(0, result.InvalidNameCount);
    }

    [Fact]
    public async Task ImportPlayersAsync_UnreadableRow_IsCountedRatherThanVanishing()
    {
        using var file = new TempCsvFile($"{StorageHeader}\n");
        var (service, _) = CreateStack(file.Path_);

        // "abc" is not a number, so this row never becomes a player at all.
        var result = await service.ImportPlayersAsync($"{ExportHeader}\nAlice,3,2,1,Defender,\nBob,abc,2,2,Forward,\n");

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(1, result.UnreadableCount);
        Assert.Equal(2, result.TotalRows);
    }

    [Fact]
    public async Task ImportPlayersAsync_SameFileTwice_ReportsEveryRowAsADuplicate()
    {
        using var file = new TempCsvFile($"{StorageHeader}\n");
        var (service, _) = CreateStack(file.Path_);

        var csv = $"{ExportHeader}\nAlice,3,2,1,Defender,\nBob,2,2,2,Forward,\n";
        await service.ImportPlayersAsync(csv);

        var second = await service.ImportPlayersAsync(csv);

        Assert.Equal(0, second.ImportedCount);
        Assert.Equal(2, second.DuplicateCount);
        Assert.True(second.IsEntirelyDuplicates);
    }

    [Fact]
    public async Task ImportPlayersAsync_PartlyDuplicate_IsNotReportedAsEntirelyDuplicates()
    {
        using var file = new TempCsvFile($"{StorageHeader}\n");
        var (service, _) = CreateStack(file.Path_);

        await service.ImportPlayersAsync($"{ExportHeader}\nAlice,3,2,1,Defender,\n");

        var second = await service.ImportPlayersAsync($"{ExportHeader}\nAlice,3,2,1,Defender,\nBob,2,2,2,Forward,\n");

        Assert.Equal(1, second.ImportedCount);
        Assert.Equal(1, second.DuplicateCount);
        Assert.False(second.IsEntirelyDuplicates);
    }

    [Fact]
    public async Task ImportPlayersAsync_EmptyFileBody_ReportsNoRowsAtAll()
    {
        using var file = new TempCsvFile($"{StorageHeader}\n");
        var (service, _) = CreateStack(file.Path_);

        var result = await service.ImportPlayersAsync($"{ExportHeader}\n");

        Assert.Equal(0, result.TotalRows);
        Assert.False(result.IsEntirelyDuplicates);
    }

    [Fact]
    public async Task ImportPlayersAsync_EveryReasonAtOnce_CountsEachOne()
    {
        using var file = new TempCsvFile($"{StorageHeader}\n");
        var (service, _) = CreateStack(file.Path_);

        await service.ImportPlayersAsync($"{ExportHeader}\nAlice,3,2,1,Defender,\n");

        var tooLong = new string('x', CsvSafeName.MaxLength + 1);
        var result = await service.ImportPlayersAsync(
            $"{ExportHeader}\n" +
            "Alice,3,2,1,Defender,\n" +     // duplicate
            "=Formula,2,2,2,Forward,\n" +   // invalid name, beyond rescue
            $"{tooLong},2,2,2,Forward,\n" + // too long: shortened and imported
            "Bob,9,2,2,Forward,\n" +        // skills out of range
            "Carol,nope,2,2,Forward,\n" +   // unreadable
            "Dave,2,2,2,Forward,\n");       // fine

        Assert.Equal(2, result.ImportedCount);
        Assert.Equal(1, result.TruncatedCount);
        Assert.Equal(1, result.DuplicateCount);
        Assert.Equal(1, result.InvalidNameCount);
        Assert.Equal(1, result.InvalidSkillsCount);
        Assert.Equal(1, result.UnreadableCount);
        Assert.Equal(4, result.SkippedCount);
        Assert.Equal(6, result.TotalRows);
    }

    [Fact]
    public async Task ImportPlayersAsync_TwentyCharacterName_IsAccepted()
    {
        using var file = new TempCsvFile($"{StorageHeader}\n");
        var (service, repository) = CreateStack(file.Path_);

        var name = new string('x', 20);
        var result = await service.ImportPlayersAsync($"{ExportHeader}\n{name},2,2,2,Forward,\n");

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Single(await repository.GetAllAsync(), p => p.Name == name);
    }
}
