namespace TeamBalancer.Core.Tests.Models;

using TeamBalancer.Core.Models;

/// <summary>
/// Covers the shared name rules. The length cap is asserted against its literal value as well
/// as against the constant, so raising the constant alone cannot silently move the limit that
/// player names and list names are actually held to.
/// </summary>
public class CsvSafeNameTests
{
    [Fact]
    public void MaxLength_IsTwenty()
    {
        Assert.Equal(20, CsvSafeName.MaxLength);
    }

    [Fact]
    public void IsValid_NameAtTheLimit_IsAccepted()
    {
        Assert.True(CsvSafeName.IsValid(new string('x', CsvSafeName.MaxLength)));
    }

    [Fact]
    public void IsValid_NameOneOverTheLimit_IsRejected()
    {
        Assert.False(CsvSafeName.IsValid(new string('x', CsvSafeName.MaxLength + 1)));
    }

    [Fact]
    public void IsValid_SixteenCharacterName_IsNowAccepted()
    {
        // Previously the cap was 15, so this is the case the change was made for.
        Assert.True(CsvSafeName.IsValid("Wladimir Melnyk!"[..16]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" leading")]
    [InlineData("trailing ")]
    [InlineData("with,comma")]
    [InlineData("with\"quote")]
    [InlineData("=formula")]
    [InlineData("+formula")]
    [InlineData("-formula")]
    [InlineData("@formula")]
    public void IsValid_UnsafeNames_AreStillRejected(string name)
    {
        Assert.False(CsvSafeName.IsValid(name));
    }

    [Fact]
    public void IsValid_Null_IsRejected()
    {
        Assert.False(CsvSafeName.IsValid(null));
    }

    [Fact]
    public void Truncate_NameWithinTheLimit_IsReturnedUnchanged()
    {
        Assert.Equal("Alice", CsvSafeName.Truncate("Alice"));
    }

    [Fact]
    public void Truncate_LongName_IsCutToTheLimit()
    {
        var cut = CsvSafeName.Truncate(new string('x', CsvSafeName.MaxLength + 10));

        Assert.Equal(CsvSafeName.MaxLength, cut.Length);
        Assert.True(CsvSafeName.IsValid(cut));
    }

    [Fact]
    public void Truncate_CutLandingOnASpace_LeavesNoTrailingWhitespace()
    {
        var cut = CsvSafeName.Truncate(new string('x', CsvSafeName.MaxLength - 1) + " more");

        Assert.Equal(new string('x', CsvSafeName.MaxLength - 1), cut);
        Assert.True(CsvSafeName.IsValid(cut));
    }

    [Fact]
    public void Truncate_NameInvalidForAnotherReason_StaysInvalid()
    {
        // Truncation addresses length only - the caller still has to validate.
        Assert.False(CsvSafeName.IsValid(CsvSafeName.Truncate("=" + new string('x', CsvSafeName.MaxLength + 5))));
    }
}
