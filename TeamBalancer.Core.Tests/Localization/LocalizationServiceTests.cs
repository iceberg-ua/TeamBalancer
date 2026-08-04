namespace TeamBalancer.Core.Tests.Localization;

using System.Globalization;
using TeamBalancer.Core.Localization;
using TeamBalancer.Core.Tests.TestSupport;

/// <summary>
/// Covers <see cref="LocalizationService"/>: how it picks the language to start in, what it
/// does with keys a language leaves untranslated, and what switching languages persists.
/// </summary>
public class LocalizationServiceTests
{
    private const string EnglishJson = """
        {
          "common.cancel": "Cancel",
          "common.delete": "Delete",
          "playerList.deleteConfirm": "Are you sure you want to delete {0}?",
          "playerList.headerPlayer": "Player ({0}/{1})"
        }
        """;

    private const string GermanJson = """
        {
          "common.cancel": "Abbrechen",
          "playerList.deleteConfirm": "Möchtest du {0} wirklich löschen?",
          "playerList.headerPlayer": "Spieler ({0}/{1})"
        }
        """;

    [Fact]
    public async Task Indexer_KeyMissingFromActiveLanguage_FallsBackToEnglish()
    {
        var service = await CreateServiceAsync(stored: "de");

        // "common.delete" is only in the English file.
        Assert.Equal("Delete", service["common.delete"]);
        Assert.Equal("Abbrechen", service["common.cancel"]);
    }

    [Fact]
    public async Task Indexer_KeyMissingEverywhere_ReturnsBracketedKey()
    {
        var service = await CreateServiceAsync(stored: "de");

        Assert.Equal("[[teams.nothingHere]]", service["teams.nothingHere"]);
    }

    [Fact]
    public async Task Indexer_KeyBlankInActiveLanguage_FallsBackToEnglish()
    {
        // A key left as an empty string is a translation nobody has written yet, not a
        // deliberately blank label.
        var service = await CreateServiceAsync(
            stored: "de",
            german: """{ "common.cancel": "" }""");

        Assert.Equal("Cancel", service["common.cancel"]);
    }

    [Fact]
    public async Task Indexer_WithArguments_SubstitutesPlaceholders()
    {
        var service = await CreateServiceAsync(stored: "de");

        Assert.Equal("Möchtest du Ivan wirklich löschen?", service["playerList.deleteConfirm", "Ivan"]);
        Assert.Equal("Spieler (3/8)", service["playerList.headerPlayer", 3, 8]);
    }

    [Fact]
    public async Task Indexer_WithArgumentsOnFallbackText_SubstitutesPlaceholders()
    {
        var service = await CreateServiceAsync(
            stored: "de",
            german: """{ "common.cancel": "Abbrechen" }""");

        Assert.Equal("Are you sure you want to delete Ivan?", service["playerList.deleteConfirm", "Ivan"]);
    }

    [Fact]
    public async Task Indexer_WithArgumentsOnMissingKey_ReturnsBracketedKey()
    {
        var service = await CreateServiceAsync(stored: "de");

        Assert.Equal("[[teams.nothingHere]]", service["teams.nothingHere", "Ivan"]);
    }

    [Fact]
    public async Task Indexer_TranslationWithMalformedPlaceholder_ReturnsTemplateUnsubstituted()
    {
        // A bad string in a translation file must not take down the screen rendering it.
        var service = await CreateServiceAsync(
            stored: "de",
            german: """{ "common.cancel": "Abbrechen {oops}" }""");

        Assert.Equal("Abbrechen {oops}", service["common.cancel", "Ivan"]);
    }

    [Fact]
    public async Task InitializeAsync_NoStoredPreference_UsesDeviceLanguage()
    {
        var service = await CreateServiceAsync(stored: null, deviceCulture: "de-AT");

        Assert.Equal("de", service.CurrentLanguage);
        Assert.Equal("Abbrechen", service["common.cancel"]);
    }

    [Fact]
    public async Task InitializeAsync_DeviceLanguageNotShipped_FallsBackToEnglish()
    {
        var service = await CreateServiceAsync(stored: null, deviceCulture: "fr-FR");

        Assert.Equal("en", service.CurrentLanguage);
        Assert.Equal("Cancel", service["common.cancel"]);
    }

