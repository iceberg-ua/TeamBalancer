namespace TeamBalancer.Core.Localization;

/// <summary>
/// Looks up user-facing text in the language the app is currently running in.
/// </summary>
/// <remarks>
/// Lookups are synchronous because they happen during rendering; the translations for the
/// active language and for the fallback language are loaded up front by
/// <see cref="InitializeAsync"/> and kept in memory.
/// </remarks>
public interface ILocalizationService
{
    /// <summary>
    /// Gets the translation for a key.
    /// </summary>
    /// <param name="key">A dot-notation key, such as "playerList.title".</param>
    /// <returns>
    /// The translation in the active language; the English one when the active language does
    /// not define the key; or the key wrapped in double brackets when no language defines it,
    /// so a missing translation shows up during testing instead of rendering as blank.
    /// </returns>
    string this[string key] { get; }

    /// <summary>
    /// Gets the translation for a key and substitutes its <c>{0}</c>-style placeholders.
    /// </summary>
    /// <param name="key">A dot-notation key whose translation contains placeholders.</param>
    /// <param name="args">The values to substitute, in placeholder order.</param>
    /// <returns>The formatted translation, with the same fallback behaviour as <see cref="this[string]"/>.</returns>
    string this[string key, params object[] args] { get; }

    /// <summary>
    /// Gets every language the app can be switched to.
    /// </summary>
    IReadOnlyList<SupportedLanguage> SupportedLanguages { get; }

    /// <summary>
    /// Gets the code of the language currently in use.
    /// </summary>
    string CurrentLanguage { get; }

    /// <summary>
    /// Loads the fallback translations and resolves the language to start in: the stored
    /// preference, else the device culture when the app ships that language, else English.
    /// Must complete before the first lookup.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Switches to another language, remembers the choice for the next launch, and raises
    /// <see cref="LanguageChanged"/> when the language actually changed.
    /// </summary>
    /// <param name="code">The two-letter code of a language the app ships.</param>
    /// <exception cref="ArgumentException">The code is not one of <see cref="SupportedLanguages"/>.</exception>
    Task SetLanguageAsync(string code);

    /// <summary>
    /// Raised after the active language changes, so that components already on screen can
    /// re-render themselves in the new language.
    /// </summary>
    event Action? LanguageChanged;
}
