namespace TeamBalancer.Services;

using ZXing.Net.Maui;

/// <summary>
/// Renders QR codes with ZXing.
/// </summary>
/// <remarks>
/// This class and <see cref="QrScannerService"/> are the only two places in the app that name a
/// ZXing type. Everything above them deals in strings and in
/// <see cref="Core.Models.SquadPayload"/>, so replacing the barcode library later - or fixing an
/// API that moved between versions - is a change to two files rather than to the screens.
/// </remarks>
public sealed class QrCodeService : IQrCodeService
{
    /// <summary>
    /// The quiet zone, in modules. The QR specification asks for four, and a code rendered
    /// tight to its edge is markedly harder for a camera to find - which matters more here than
    /// usual, because the codes a full squad produces are dense to begin with.
    /// </summary>
    private const int QuietZoneModules = 4;

    /// <inheritdoc />
    public async Task<string> CreateQrImageAsync(string content, int pixelSize = 1024)
    {
        ArgumentException.ThrowIfNullOrEmpty(content);

        var options = new BarcodeGeneratorOptions
        {
            Format = BarcodeFormat.QrCode,
            Width = pixelSize,
            Height = pixelSize,
            Margin = QuietZoneModules,

            // The payload is base32, so every character is inside the QR alphanumeric set and
            // the encoder picks that mode on its own - which is what keeps a 100-player squad
            // three versions smaller than the same data in base64. Pinning the character set
            // to Latin-1 stops the encoder adding a UTF-8 ECI header it does not need.
            CharacterSet = "ISO-8859-1"
        };

        using var stream = new MemoryStream();
        await BarcodeGenerator.WriteToStreamAsync(content, stream, options);

        return "data:image/png;base64," + Convert.ToBase64String(stream.ToArray());
    }
}
