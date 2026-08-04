using Microsoft.Extensions.Logging;
using TeamBalancer.Core.Localization;
using TeamBalancer.Core.Services.Interfaces;
using TeamBalancer.Core.Services.Csv;
using TeamBalancer.Core.Services.Balancing;
using TeamBalancer.Localization;
using TeamBalancer.Services;

namespace TeamBalancer;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
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

		// Register player repository with CSV file path
		var dataFilePath = Path.Combine(FileSystem.AppDataDirectory, "players.csv");
		services.AddSingleton<IPlayerRepository>(sp =>
		{
			var csvParser = sp.GetRequiredService<ICsvParser>();
			return new CsvPlayerRepository(csvParser, dataFilePath);
		});

		// Register CSV import/export service
		services.AddSingleton<ICsvImportExportService, CsvImportExportService>();

		// Register team balancing services
		services.AddSingleton<ITeamBalancingStrategy, DraftStrategy>();
		services.AddSingleton<TeamBalancingService>();

		// Register UI services
		services.AddSingleton<TeamStateService>();

		// Register file save service
		services.AddSingleton<IFileSaveService, FileSaveService>();
	}
}
