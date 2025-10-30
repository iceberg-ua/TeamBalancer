using Microsoft.Extensions.Logging;
using TeamBalancer.Core.Services.Interfaces;
using TeamBalancer.Core.Services.Csv;
using TeamBalancer.Core.Services.Balancing;
using TeamBalancer.Desktop.Services;

namespace TeamBalancer.Desktop;
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

        return builder.Build();
    }

    private static void RegisterDataServices(IServiceCollection services)
    {
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
        services.AddSingleton<ITeamBalancingStrategy, SnakeDraftStrategy>();
        services.AddSingleton<TeamBalancingService>();

        // Register UI services
        services.AddSingleton<TeamStateService>();
    }
}
