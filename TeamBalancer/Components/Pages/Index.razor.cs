using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using TeamBalancer.Components.Layout;
using TeamBalancer.Core.Exceptions;
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

    [Inject]
    private ICsvParser CsvParser { get; set; } = default!;

    [Inject]
    private ISquadPayloadCodec SquadCodec { get; set; } = default!;

    [Inject]
    private IQrCodeService QrCodeService { get; set; } = default!;

    [Inject]
    private IQrScannerService QrScanner { get; set; } = default!;

    [CascadingParameter]
    private MainLayout? Layout { get; set; }

    /// <summary>
    /// The longest payload the app will render as a QR code. Past this the symbol needs so many
    /// modules that a phone camera struggles to read it off another phone's screen, and showing
    /// a code that mostly fails is worse than saying plainly that the squad is too big for one.
    /// In practice this is around 180 players; a hundred come to roughly 1,500 characters.
    /// </summary>
    private const int MaxQrPayloadLength = 2200;

    private List<Player> _players = new();
    private bool CanCreateTeams => _players.Count >= 2;
    private string _message = string.Empty;
    private bool _isError = false;
    private bool _showMenu = false;
    private bool _showLanguageMenu = false;
    private bool _showListMenu = false;
    private List<PlayerListInfo> _lists = new();
    private string _activeListName = string.Empty;

    private bool _showQrOverlay = false;
    private string? _qrImage;

    private bool _showImportDialog = false;
    private string _pendingCsv = string.Empty;
    private int _pendingPlayerCount;
    private bool _importCreatesNewList = true;
    private string _importListName = string.Empty;
    private bool _showImportNameError = false;
    private Guid _importTargetListId;

    /// <summary>
    /// Gets a value indicating whether the import can go ahead: a new list needs a name that
    /// will survive being written to CSV, an existing one needs to have been picked.
    /// </summary>
    private bool CanConfirmImport => _importCreatesNewList
        ? CsvSafeName.IsValid(_importListName)
        : _importTargetListId != Guid.Empty;

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

            // A file carries no list name of its own, so the naming dialog opens with the file's
            // own name behind it - closer to something the user recognises than a blank box.
            _showMenu = false;
            BeginImport(csvContent, SuggestNameFromFile(file.Name));
        }
        catch (Exception ex)
        {
            _message = Loc["home.importError", ex.Message];
            _isError = true;
        }
    }

    /// <summary>
    /// Renders the active list as a QR code for someone standing next to you to scan. The code
    /// carries the same CSV the export produces, compressed, so a squad shared this way and a
    /// squad shared as a file arrive as exactly the same thing.
    /// </summary>
    private async Task ShowSquadQr()
    {
        _showMenu = false;
        _message = string.Empty;
        _qrImage = null;

        if (_players.Count == 0)
        {
            _message = Loc["share.nothingToShare"];
            _isError = true;
            return;
        }

        try
        {
            var csv = await CsvImportExportService.ExportPlayersAsync();
            var payload = SquadCodec.Encode(new SquadPayload(_activeListName, csv));

            // Left null when the squad is too big, which the dialog reports rather than showing
            // a code dense enough that scanning it would mostly fail.
            _qrImage = payload.Length <= MaxQrPayloadLength
                ? await QrCodeService.CreateQrImageAsync(payload)
                : null;

            _showQrOverlay = true;
        }
        catch (Exception ex)
        {
            _message = Loc["share.qrError", ex.Message];
            _isError = true;
        }
    }

    private void CloseQr()
    {
        _showQrOverlay = false;
        _qrImage = null;
    }

    /// <summary>
    /// Opens the camera and imports whatever squad it finds.
    /// </summary>
    private async Task ScanSquadQr()
    {
        _showMenu = false;
        _message = string.Empty;

        try
        {
            HandleScannedCode(await QrScanner.ScanWithCameraAsync());
        }
        catch (Exception ex)
        {
            _message = Loc["share.scanError", ex.Message];
            _isError = true;
        }
    }

    /// <summary>
    /// Decides what a scan produced and either opens the import dialog or explains why it
    /// cannot. The failures are told apart deliberately: backing out of the camera is not an
    /// error at all, a code belonging to something else is a different mistake from a squad
    /// code that arrived damaged, and each wants a different sentence.
    /// </summary>
    /// <param name="scanned">The text read, or null if the user backed out.</param>
    private void HandleScannedCode(string? scanned)
    {
        if (string.IsNullOrEmpty(scanned))
        {
            return;
        }

        if (!SquadCodec.IsSquadCode(scanned))
        {
            _message = Loc["share.notASquadCode"];
            _isError = true;
            return;
        }

        try
        {
            var payload = SquadCodec.Decode(scanned);
            BeginImport(payload.PlayersCsv, payload.ListName);
        }
        catch (SquadPayloadException)
        {
            _message = Loc["share.codeUnreadable"];
            _isError = true;
        }
    }

    /// <summary>
    /// Opens the dialog that asks where an incoming squad should go. Every import goes through
    /// here, whichever way the players arrived, so a file and a scanned code ask the same
    /// question and land in the same place.
    /// </summary>
    /// <param name="csvContent">The players that arrived.</param>
    /// <param name="suggestedName">The name to offer for a new list.</param>
    private void BeginImport(string csvContent, string suggestedName)
    {
        // Counted with the same parser the import itself uses, so the number in the question is
        // the number of players that will actually arrive rather than a count of lines.
        _pendingPlayerCount = CsvParser.ParsePlayersWithDiagnostics(csvContent).Players.Count;

        if (_pendingPlayerCount == 0)
        {
            _message = Loc["home.importEmpty"];
            _isError = true;
            return;
        }

        _pendingCsv = csvContent;
        _importCreatesNewList = true;
        _importListName = CsvSafeName.IsValid(suggestedName) ? suggestedName : string.Empty;
        _showImportNameError = false;
        _importTargetListId = ActivePlayerRepository.CurrentListId;
        _showImportDialog = true;
    }

    /// <summary>
    /// Switches the import between creating a list and adding to one that exists.
    /// </summary>
    /// <param name="createsNewList">True to create a list, false to add to an existing one.</param>
    private void SetImportTarget(bool createsNewList)
    {
        _importCreatesNewList = createsNewList;
        _showImportNameError = false;
    }

    /// <summary>
    /// Shows the name rule once the field has something in it, so the message appears when the
    /// name is wrong rather than the moment the dialog opens on an empty box.
    /// </summary>
    private void ValidateImportName() =>
        _showImportNameError = !string.IsNullOrEmpty(_importListName) && !CsvSafeName.IsValid(_importListName);

    private void CancelImport()
    {
        _showImportDialog = false;
        _pendingCsv = string.Empty;
        _pendingPlayerCount = 0;
    }

    /// <summary>
    /// Carries out the import against whichever list the user chose. A new list is created and
    /// switched to first; an existing one only needs switching to, because the import service
    /// writes to whichever list is active.
    /// </summary>
    private async Task ConfirmImport()
    {
        _showImportDialog = false;
        _message = string.Empty;

        var csvContent = _pendingCsv;
        _pendingCsv = string.Empty;

        if (string.IsNullOrWhiteSpace(csvContent))
        {
            return;
        }

        try
        {
            ImportMode mode;

            if (_importCreatesNewList)
            {
                var created = await PlayerListRepository.AddAsync(_importListName.Trim());
                await ActivePlayerRepository.SwitchListAsync(created.Id);

                // A list created a moment ago holds nobody, so there is nothing for a merge to
                // reconcile and the cheaper path says exactly what happened.
                mode = ImportMode.AddOnly;
            }
            else
            {
                if (_importTargetListId != ActivePlayerRepository.CurrentListId)
                {
                    await ActivePlayerRepository.SwitchListAsync(_importTargetListId);
                }

                mode = ImportMode.Merge;
            }

            var result = await CsvImportExportService.ImportPlayersAsync(csvContent, mode);

            _message = DescribeImport(result);

            // Nothing arriving is only a failure when nothing was already right. A squad that
            // was entirely up to date, or entirely already present, changed nothing and is not
            // a problem the user needs to act on.
            _isError = result.ImportedCount == 0
                && result.UpdatedCount == 0
                && !result.IsEntirelyDuplicates
                && !result.IsEntirelyUnchanged;

            await LoadPlayers();
            await LoadLists();
        }
        catch (Exception ex)
        {
            _message = Loc["home.importError", ex.Message];
            _isError = true;
        }
    }

    /// <summary>
    /// Turns a file name into a starting point for a list name - the name without its
    /// extension, shortened to fit, and dropped entirely if what is left would be rejected.
    /// </summary>
    /// <param name="fileName">The name of the file the user picked.</param>
    /// <returns>A name to offer, or an empty string when the file name cannot supply one.</returns>
    private static string SuggestNameFromFile(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName).Trim();

        if (stem.Length > CsvSafeName.MaxLength)
        {
            stem = CsvSafeName.Truncate(stem);
        }

        return CsvSafeName.IsValid(stem) ? stem : string.Empty;
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

        // The merge counterpart: every player was already there and already agreed. Receiving
        // a squad that has not changed since last time is the most ordinary outcome there is.
        if (result.IsEntirelyUnchanged)
        {
            return Loc["home.importAllUnchanged", result.UnchangedCount];
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

        var headline = Headline(result);

        if (reasons.Count == 0)
        {
            // Every row landed untouched, so the headline on its own is the whole story.
            return headline;
        }

        return $"{headline} {string.Join(" ", reasons)}";
    }

    /// <summary>
    /// The opening sentence of an import message. A merge can add players, update players, or
    /// both, and each of the three reads differently enough to deserve its own sentence -
    /// "imported 0 players" would be an actively misleading way to report a merge that
    /// refreshed a dozen ratings.
    /// </summary>
    /// <param name="result">The outcome of the import.</param>
    /// <returns>The sentence to open with.</returns>
    private string Headline(PlayerImportResult result)
    {
        if (result.ImportedCount > 0 && result.UpdatedCount > 0)
        {
            return Loc["home.importAddedAndUpdated", result.ImportedCount, result.UpdatedCount];
        }

        if (result.UpdatedCount > 0)
        {
            return Loc["home.importUpdatedOnly", result.UpdatedCount];
        }

        if (result.SkippedCount == 0)
        {
            return Loc["home.importSuccess", result.ImportedCount];
        }

        return result.ImportedCount > 0
            ? Loc["home.importPartial", result.ImportedCount, result.TotalRows]
            : Loc["home.importNone", result.TotalRows];
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
    /// Opens the games this squad has already played.
    /// </summary>
    private void GoToHistory()
    {
        _showMenu = false;

        Navigation.NavigateTo("/history");
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
