namespace TeamBalancer.Core.Localization;

/// <summary>
/// A language the app ships translations for.
/// </summary>
/// <param name="Code">
/// The two-letter ISO 639-1 code. It doubles as the name of the language's JSON file and as
/// the value stored in the user's preferences.
/// </param>
/// <param name="NativeName">
/// The language's name written in that language. The switcher lists languages this way so
/// that someone who has landed in a language they cannot read still recognises their own.
/// </param>
public sealed record SupportedLanguage(string Code, string NativeName)
{
    /// <summary>
    /// Gets the language every lookup falls back to, and the one the app starts in when
    /// neither the stored preference nor the device culture names a language we ship.
    /// </summary>
    public static SupportedLanguage Default { get; } = new("en", "English");

    /// <summary>
    /// Gets every language the app ships, in the order the switcher lists them.
    /// </summary>
    public static IReadOnlyList<SupportedLanguage> All { get; } =
    [
        Default,
        new("de", "Deutsch"),
        new("uk", "Українська")
    ];

    /// <summary>
    /// Finds a shipped language by its code.
    /// </summary>
    /// <param name="code">A two-letter language code, or null.</param>
    /// <returns>The matching language, or null when the code is not one we ship.</returns>
    public static SupportedLanguage? Find(string? code) =>
        string.IsNullOrWhiteSpace(code)
            ? null
            : All.FirstOrDefault(language =>
                string.Equals(language.Code, code, StringComparison.OrdinalIgnoreCase));
}
