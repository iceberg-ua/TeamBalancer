using Microsoft.AspNetCore.Components;
using TeamBalancer.Core.Models;
using TeamBalancer.Core.Services.Interfaces;

namespace TeamBalancer.Components.Pages;

/// <summary>
/// Code-behind for the PlayerLists component.
/// Handles creating, renaming, deleting and switching between player lists.
/// </summary>
public partial class PlayerLists
{
    #region Injected Dependencies

    [Inject]
    private IPlayerListRepository PlayerListRepository { get; set; } = default!;

    [Inject]
    private IActivePlayerRepository ActivePlayerRepository { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    #endregion

    #region Private Fields

    private List<PlayerListInfo> _lists = new();
    private bool _isLoading = true;
    private string _message = string.Empty;

    private bool _showNameDialog = false;
    private string _listName = string.Empty;
    private bool _showNameError = false;

    /// <summary>
    /// The list the name dialog is renaming, or null when it is creating a new one.
    /// </summary>
    private PlayerListInfo? _listBeingRenamed = null;

    private bool _showDeleteConfirm = false;
    private PlayerListInfo? _listToDelete = null;

    #endregion

    #region Properties

    /// <summary>
    /// Gets whether lists can be deleted at all. The last one has to stay, so its delete
    /// action is hidden rather than left to fail.
    /// </summary>
    private bool CanDelete => _lists.Count > 1;

    /// <summary>
    /// Gets whether the name currently typed can be saved.
    /// </summary>
    private bool IsNameValid => CsvSafeName.IsValid(_listName.Trim());

    #endregion

    #region Lifecycle Methods

    /// <summary>
    /// Initializes the component and loads the lists.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        ActivePlayerRepository.ListChanged += HandleListChanged;

        await LoadLists();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        ActivePlayerRepository.ListChanged -= HandleListChanged;

        base.Dispose();
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Loads all lists from the repository.
    /// </summary>
    private async Task LoadLists()
    {
        _isLoading = true;
        _lists = (await PlayerListRepository.GetAllAsync()).ToList();
        _isLoading = false;

        StateHasChanged();
    }

    /// <summary>
    /// Repaints the ticks after the active list changed - which this screen does itself when
    /// switching, and indirectly when deleting the list that was active.
    /// </summary>
    private void HandleListChanged() => InvokeAsync(StateHasChanged);

    /// <summary>
    /// Makes a list the active one.
    /// </summary>
    /// <param name="listId">The list to switch to.</param>
    private async Task SwitchTo(Guid listId)
    {
        _message = string.Empty;

        try
        {
            await ActivePlayerRepository.SwitchListAsync(listId);
        }
        catch (Exception ex)
        {
            _message = Loc["playerLists.error", ex.Message];
        }
    }

    /// <summary>
    /// Opens the name dialog to create a new list.
    /// </summary>
    private void StartCreate()
    {
        _listBeingRenamed = null;
        _listName = string.Empty;
        _showNameError = false;
        _showNameDialog = true;
    }

    /// <summary>
    /// Opens the name dialog to rename an existing list.
    /// </summary>
    /// <param name="list">The list to rename.</param>
    private void StartRename(PlayerListInfo list)
    {
        _listBeingRenamed = list;
        _listName = list.Name;
        _showNameError = false;
        _showNameDialog = true;
    }

    /// <summary>
    /// Validates the typed name after binding.
    /// </summary>
    private void ValidateName()
    {
        // An empty box is the dialog's starting state, not something to complain about; the
        // save button stays disabled until a valid name is typed either way.
        _showNameError = !string.IsNullOrWhiteSpace(_listName) && !IsNameValid;
    }

    /// <summary>
    /// Creates or renames the list the dialog was opened for.
    /// </summary>
    private async Task SaveName()
    {
        if (!IsNameValid)
        {
            return;
        }

        _message = string.Empty;

        try
        {
            if (_listBeingRenamed is null)
            {
                await PlayerListRepository.AddAsync(_listName.Trim());
            }
            else
            {
                await PlayerListRepository.RenameAsync(_listBeingRenamed.Id, _listName.Trim());
            }

            CancelNameDialog();
            await LoadLists();
        }
        catch (Exception ex)
        {
            _message = Loc["playerLists.error", ex.Message];
            CancelNameDialog();
        }
    }

    /// <summary>
    /// Closes the name dialog without saving.
    /// </summary>
    private void CancelNameDialog()
    {
        _showNameDialog = false;
        _listBeingRenamed = null;
        _listName = string.Empty;
        _showNameError = false;
    }

    /// <summary>
    /// Asks for confirmation before deleting a list.
    /// </summary>
    /// <param name="list">The list to delete.</param>
    private void StartDelete(PlayerListInfo list)
    {
        _listToDelete = list;
        _showDeleteConfirm = true;
    }

    /// <summary>
    /// Confirms and executes the list deletion. Deleting the active list switches to another
    /// one first, which the repository takes care of.
    /// </summary>
    private async Task ConfirmDelete()
    {
        if (_listToDelete != null)
        {
            _message = string.Empty;

            try
            {
                await ActivePlayerRepository.DeleteListAsync(_listToDelete.Id);
            }
            catch (InvalidOperationException ex)
            {
                // The delete action is hidden for the last list, so this only fires if the
                // lists changed underneath the screen.
                _message = _lists.Count <= 1
                    ? Loc["playerLists.cannotDeleteLast"]
                    : Loc["playerLists.error", ex.Message];
            }

            await LoadLists();
        }

        _showDeleteConfirm = false;
        _listToDelete = null;
    }

    /// <summary>
    /// Cancels the delete operation.
    /// </summary>
    private void CancelDelete()
    {
        _showDeleteConfirm = false;
        _listToDelete = null;
    }

    /// <summary>
    /// Navigates back to the home screen.
    /// </summary>
    private void GoHome()
    {
        Navigation.NavigateTo("/");
    }

    #endregion
}
