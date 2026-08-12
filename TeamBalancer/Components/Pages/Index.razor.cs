using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using TeamBalancer.Components.Layout;
using TeamBalancer.Core.Models;
using TeamBalancer.Core.Services.Interfaces;
using TeamBalancer.Services;

namespace TeamBalancer.Components.Pages;

public partial class Index
{
    [Inject]
    private IPlayerRepository PlayerRepository { get; set; } = default!;

    [Inject]
    private IActivePlayerRepository ActivePlayerRepository { get; set; } = default!;

    [Inject]
    private IPlayerListRepository PlayerListRepository { get; set; } = default!;

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
    private bool _showLanguageMenu = false;
    private bool _showListMenu = false;
    private List<PlayerListInfo> _lists = new();
    private string _activeListName = string.Empty;

    protected override void OnInitialized()
    {
        base.OnInitialized();

        ActivePlayerRepository.ListChanged += HandleListChanged;
    }

    protected override async Task OnParametersSetAsync()
    {
        // Players first: reading them is what makes the repository resolve which list is
        // active, and the header cannot name the active list until it has. Both run on every
        // navigation back here, so a list renamed or deleted on the manage screen shows up.
        await LoadPlayers();
        await LoadLists();
    }

    public override void Dispose()
    {
        ActivePlayerRepository.ListChanged -= HandleListChanged;

        base.Dispose();
    }

    private async Task LoadPlayers()
    {
        var players = await PlayerRepository.GetAllAsync();
        _playerCount = players.Count();
        Layout?.Refresh();
    }

    /// <summary>
    /// Loads the lists behind the switcher, and the name of the active one for the header.
    /// </summary>
    private async Task LoadLists()
    {
        _lists = (await PlayerListRepository.GetAllAsync()).ToList();

        var active = _lists.FirstOrDefault(l => l.Id == ActivePlayerRepository.CurrentListId);
        _activeListName = active?.Name ?? string.Empty;

        // The switcher is part of the header, which belongs to the layout rather than to this
        // page, so re-rendering the page alone would leave the old name on screen.
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
        _message = string.Empty;

        try
        {
            var csvContent = await CsvImportExportService.ExportPlayersAsync();
            var fileName = $"players_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

            // Use native share dialog to save/share the file
            // Note: Share API returns immediately after showing dialog,
            // so we can't know if user completed the export
            await FileSaveService.SaveAndShareAsync(fileName, csvContent, "text/csv");
        }
        catch (Exception ex)
        {
            _message = Loc["home.exportError", ex.Message];
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
                _message = Loc["home.importSuccess", importedCount];
                _isError = false;
                await LoadPlayers();
            }
            else
            {
                _message = Loc["home.importEmpty"];
                _isError = true;
            }

            _showMenu = false;
        }
        catch (Exception ex)
        {
            _message = Loc["home.importError", ex.Message];
            _isError = true;
        }
    }

    private void ToggleMenu()
    {
        _showMenu = !_showMenu;
        _showLanguageMenu = false;
        _showListMenu = false;
    }

    private void ToggleLanguageMenu()
    {
        _showLanguageMenu = !_showLanguageMenu;
        _showMenu = false;
        _showListMenu = false;
    }

    private void ToggleListMenu()
    {
        _showListMenu = !_showListMenu;
        _showMenu = false;
        _showLanguageMenu = false;
    }

    /// <summary>
    /// Switches to another player list and closes the switcher. The repository raises
    /// ListChanged from here, which is what reloads this screen's player count.
    /// </summary>
    private async Task SelectList(Guid listId)
    {
        _showListMenu = false;

        await ActivePlayerRepository.SwitchListAsync(listId);
    }

    private void GoToPlayerLists()
    {
        _showListMenu = false;

        Navigation.NavigateTo("/player-lists");
    }

    /// <summary>
    /// Reloads the header and the player count after the active list changed - which can
    /// happen from this screen's own switcher, or from the manage screen deleting the list
    /// that was active.
    /// </summary>
    private void HandleListChanged() => InvokeAsync(async () =>
    {
        await LoadPlayers();
        await LoadLists();

        StateHasChanged();
    });

    /// <summary>
    /// Switches the app to another language and closes the switcher. The service raises its
    /// change event from here, which is what repaints the rest of the screen.
    /// </summary>
    private async Task SelectLanguage(string code)
    {
        _showLanguageMenu = false;

        await Loc.SetLanguageAsync(code);
    }

    /// <inheritdoc />
    protected override void OnLanguageChanged()
    {
        base.OnLanguageChanged();

        // The header and footer belong to the layout, not to this page, so re-rendering the
        // page alone would leave the title and buttons in the old language.
        Layout?.Refresh();
    }
}
