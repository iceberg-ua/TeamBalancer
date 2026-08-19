namespace TeamBalancer.Services;

/// <summary>
/// Reads a QR code with the device camera.
/// </summary>
public interface IQrScannerService
{
    /// <summary>
    /// Gets a value indicating whether this device can scan at all. Windows has no scanner, and
    /// a device can refuse camera access, in which case the screen leaves the action out rather
    /// than offering a button that cannot work.
    /// </summary>
    bool IsCameraScanningSupported { get; }

    /// <summary>
    /// Opens the camera and returns the first code it reads.
    /// </summary>
    /// <returns>The text the code carried, or null if the user backed out.</returns>
    Task<string?> ScanWithCameraAsync();
}
