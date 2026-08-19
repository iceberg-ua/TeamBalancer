namespace TeamBalancer.Core.Services.Sharing;

using System.Text;

/// <summary>
/// RFC 4648 base32, without padding. Chosen over the more familiar base64 for one reason: its
/// alphabet is uppercase letters and digits only, which is exactly the character set a QR code
/// can carry in alphanumeric mode at 5.5 bits per character. Base64 forces byte mode and 8 bits
/// per character, and the 33% it saves in text length is more than lost again in the code.
/// A 100-player squad comes out a whole three QR versions smaller this way.
/// </summary>
public static class Base32
{
    /// <summary>
    /// The RFC 4648 alphabet. Every character is inside the QR alphanumeric set.
    /// </summary>
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>
    /// Reverse lookup for decoding, indexed by character value. -1 marks a character that is
    /// not part of the alphabet.
    /// </summary>
    private static readonly sbyte[] Lookup = BuildLookup();

    /// <summary>
    /// Encodes bytes as unpadded base32.
    /// </summary>
    /// <param name="data">The bytes to encode.</param>
    /// <returns>The encoded text, in uppercase.</returns>
    public static string Encode(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0)
        {
            return string.Empty;
        }

        // Eight characters carry five bytes, rounded up for the tail.
        var sb = new StringBuilder((data.Length * 8 + 4) / 5);

        var buffer = 0;
        var bitsHeld = 0;

        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bitsHeld += 8;

            while (bitsHeld >= 5)
            {
                sb.Append(Alphabet[(buffer >> (bitsHeld - 5)) & 0x1F]);
                bitsHeld -= 5;
            }
        }

        // The last few bits are padded out to a whole character with zeros. Decoding drops
        // them again, which is what makes the padding characters RFC 4648 would add pointless
        // here - and they would only make the QR code bigger.
        if (bitsHeld > 0)
        {
            sb.Append(Alphabet[(buffer << (5 - bitsHeld)) & 0x1F]);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Decodes unpadded base32 text back to bytes.
    /// </summary>
    /// <param name="text">The encoded text. Case is ignored and '=' padding is tolerated.</param>
    /// <returns>The decoded bytes.</returns>
    /// <exception cref="FormatException">The text holds a character outside the alphabet.</exception>
    public static byte[] Decode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length == 0)
        {
            return [];
        }

        var bytes = new List<byte>(text.Length * 5 / 8);

        var buffer = 0;
        var bitsHeld = 0;

        foreach (var c in text)
        {
            // Padding is not written by Encode, but a payload that travelled through something
            // that added it back should still decode rather than be rejected on a technicality.
            if (c == '=')
            {
                continue;
            }

            var value = c < Lookup.Length ? Lookup[c] : (sbyte)-1;
            if (value < 0)
            {
                throw new FormatException($"'{c}' is not a base32 character.");
            }

            buffer = (buffer << 5) | value;
            bitsHeld += 5;

            if (bitsHeld >= 8)
            {
                bytes.Add((byte)((buffer >> (bitsHeld - 8)) & 0xFF));
                bitsHeld -= 8;
            }
        }

        // Fewer than eight bits are left over whenever the byte count was not a multiple of
        // five. They are the zero padding Encode appended, and carry no data.
        return [.. bytes];
    }

    /// <summary>
    /// Builds the decoding table, accepting both cases so a payload that was lowercased on its
    /// way through some other program still reads.
    /// </summary>
    /// <returns>The lookup table, indexed by character.</returns>
    private static sbyte[] BuildLookup()
    {
        var lookup = new sbyte[128];
        Array.Fill(lookup, (sbyte)-1);

        for (var i = 0; i < Alphabet.Length; i++)
        {
            lookup[Alphabet[i]] = (sbyte)i;
            lookup[char.ToLowerInvariant(Alphabet[i])] = (sbyte)i;
        }

        return lookup;
    }
}
