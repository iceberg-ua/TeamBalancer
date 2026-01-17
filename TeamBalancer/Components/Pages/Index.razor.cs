using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using TeamBalancer.Components.Layout;
using TeamBalancer.Core.Services.Interfaces;
using TeamBalancer.Services;

namespace TeamBalancer.Components.Pages;

public partial class Index
{
    [Inject]
    private IPlayerRepository PlayerRepository { get; set; } = default!;

    [Inject]
    private ICsvImportExportService CsvImportExportService { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private IFileSaveService FileSaveService { get; set; } = default!;

    [CascadingParameter]
    private MainLayout? Layout { get; set; }

    private int _playerCount = 0;
    private bool CanCreateTeams => _playerCount >= 2;
    private string _message = string.Empty;
    private bool _isError = false;
    private bool _showMenu = false;

    protected override async Task OnParametersSetAsync()
    {
        await LoadPlayers();
    }

    private async Task LoadPlayers()
    {
        var players = await PlayerRepository.GetAllAsync();
        _playerCount = players.Count();
        Layout?.Refresh();
    }

    private void AddPlayer()
    {
        Navigation.NavigateTo("/add-player");
    }

    private void CreateTeams()
    {
        Navigation.NavigateTo("/create-teams");
    }

    private async Task ExportPlayers()
    {
        _showMenu = false;

        try
        {
            _message = string.Empty;
            var csvContent = await CsvImportExportService.ExportPlayersAsync();
            var fileName = $"players_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

            // Use native share dialog to save/share the file
            await FileSaveService.SaveAndShareAsync(fileName, csvContent, "text/csv");

            _message = "Players exported successfully!";
            _isError = false;
        }
        catch (Exception ex)
        {
            _message = $"Error exporting players: {ex.Message}";
            _isError = true;
        }
    }

    private async Task ImportPlayers(InputFileChangeEventArgs e)
    {
        try
        {
            _message = string.Empty;
            var file = e.File;

            if (file == null)
                return;

            // Read file content
            using var stream = file.OpenReadStream(maxAllowedSize: 1024 * 1024); // 1MB max
            using var reader = new StreamReader(stream);
            var csvContent = await reader.ReadToEndAsync();

            // Import players
            var importedCount = await CsvImportExportService.ImportPlayersAsync(csvContent);

            if (importedCount > 0)
            {
                _message = $"Successfully imported {importedCount} player(s)!";
                _isError = false;
                await LoadPlayers();
            }
            else
            {
                _message = "No valid players found in the CSV file.";
                _isError = true;
            }

            _showMenu = false;
        }
        catch (Exception ex)
        {
            _message = $"Error importing players: {ex.Message}";
            _isError = true;
        }
    }

    private void ToggleMenu()
    {
        _showMenu = !_showMenu;
    }
}
