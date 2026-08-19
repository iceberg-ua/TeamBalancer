namespace TeamBalancer.Core.Tests.Services.Sharing;

using System.Text;
using TeamBalancer.Core.Exceptions;
using TeamBalancer.Core.Models;
using TeamBalancer.Core.Services.Sharing;

/// <summary>
/// Covers packing a squad into the text a QR code carries. The size assertions are as much part
/// of the contract as the round trip: the point of compressing at all is that a hundred-player
/// squad has to fit in a code a phone can still read off another phone's screen.
/// </summary>
public class SquadPayloadCodecTests
{
    private const string ExportHeader = "Name,Speed,TechnicalSkills,Stamina,PrimaryPosition,SecondaryPosition";

    private readonly SquadPayloadCodec _codec = new();

    [Fact]
    public void RoundTrip_ReturnsTheSameListNameAndPlayers()
    {
        var payload = new SquadPayload("Sunday League", $"{ExportHeader}\nAlice,3,2,1,Defender,\nBob,2,2,2,Forward,Midfielder\n");

        var decoded = _codec.Decode(_codec.Encode(payload));

        Assert.Equal(payload.ListName, decoded.ListName);
        Assert.Equal(payload.PlayersCsv, decoded.PlayersCsv);
    }

    [Fact]
    public void RoundTrip_NonAsciiListName_Survives()
    {
        // List names are user data and the app ships in Ukrainian and German, so the envelope
        // has to carry more than ASCII through compression and base32 both.
        var payload = new SquadPayload("Неділя", $"{ExportHeader}\nЮрій,3,3,2,Midfielder,\n");

        var decoded = _codec.Decode(_codec.Encode(payload));

        Assert.Equal("Неділя", decoded.ListName);
        Assert.Contains("Юрій", decoded.PlayersCsv);
    }

    [Fact]
    public void Encode_StartsWithTheVersionMarker()
    {
        var encoded = _codec.Encode(new SquadPayload("Squad", $"{ExportHeader}\nAlice,1,1,1,Forward,\n"));

        Assert.StartsWith(SquadPayloadCodec.Marker, encoded, StringComparison.Ordinal);
    }

    [Fact]
    public void Encode_HundredPlayers_FitsInAScannableQrCode()
    {
        var encoded = _codec.Encode(new SquadPayload("Sunday League", BuildRoster(100)));

        // The screen refuses to render anything past 2,200 characters, on the grounds that a
        // denser symbol mostly fails to scan. A hundred players is the size the feature was
        // asked for, so it has to sit comfortably under that rather than just inside it.
        Assert.True(
            encoded.Length < 1800,
            $"A 100-player squad encoded to {encoded.Length} characters, which leaves no headroom under the 2,200 cap.");
    }

    [Fact]
    public void Encode_CompressesFarBelowThePlainCsv()
    {
        var csv = BuildRoster(100);

        var encoded = _codec.Encode(new SquadPayload("Sunday League", csv));

        // Position names repeat on every row, which is what deflate feeds on. Without that the
        // squad would not fit in a QR code at all, so a regression here is worth catching.
        Assert.True(
            encoded.Length < csv.Length / 2,
            $"Encoding {csv.Length} characters of CSV produced {encoded.Length} characters, which is barely a saving.");
    }

    [Fact]
    public void Decode_TextWithoutTheMarker_Throws()
    {
        Assert.Throws<SquadPayloadException>(() => _codec.Decode("https://example.com/bus-timetable"));
    }

    [Fact]
    public void Decode_MarkerWithRubbishAfterIt_Throws()
    {
        // Base32-legal characters that are not a deflate stream: the marker got the payload as
        // far as being decoded, and it fell over on decompression instead.
        Assert.Throws<SquadPayloadException>(() => _codec.Decode(SquadPayloadCodec.Marker + "AAAAAAAAAAAAAAAA"));
    }

    [Fact]
    public void Decode_PayloadWithACharacterOutsideBase32_Throws()
    {
        Assert.Throws<SquadPayloadException>(() => _codec.Decode(SquadPayloadCodec.Marker + "!!!!"));
    }

    [Fact]
    public void IsSquadCode_TellsSquadCodesFromEverythingElse()
    {
        Assert.True(_codec.IsSquadCode(SquadPayloadCodec.Marker + "ABCD"));
        Assert.True(_codec.IsSquadCode("  " + SquadPayloadCodec.Marker + "ABCD  "));
        Assert.False(_codec.IsSquadCode("WIFI:S:cafe;T:WPA;P:hunter2;;"));
        Assert.False(_codec.IsSquadCode(string.Empty));
        Assert.False(_codec.IsSquadCode(null));
    }

    [Fact]
    public void Decode_OverLongListName_IsShortenedRatherThanRejected()
    {
        // The name only ever prefills a box the user can edit, so an over-long one is not worth
        // failing an otherwise perfectly good squad over.
        var payload = new SquadPayload(new string('x', CsvSafeName.MaxLength + 20), $"{ExportHeader}\nAlice,1,1,1,Forward,\n");

        var decoded = _codec.Decode(_codec.Encode(payload));

        Assert.Equal(CsvSafeName.MaxLength, decoded.ListName.Length);
        Assert.Contains("Alice", decoded.PlayersCsv);
    }

    /// <summary>
    /// Builds an export-format CSV with realistic name lengths and a spread of positions, so the
    /// size assertions measure something close to a real squad rather than compressible filler.
    /// </summary>
    /// <param name="count">How many players the roster should hold.</param>
    /// <returns>The CSV content.</returns>
    private static string BuildRoster(int count)
    {
        string[] first = ["Volodymyr", "Oleksandr", "Sebastian", "Maximilian", "Andriy", "Christian", "Dmytro", "Friedrich"];
        string[] last = ["M.", "K.", "Sh.", "B.", "P.", "H."];
        string[] positions = ["Goalkeeper", "Defender", "Midfielder", "Forward"];

        var sb = new StringBuilder();
        sb.AppendLine(ExportHeader);

        for (var i = 0; i < count; i++)
        {
            var name = $"{first[i % first.Length]} {last[i % last.Length]}{i}";
            var primary = positions[i % positions.Length];
            var secondary = i % 3 == 0 ? string.Empty : positions[(i + 1) % positions.Length];

            sb.AppendLine($"{name},{i % 3 + 1},{(i + 1) % 3 + 1},{(i + 2) % 3 + 1},{primary},{secondary}");
        }

        return sb.ToString();
    }
}
