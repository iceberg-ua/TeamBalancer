namespace TeamBalancer.Core.Services.Sharing;

using System.IO.Compression;
using System.Text;
using TeamBalancer.Core.Exceptions;
using TeamBalancer.Core.Models;
using TeamBalancer.Core.Services.Interfaces;

/// <summary>
/// Packs a squad into QR text and reads it back.
/// </summary>
/// <remarks>
/// The pipeline is: list name and CSV joined into one document, UTF-8, raw deflate, base32,
/// and a short marker in front. Every step earns its place in how big the resulting code is.
/// Deflate does the heavy lifting - a CSV repeats "Midfielder" and "Defender" endlessly, so a
/// 100-player squad falls from about 3.6 KB to under 1 KB. Base32 then keeps the result inside
/// the QR alphanumeric character set, which stores 5.5 bits per character instead of the 8 that
/// base64 would force. The marker is checked before anything else runs, so scanning some
/// unrelated code costs nothing and reports the right thing.
/// </remarks>
public sealed class SquadPayloadCodec : ISquadPayloadCodec
{
    /// <summary>
    /// The marker every squad code starts with. The digit is the format version: a future
    /// change to what the envelope holds becomes TB2, and this version will then say it cannot
    /// read the code rather than misreading it. Both characters and the colon are inside the
    /// QR alphanumeric set, so the marker does not cost the code its efficient encoding.
    /// </summary>
    public const string Marker = "TB1:";

    /// <summary>
    /// The largest document the codec will inflate. Deflate can turn a few hundred bytes into
    /// hundreds of megabytes, and a QR code is something a stranger can point at the app, so
    /// the ceiling is enforced rather than trusted. It is far above any real squad: 512 KB is
    /// on the order of fifteen thousand players.
    /// </summary>
    private const int MaxInflatedBytes = 512 * 1024;

    /// <inheritdoc />
    public string Encode(SquadPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        // The list name occupies the first line and the CSV follows. A name can never hold a
        // newline - CsvSafeName forbids it, the same rule that keeps names inside one CSV
        // cell - so the split on the way back is unambiguous.
        var document = payload.ListName + "\n" + payload.PlayersCsv;
        var bytes = Encoding.UTF8.GetBytes(document);

        using var compressed = new MemoryStream();
        using (var deflate = new DeflateStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            deflate.Write(bytes, 0, bytes.Length);
        }

        return Marker + Base32.Encode(compressed.ToArray());
    }

    /// <inheritdoc />
    public SquadPayload Decode(string qrText)
    {
        if (!IsSquadCode(qrText))
        {
            throw new SquadPayloadException("This code does not carry a Team Balancer squad.");
        }

        var encoded = qrText!.Trim()[Marker.Length..];

        byte[] compressed;
        try
        {
            compressed = Base32.Decode(encoded);
        }
        catch (FormatException ex)
        {
            throw new SquadPayloadException("The code is damaged and could not be read.", ex);
        }

        var document = Inflate(compressed);

        // A squad always has at least the name line and the CSV header line. Anything shorter
        // is a payload that decompressed but is not what this format describes.
        var split = document.IndexOf('\n');
        if (split < 0)
        {
            throw new SquadPayloadException("The code is damaged and could not be read.");
        }

        var listName = document[..split].Trim();
        var playersCsv = document[(split + 1)..];

        // The name only ever becomes a suggestion in the naming dialog, and the user is free to
        // replace it, so an over-long one is shortened rather than treated as a broken payload.
        if (listName.Length > CsvSafeName.MaxLength)
        {
            listName = CsvSafeName.Truncate(listName);
        }

        return new SquadPayload(listName, playersCsv);
    }

    /// <inheritdoc />
    public bool IsSquadCode(string? qrText) =>
        qrText is not null && qrText.Trim().StartsWith(Marker, StringComparison.Ordinal);

    /// <summary>
    /// Decompresses the payload, refusing anything that inflates past the ceiling.
    /// </summary>
    /// <param name="compressed">The deflated bytes.</param>
    /// <returns>The document the payload carried.</returns>
    /// <exception cref="SquadPayloadException">
    /// The bytes are not valid deflate output, or they inflate to more than the app will hold.
    /// </exception>
    private static string Inflate(byte[] compressed)
    {
        try
        {
            using var source = new MemoryStream(compressed);
            using var deflate = new DeflateStream(source, CompressionMode.Decompress);
            using var inflated = new MemoryStream();

            // Copied a block at a time and measured as it goes, so a payload built to expand
            // without limit is stopped part way rather than after it has been held in full.
            var buffer = new byte[8192];
            int read;
            while ((read = deflate.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (inflated.Length + read > MaxInflatedBytes)
                {
                    throw new SquadPayloadException("The code carries more data than the app will read.");
                }

                inflated.Write(buffer, 0, read);
            }

            return Encoding.UTF8.GetString(inflated.ToArray());
        }
        catch (InvalidDataException ex)
        {
            throw new SquadPayloadException("The code is damaged and could not be read.", ex);
        }
    }
}
