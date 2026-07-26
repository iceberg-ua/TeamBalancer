namespace TeamBalancer.Core.Tests.Services.Csv;

using Microsoft.Extensions.Logging.Abstractions;
using TeamBalancer.Core.Models;
using TeamBalancer.Core.Services.Csv;
using TeamBalancer.Core.Tests.TestSupport;

/// <summary>
/// Covers position persistence and validation in <see cref="CsvPlayerRepository"/> (Phase 1).
/// </summary>
public class CsvPlayerRepositoryPositionTests
{
    private const string StorageHeader = "Name,Speed,TechnicalSkills,Stamina,PrimaryPosition,SecondaryPosition,IsSelected";

    private static CsvPlayerRepository CreateRepository(string filePath)
    {
        return new CsvPlayerRepository(new CsvParser(NullLogger<CsvParser>.Instance), filePath);
    }

    [Fact]
    public async Task UpdateAsync_ChangingPositions_PersistsToDiskAndSurvivesReload()
    {
        using var file = new TempCsvFile($"{StorageHeader}\nMover,2,2,2,Defender,,True\n");
        var repository = CreateRepository(file.Path_);

        var player = (await repository.GetAllAsync()).Single();
        player.PrimaryPosition = Position.Forward;
        player.SecondaryPosition = Position.Midfielder;

        await repository.UpdateAsync(player);
        await repository.SaveChangesAsync();

        // Re-read through a fresh repository to prove it reached the file.
        var reloaded = (await CreateRepository(file.Path_).GetAllAsync()).Single();

        Assert.Equal(Position.Forward, reloaded.PrimaryPosition);
        Assert.Equal(Position.Midfielder, reloaded.SecondaryPosition);
    }

    [Fact]
    public async Task UpdateAsync_ClearingSecondaryPosition_PersistsAsNull()
    {
        using var file = new TempCsvFile($"{StorageHeader}\nClearer,2,2,2,Defender,Forward,True\n");
        var repository = CreateRepository(file.Path_);

        var player = (await repository.GetAllAsync()).Single();
        player.SecondaryPosition = null;

        await repository.UpdateAsync(player);
        await repository.SaveChangesAsync();

        var reloaded = (await CreateRepository(file.Path_).GetAllAsync()).Single();

        Assert.Equal(Position.Defender, reloaded.PrimaryPosition);
        Assert.Null(reloaded.SecondaryPosition);
    }

    [Fact]
    public async Task AddAsync_WithValidPositions_StoresThem()
    {
        using var file = new TempCsvFile($"{StorageHeader}\n");
        var repository = CreateRepository(file.Path_);

        await repository.AddAsync(TestPlayers.Create("Newbie", Position.Midfielder, Position.Forward));
        await repository.SaveChangesAsync();

        var reloaded = (await CreateRepository(file.Path_).GetAllAsync()).Single();

        Assert.Equal(Position.Midfielder, reloaded.PrimaryPosition);
        Assert.Equal(Position.Forward, reloaded.SecondaryPosition);
    }

    [Fact]
    public async Task AddAsync_SecondaryEqualsPrimary_Throws()
    {
        using var file = new TempCsvFile($"{StorageHeader}\n");
        var repository = CreateRepository(file.Path_);

        var player = TestPlayers.Create("Contradict", Position.Defender, Position.Defender);

        await Assert.ThrowsAsync<ArgumentException>(() => repository.AddAsync(player));
    }

    [Fact]
    public async Task UpdateAsync_SecondaryEqualsPrimary_Throws()
    {
        using var file = new TempCsvFile($"{StorageHeader}\nEditMe,2,2,2,Defender,,True\n");
        var repository = CreateRepository(file.Path_);

        var player = (await repository.GetAllAsync()).Single();
        player.SecondaryPosition = Position.Defender;

        await Assert.ThrowsAsync<ArgumentException>(() => repository.UpdateAsync(player));
    }

    /// <summary>
    /// DIVERGENCE FROM THE PHASE 4 SPEC. The spec asks for AddAsync to throw when
    /// PrimaryPosition is Unspecified. The Phase 1 implementation deliberately tolerates it
    /// (see the comment in CsvPlayerRepository.AddAsync) so that players imported from
    /// pre-position CSVs can still be added; only a secondary that contradicts the primary is
    /// rejected. This test documents the behaviour that actually shipped — it is not a fix.
    /// Requiring a position at this layer would break the old-CSV import path covered by
    /// CsvBackwardCompatibilityTests.
    /// </summary>
    [Fact]
    public async Task AddAsync_UnspecifiedPrimaryPosition_IsToleratedByDesign()
    {
        using var file = new TempCsvFile($"{StorageHeader}\n");
        var repository = CreateRepository(file.Path_);

        var player = TestPlayers.Create("Legacy", Position.Unspecified);

        var added = await repository.AddAsync(player);

        Assert.Equal(Position.Unspecified, added.PrimaryPosition);
        Assert.Null(added.SecondaryPosition);
    }
}
