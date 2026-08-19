namespace TeamBalancer.Core.Tests.Services.Sharing;

using System.Text;
using TeamBalancer.Core.Services.Sharing;

/// <summary>
/// Covers the base32 codec that keeps a shared squad inside the QR alphanumeric character set.
/// Round-tripping is the whole contract: a payload that comes back one byte different is a
/// squad that fails to decompress, and the user only sees "the code is damaged".
/// </summary>
public class Base32Tests
{
    [Fact]
    public void Encode_Empty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, Base32.Encode([]));
    }

    [Fact]
    public void Decode_Empty_ReturnsEmpty()
    {
        Assert.Empty(Base32.Decode(string.Empty));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    public void RoundTrip_EveryTailLength_ReturnsTheSameBytes(int length)
    {
        // Five bytes fill exactly eight characters, so the interesting cases are the lengths
        // either side of that boundary - each leaves a different number of bits in the tail.
        var data = new byte[length];
        for (var i = 0; i < length; i++)
        {
            data[i] = (byte)(i * 37 + 11);
        }

        Assert.Equal(data, Base32.Decode(Base32.Encode(data)));
    }

    [Fact]
    public void RoundTrip_AllByteValues_ReturnsTheSameBytes()
    {
        var data = new byte[256];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)i;
        }

        Assert.Equal(data, Base32.Decode(Base32.Encode(data)));
    }

    [Fact]
    public void Encode_UsesOnlyCharactersAQrCanCarryInAlphanumericMode()
    {
        // This is the property the whole choice of base32 rests on. Any character outside this
        // set would push the QR encoder into byte mode, which costs 8 bits per character
        // instead of 5.5 and makes a full squad several versions denser.
        const string QrAlphanumeric = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ $%*+-./:";

        var data = new byte[512];
        Random.Shared.NextBytes(data);

        var encoded = Base32.Encode(data);

        Assert.All(encoded, c => Assert.Contains(c, QrAlphanumeric));
    }

    [Fact]
    public void Decode_IsCaseInsensitive()
    {
        var data = Encoding.UTF8.GetBytes("Sunday League");
        var encoded = Base32.Encode(data);

        Assert.Equal(data, Base32.Decode(encoded.ToLowerInvariant()));
    }

    [Fact]
    public void Decode_TolerateSPadding()
    {
        var data = Encoding.UTF8.GetBytes("abc");

        Assert.Equal(data, Base32.Decode(Base32.Encode(data) + "======"));
    }

    [Theory]
    [InlineData("ABC1")]
    [InlineData("AB!C")]
    [InlineData("ABC0")]
    public void Decode_CharacterOutsideTheAlphabet_Throws(string text)
    {
        // 0, 1 and 8 are deliberately absent from the RFC 4648 alphabet so they cannot be
        // confused with O, I and B when a code is read out loud or typed.
        Assert.Throws<FormatException>(() => Base32.Decode(text));
    }
}
