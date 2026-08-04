namespace TeamBalancer.Localization;

using TeamBalancer.Core.Localization;

/// <summary>
/// Stores the chosen language in the platform's local preferences. It stays out of the
/// player CSV on purpose: which language this device shows is a setting of the install, not
/// part of the squad data the user imports and exports.
/// </summary>
public sealed class MauiLanguagePreference : ILanguagePreference
{
    /// <summary>
    /// The preferences key the chosen language code is stored under.
    /// </summary>
    private const string PreferenceKey = "AppLanguage";

    /// <inheritdoc />
    public string? Read()
    {
        var stored = Preferences.Default.Get(PreferenceKey, string.Empty);

        return string.IsNullOrWhiteSpace(stored) ? null : stored;
    }

    /// <inheritdoc />
    public void Write(string languageCode) => Preferences.Default.Set(PreferenceKey, languageCode);
}
