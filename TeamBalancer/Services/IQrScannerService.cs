namespace TeamBalancer.Services;

/// <summary>
/// Reads a QR code with the device, either from the camera or from an image already on the
/// phone.
/// </summary>
public interface IQrScannerService
{
    /// <summary>
    /// Gets a value indicating whether this device can scan with the camera at all. Windows has
    /// no scanner, and a device can refuse camera access, in which case the screen offers only
    /// the image route rather than a button that cannot work.
    /// </summary>
    bool IsCameraScanningSupported { get; }

    /// <summary>
    /// Opens the camera and returns the first code it reads.
    /// </summary>
    /// <returns>The text the code carried, or null if the user backed out.</returns>
    Task<string?> ScanWithCameraAsync();

    /// <summary>
    /// Asks the user for an image and reads a code out of it. This is the answer to a QR code
    /// that arrived through a messenger: it is a picture on the phone that is showing it, and
    /// a camera cannot photograph its own screen.
    /// </summary>
    /// <returns>
    /// The text the code carried, null if the user picked nothing, or an empty string if the
    /// image held no code at all - a different outcome, and one worth telling the user about.
    /// </returns>
    Task<string?> ScanFromImageAsync();
}
