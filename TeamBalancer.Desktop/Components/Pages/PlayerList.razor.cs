using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using TeamBalancer.Core.Models;
using TeamBalancer.Core.Services.Balancing;
using TeamBalancer.Core.Services.Interfaces;
using TeamBalancer.Desktop.Components.Layout;
using TeamBalancer.Desktop.Services;

namespace TeamBalancer.Desktop.Components.Pages;

/// <summary>
/// Code-behind for PlayerList component.
/// Handles player selection, team creation, and player deletion.
/// </summary>
public partial class PlayerList : ComponentBase
{
    #region Injected Dependencies

    [Inject]
    private IPlayerRepository PlayerRepository { get; set; } = default!;

    [Inject]
    private TeamBalancingService TeamBalancingService { get; set; } = default!;

    [Inject]
    private TeamStateService TeamState { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    [CascadingParameter]
    private MainLayout? Layout { get; set; }

    #endregion

    #region Private Fields

    private List<Player> _players = new();
    private HashSet<Guid> _selectedPlayerIds = new();
    private bool _isLoading = true;
    private BalancingAlgorithmType _selectedAlgorithm = BalancingAlgorithmType.SnakeDraft;

    #endregion

    #region Properties

    /// <summary>
    /// Gets whether the Create Teams button should be enabled.
    /// </summary>
    private bool NoPlayersSelected => _selectedPlayerIds.Count == 0;

    #endregion

    #region Lifecycle Methods

    /// <summary>
    /// Initializes the component and loads players.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        await LoadPlayers();
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Loads all players from the repository and pre-selects them.
    /// </summary>
    private async Task LoadPlayers()
    {
        _isLoading = true;
        var players = await PlayerRepository.GetAllAsync();
        _players = players.ToList();

        // Pre-select all players
        _selectedPlayerIds = _players.Select(p => p.Id).ToHashSet();

        _isLoading = false;
        StateHasChanged();
    }

    /// <summary>
    /// Handles individual player selection changes.
    /// </summary>
    /// <param name="selection">Tuple containing PlayerId and selection state.</param>
    private void HandlePlayerSelectionChanged((Guid PlayerId, bool IsSelected) selection)
    {
        if (selection.IsSelected)
        {
            _selectedPlayerIds.Add(selection.PlayerId);
        }
        else
        {
            _selectedPlayerIds.Remove(selection.PlayerId);
        }

        StateHasChanged();
        Layout?.Refresh();
    }

    /// <summary>
    /// Handles select all / deselect all action.
    /// </summary>
    /// <param name="selectAll">True to select all players, false to deselect all.</param>
    private void HandleSelectAllChanged(bool selectAll)
    {
        if (selectAll)
        {
            _selectedPlayerIds = [.. _players.Select(p => p.Id)];
        }
        else
        {
            _selectedPlayerIds.Clear();
        }
        StateHasChanged();
        Layout?.Refresh();
    }

    /// <summary>
    /// Creates balanced teams from selected players using the chosen algorithm.
    /// </summary>
    private void CreateTeams()
    {
        // Get selected players
        var selectedPlayers = _players
            .Where(p => _selectedPlayerIds.Contains(p.Id))
            .ToList();

        if (selectedPlayers.Count < 2)
        {
            return;
        }

        // Balance teams using selected algorithm with shuffle enabled for variety
        // Shuffle ensures each generation creates different teams while maintaining balance
        var teams = TeamBalancingService.BalanceTeams(
            selectedPlayers,
            numberOfTeams: 2,
            algorithmType: _selectedAlgorithm,
            shuffle: true);

        // Store teams in state service
        TeamState.SetTeams(teams);

        // Navigate to teams page
        Navigation.NavigateTo("/teams");
    }

    /// <summary>
    /// Navigates to the Add Player page.
    /// </summary>
    private void GoToAddPlayer()
    {
        Navigation.NavigateTo("/add-player");
    }

    /// <summary>
    /// Cancels team creation and navigates back to home.
    /// </summary>
    private void Cancel()
    {
        Navigation.NavigateTo("/");
    }

    /// <summary>
    /// Handles player edit action by navigating to the add-player page with the player ID.
    /// </summary>
    /// <param name="player">The player to edit.</param>
    private void HandlePlayerEdit(Player player)
    {
        Navigation.NavigateTo($"/add-player/{player.Id}");
    }

    /// <summary>
    /// Handles player deletion with confirmation dialog.
    /// </summary>
    /// <param name="player">The player to delete.</param>
    private async Task HandlePlayerDelete(Player player)
    {
        // Confirm deletion
        bool confirmed = await ConfirmDelete(player.Name);

        if (confirmed)
        {
            // Delete player
            await PlayerRepository.DeleteAsync(player.Id);
            await PlayerRepository.SaveChangesAsync();

            // Remove from selected if it was selected
            _selectedPlayerIds.Remove(player.Id);

            // Reload players
            await LoadPlayers();
        }
    }

    /// <summary>
    /// Shows a confirmation dialog using JavaScript interop.
    /// </summary>
    /// <param name="playerName">Name of the player being deleted.</param>
    /// <returns>True if user confirmed, false otherwise.</returns>
    private async Task<bool> ConfirmDelete(string playerName)
    {
        return await JSRuntime.InvokeAsync<bool>(
            "confirm",
            $"Are you sure you want to delete {playerName}? This action cannot be undone.");
    }

    #endregion
}
