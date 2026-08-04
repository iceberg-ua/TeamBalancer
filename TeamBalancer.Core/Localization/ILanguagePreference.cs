namespace TeamBalancer.Core.Localization;

/// <summary>
/// Remembers the language the user picked, across restarts. Implemented by the host app,
/// which knows where local settings belong on its platform.
/// </summary>
public interface ILanguagePreference
{
    /// <summary>
    /// Reads the stored language code.
    /// </summary>
    /// <returns>The code the user last chose, or null when they have never chosen one.</returns>
    string? Read();

    /// <summary>
    /// Stores the language code the user chose.
    /// </summary>
    /// <param name="languageCode">A two-letter language code.</param>
    void Write(string languageCode);
}