    [Fact]
    public async Task InitializeAsync_StoredPreference_WinsOverDeviceLanguage()
    {
        var service = await CreateServiceAsync(stored: "de", deviceCulture: "en-GB");

        Assert.Equal("de", service.CurrentLanguage);
    }

    [Fact]
    public async Task InitializeAsync_StoredPreferenceNotShipped_FallsBackToDeviceLanguage()
    {
        var service = await CreateServiceAsync(stored: "fr", deviceCulture: "de-DE");

        Assert.Equal("de", service.CurrentLanguage);
    }

    [Fact]
    public async Task InitializeAsync_LanguageFileMissing_FallsBackToEnglishText()
    {
        // Ukrainian is a supported language with no file in this fixture.
        var service = await CreateServiceAsync(stored: "uk");

        Assert.Equal("uk", service.CurrentLanguage);
        Assert.Equal("Cancel", service["common.cancel"]);
    }

    [Fact]
    public async Task SetLanguageAsync_PersistsSelectionAndRaisesLanguageChanged()
    {
        var preference = new FakeLanguagePreference();
        var service = await CreateServiceAsync(preference, deviceCulture: "en-GB");
        var changedCount = 0;
        service.LanguageChanged += () => changedCount++;

        await service.SetLanguageAsync("de");

        Assert.Equal("de", service.CurrentLanguage);
        Assert.Equal("de", preference.Stored);
        Assert.Equal(1, changedCount);
        Assert.Equal("Abbrechen", service["common.cancel"]);
    }

    [Fact]
    public async Task SetLanguageAsync_SameLanguage_StillPersistsButDoesNotRaise()
    {
        // Choosing the language the app defaulted to is how a user pins it: the choice has to
        // be stored, or the next launch would resolve the device culture all over again.
        var preference = new FakeLanguagePreference();
        var service = await CreateServiceAsync(preference, deviceCulture: "en-GB");
        var changedCount = 0;
        service.LanguageChanged += () => changedCount++;

        await service.SetLanguageAsync("en");

        Assert.Equal("en", preference.Stored);
        Assert.Equal(0, changedCount);
    }

    [Fact]
    public async Task SetLanguageAsync_UnsupportedLanguage_Throws()
    {
        var service = await CreateServiceAsync(stored: null);

        await Assert.ThrowsAsync<ArgumentException>(() => service.SetLanguageAsync("fr"));
    }

    [Fact]
    public async Task SetLanguageAsync_SwitchingBack_ReadsEachFileOnlyOnce()
    {
        var source = new FakeTranslationSource(new Dictionary<string, string>
        {
            ["en"] = EnglishJson,
            ["de"] = GermanJson
        });
        var service = new LocalizationService(source, new FakeLanguagePreference(), () => new CultureInfo("en-GB"));
        await service.InitializeAsync();

        await service.SetLanguageAsync("de");
        await service.SetLanguageAsync("en");
        await service.SetLanguageAsync("de");

        // English on startup, then German once; every later switch is served from memory.
        Assert.Equal(2, source.OpenCount);
    }

    [Fact]
    public async Task SupportedLanguages_ListsEveryShippedLanguageByNativeName()
    {
        var service = await CreateServiceAsync(stored: null);

        Assert.Equal(
            new[] { "English", "Deutsch", "Українська" },
            service.SupportedLanguages.Select(language => language.NativeName));
    }

    private static Task<LocalizationService> CreateServiceAsync(
        string? stored,
        string deviceCulture = "en-GB",
        string german = GermanJson) =>
        CreateServiceAsync(new FakeLanguagePreference(stored), deviceCulture, german);

    private static async Task<LocalizationService> CreateServiceAsync(
        FakeLanguagePreference preference,
        string deviceCulture = "en-GB",
        string german = GermanJson)
    {
        var source = new FakeTranslationSource(new Dictionary<string, string>
        {
            ["en"] = EnglishJson,
            ["de"] = german
        });

        var service = new LocalizationService(source, preference, () => new CultureInfo(deviceCulture));
        await service.InitializeAsync();

        return service;
    }
}
