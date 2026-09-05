using Microsoft.Extensions.Logging;
using TeamBalancer.Core.Localization;
using TeamBalancer.Core.Services.Interfaces;
using TeamBalancer.Core.Services.Csv;
using TeamBalancer.Core.Services.Balancing;
using TeamBalancer.Core.Services.Sharing;
using TeamBalancer.Localization;
using TeamBalancer.Services;
using ZXing.Net.Maui.Controls;

namespace TeamBalancer;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseBarcodeReader()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		// Register data services
		RegisterDataServices(builder.Services);

		var app = builder.Build();

		// Load the translations before the first window - and so the first render - exists,
		// otherwise the UI briefly paints in whatever language happened to be loaded.
		// Blocking is safe here: the load reads a packaged asset and never marshals back to
		// this thread, and it is the last thing that happens before the app starts up.
		app.Services.GetRequiredService<ILocalizationService>()
			.InitializeAsync()
			.GetAwaiter()
			.GetResult();

		return app;
	}

	private static void RegisterDataServices(IServiceCollection services)
	{
		// Register localization: one shared dictionary of translations for the whole app,
		// reading its files from the app package and its stored language from Preferences.
		services.AddSingleton<ITranslationSource, MauiTranslationSource>();
		services.AddSingleton<ILanguagePreference, MauiLanguagePreference>();
		services.AddSingleton<ILocalizationService>(sp => new LocalizationService(
			sp.GetRequiredService<ITranslationSource>(),
			sp.GetRequiredService<ILanguagePreference>()));

		// Register CSV parser
		services.AddSingleton<ICsvParser, CsvParser>();

		// Register the player list store and the active-list preference. The list repository
		// also performs the one-time upgrade from single-list storage the first time it reads.
		var dataDirectory = FileSystem.AppDataDirectory;
		services.AddSingleton<IPlayerListRepository>(sp =>
		{
			var csvParser = sp.GetRequiredService<ICsvParser>();
			return new CsvPlayerListRepository(csvParser, dataDirectory);
		});
		services.AddSingleton<ICurrentListPreference, MauiCurrentListPreference>();

		// Register the player repository. It is one object registered under two interfaces:
		// the list switcher asks for IActivePlayerRepository, every screen that only deals in
		// players keeps asking for IPlayerRepository and gets the active list's players.
		services.AddSingleton<IActivePlayerRepository>(sp => new ActivePlayerRepository(
			sp.GetRequiredService<ICsvParser>(),
			sp.GetRequiredService<IPlayerListRepository>(),
			sp.GetRequiredService<ICurrentListPreference>(),
			dataDirectory));
		services.AddSingleton<IPlayerRepository>(sp => sp.GetRequiredService<IActivePlayerRepository>());

		// Register the store for finished matches. It writes matches.csv into the same data
		// directory as the player files, and is only ever appended to.
		services.AddSingleton<IMatchRepository>(_ => new CsvMatchRepository(dataDirectory));

		// Register CSV import/export service
		services.AddSingleton<ICsvImportExportService, CsvImportExportService>();

		// Register team balancing services
		services.AddSingleton<ITeamBalancingStrategy, DraftStrategy>();
		services.AddSingleton<TeamBalancingService>();

		// Register UI services. The team state carries a split from Select Players to Teams;
		// the match state carries an accepted split on to the Match screen, and holds the game
		// while the user steps out to add a player who turned up late.
		services.AddSingleton<TeamStateService>();
		services.AddSingleton<MatchStateService>();

		// Register file save service
		services.AddSingleton<IFileSaveService, FileSaveService>();

		// Register squad sharing. The codec is platform-agnostic and lives in Core with the
		// CSV it wraps; the two QR services are the only place a barcode library is named.
		services.AddSingleton<ISquadPayloadCodec, SquadPayloadCodec>();
		services.AddSingleton<IQrCodeService, QrCodeService>();
		services.AddSingleton<IQrScannerService, QrScannerService>();
	}
}
