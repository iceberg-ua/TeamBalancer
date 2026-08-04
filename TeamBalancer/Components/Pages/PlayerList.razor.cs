using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using TeamBalancer.Core.Models;
using TeamBalancer.Core.Services.Balancing;
using TeamBalancer.Core.Services.Interfaces;
using TeamBalancer.Components.Layout;
using TeamBalancer.Services;

namespace TeamBalancer.Components.Pages;

/// <summary>
/// Code-behind for PlayerList component.
/// Handles player selection, team creation, and player deletion.
/// </summary>
public partial class PlayerList
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
    private bool _showDeleteConfirm = false;
    private Player? _playerToDelete = null;

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
    /// Loads all players from the repository and restores selection state from Player.IsSelected.
    /// </summary>
    private async Task LoadPlayers()
    {
        _isLoading = true;
        var players = await PlayerRepository.GetAllAsync();
        _players = players.ToList();

        _selectedPlayerIds = [.. _players.Where(p => p.IsSelected).Select(p => p.Id)];

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

        // Update the model in memory (persisted to CSV on app close)
        var player = _players.FirstOrDefault(p => p.Id == selection.PlayerId);
        player?.IsSelected = selection.IsSelected;

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

        // Update all player models in memory
        foreach (var player in _players)
        {
            player.IsSelected = selectAll;
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
    /// Handles player deletion by showing confirmation dialog.
    /// </summary>
    /// <param name="player">The player to delete.</param>
    private void HandlePlayerDelete(Player player)
    {
        _playerToDelete = player;
        _showDeleteConfirm = true;
    }

    /// <summary>
    /// Confirms and executes the player deletion.
    /// </summary>
    private async Task ConfirmDeletePlayer()
    {
        if (_playerToDelete != null)
        {
            await PlayerRepository.DeleteAsync(_playerToDelete.Id);
            await PlayerRepository.SaveChangesAsync();
            _selectedPlayerIds.Remove(_playerToDelete.Id);
            await LoadPlayers();
        }

        _showDeleteConfirm = false;
        _playerToDelete = null;
    }

    /// <summary>
    /// Cancels the delete operation.
    /// </summary>
    private void CancelDelete()
    {
        _showDeleteConfirm = false;
        _playerToDelete = null;
    }

    #endregion
}
