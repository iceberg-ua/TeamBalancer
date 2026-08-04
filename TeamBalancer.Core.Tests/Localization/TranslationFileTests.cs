namespace TeamBalancer.Core.Tests.Localization;

using System.Text.Json;
using TeamBalancer.Core.Localization;

/// <summary>
/// Guards the translation files the app ships. These are hand-edited, and the failure they
/// exist to catch is a string added to one language and forgotten in the others - which the
/// app itself hides, because a missing key silently falls back to English.
/// </summary>
public class TranslationFileTests
{
    /// <summary>
    /// The shipped files are copied next to the test assembly by the project file, so the
    /// tests read the same JSON the app does rather than a copy that can drift.
    /// </summary>
    private static readonly string LanguagesDirectory =
        Path.Combine(AppContext.BaseDirectory, "Languages");

    public static TheoryData<string> ShippedLanguageCodes()
    {
        var data = new TheoryData<string>();
        foreach (var language in SupportedLanguage.All)
        {
            data.Add(language.Code);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ShippedLanguageCodes))]
    public void EveryShippedLanguage_HasAFile(string code)
    {
        Assert.True(
            File.Exists(FilePath(code)),
            $"{code}.json is missing: SupportedLanguage.All offers '{code}' in the switcher, so the app must ship its translations.");
    }

    [Theory]
    [MemberData(nameof(ShippedLanguageCodes))]
    public void EveryShippedLanguage_HasTheSameKeysAsEnglish(string code)
    {
        var english = ReadKeys(SupportedLanguage.Default.Code);
        var translated = ReadKeys(code);

        var missing = english.Except(translated).Order().ToList();
        var extra = translated.Except(english).Order().ToList();

        Assert.True(
            missing.Count == 0,
            $"{code}.json is missing keys that en.json defines: {string.Join(", ", missing)}");
        Assert.True(
            extra.Count == 0,
            $"{code}.json defines keys that en.json does not: {string.Join(", ", extra)}");
    }

    [Theory]
    [MemberData(nameof(ShippedLanguageCodes))]
    public void EveryShippedLanguage_HasNoBlankTranslations(string code)
    {
        var blank = ReadCatalog(code)
            .Where(entry => string.IsNullOrWhiteSpace(entry.Value))
            .Select(entry => entry.Key)
            .Order()
            .ToList();

        Assert.True(
            blank.Count == 0,
            $"{code}.json leaves keys untranslated: {string.Join(", ", blank)}");
    }

    [Theory]
    [MemberData(nameof(ShippedLanguageCodes))]
    public void EveryShippedLanguage_UsesTheSamePlaceholdersAsEnglish(string code)
    {
        // A translation that drops or invents a placeholder either loses a value the sentence
        // needs or throws when it is formatted.
        var english = ReadCatalog(SupportedLanguage.Default.Code);
        var translated = ReadCatalog(code);

        var mismatched = english
            .Where(entry => translated.TryGetValue(entry.Key, out var text)
                && PlaceholderCount(text) != PlaceholderCount(entry.Value))
            .Select(entry => entry.Key)
            .Order()
            .ToList();

        Assert.True(
            mismatched.Count == 0,
            $"{code}.json uses different placeholders than en.json for: {string.Join(", ", mismatched)}");
    }

    /// <summary>
    /// Counts the distinct <c>{n}</c> placeholders in a translation.
    /// </summary>
    private static int PlaceholderCount(string text)
    {
        var count = 0;
        while (text.Contains($"{{{count}}}", StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    private static string FilePath(string code) => Path.Combine(LanguagesDirectory, $"{code}.json");

    private static Dictionary<string, string> ReadCatalog(string code)
    {
        var json = File.ReadAllText(FilePath(code));

        return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
            ?? throw new InvalidOperationException($"{code}.json did not parse into a key/value map.");
    }

    private static IEnumerable<string> ReadKeys(string code) => ReadCatalog(code).Keys;
}
