namespace TeamBalancer.Core.Tests.TestSupport;

using TeamBalancer.Core.Localization;

/// <summary>
/// Holds the stored language in memory, standing in for the platform's preferences.
/// </summary>
public sealed class FakeLanguagePreference : ILanguagePreference
{
    /// <param name="stored">The language code already on the device, if any.</param>
    public FakeLanguagePreference(string? stored = null)
    {
        Stored = stored;
    }

    /// <summary>
    /// Gets the code currently stored, as the next launch would read it.
    /// </summary>
    public string? Stored { get; private set; }

    public string? Read() => Stored;

    public void Write(string languageCode) => Stored = languageCode;
}
