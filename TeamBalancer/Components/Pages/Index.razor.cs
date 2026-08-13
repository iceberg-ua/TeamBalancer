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

    private List<Player> _players = new();
    private bool CanCreateTeams => _players.Count >= 2;
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

    /// <summary>
    /// Loads the active list's squad, which this screen both counts by position and lists in
    /// full. The players stay in the order the list stores them rather than being sorted here,
    /// so a player is where the user last saw them on the Select Players screen.
    /// </summary>
    private async Task LoadPlayers()
    {
        _players = (await PlayerRepository.GetAllAsync()).ToList();

        // Create Teams lives in the footer, which belongs to the layout, so whether it is
        // enabled cannot follow from re-rendering this page alone.
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

    /// <summary>
    /// Opens a player from the roster for editing. The Add Player screen doubles as the
    /// editor when it is handed an id, which is the same route the Select Players screen
    /// sends its edit action to.
    /// </summary>
    /// <param name="player">The player whose row was tapped.</param>
    private void EditPlayer(Player player)
    {
        Navigation.NavigateTo($"/add-player/{player.Id}");
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

            // An empty file makes the import service throw, which would surface its exception
            // text on screen. It is an ordinary thing to pick by mistake, so answer it here.
            if (string.IsNullOrWhiteSpace(csvContent))
            {
                _message = Loc["home.importEmpty"];
                _isError = true;
                _showMenu = false;
                return;
            }

            // Import players
            var result = await CsvImportExportService.ImportPlayersAsync(csvContent);

            _message = DescribeImport(result);
            _isError = result.ImportedCount == 0 && !result.IsEntirelyDuplicates;

            if (result.ImportedCount > 0)
            {
                await LoadPlayers();
            }

            _showMenu = false;
        }
        catch (Exception ex)
        {
            _message = Loc["home.importError", ex.Message];
            _isError = true;
        }
    }

    /// <summary>
    /// Turns an import outcome into the sentence shown on the screen: what came in, followed by
    /// what did not and why. The reasons are listed separately rather than rolled into a single
    /// skipped count, because "already in this list" and "name too long" call for very different
    /// things from the user.
    /// </summary>
    /// <param name="result">The outcome of the import.</param>
    /// <returns>The message to display.</returns>
    private string DescribeImport(PlayerImportResult result)
    {
        // Nothing in the file at all - no rows, or none the parser could even look at.
        if (result.TotalRows == 0)
        {
            return Loc["home.importEmpty"];
        }

        // Re-importing a file whose players are all present is a no-op, not a failure, and
        // used to be reported as "no valid players found".
        if (result.IsEntirelyDuplicates)
        {
            return Loc["home.importAllDuplicates", result.DuplicateCount];
        }

        var reasons = new List<string>();

        if (result.DuplicateCount > 0)
            reasons.Add(Loc["home.importSkippedDuplicate", result.DuplicateCount]);

        if (result.InvalidNameCount > 0)
            reasons.Add(Loc["home.importSkippedName", result.InvalidNameCount, CsvSafeName.MaxLength]);

        if (result.InvalidSkillsCount > 0)
            reasons.Add(Loc["home.importSkippedSkills", result.InvalidSkillsCount]);

        if (result.UnreadableCount > 0)
            reasons.Add(Loc["home.importSkippedUnreadable", result.UnreadableCount]);

        if (result.ErrorCount > 0)
            reasons.Add(Loc["home.importSkippedError", result.ErrorCount]);

        // Shortening a name is not a skip - the player is in the list - but it changed their
        // data, so it is always mentioned, including when nothing else went wrong.
        if (result.TruncatedCount > 0)
        {
            reasons.Add(Loc["home.importTruncated", result.TruncatedCount, CsvSafeName.MaxLength]);
        }

        // Appending a digit changes the name beyond merely shortening it, so it is called out
        // separately - these are the players whose names no longer read as the file wrote them.
        if (result.NumberedCount > 0)
        {
            reasons.Add(Loc["home.importNumbered", result.NumberedCount]);
        }

        if (reasons.Count == 0)
        {
            // Every row landed untouched, so the count on its own is the whole story.
            return Loc["home.importSuccess", result.ImportedCount];
        }

        var headline = result.SkippedCount == 0
            ? Loc["home.importSuccess", result.ImportedCount]
            : result.ImportedCount > 0
                ? Loc["home.importPartial", result.ImportedCount, result.TotalRows]
                : Loc["home.importNone", result.TotalRows];

        return $"{headline} {string.Join(" ", reasons)}";
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
