using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using TeamBalancer.Core.Models;
using TeamBalancer.Core.Services.Interfaces;

namespace TeamBalancer.Components.Pages;

/// <summary>
/// Code-behind for AddPlayer component.
/// Handles both adding new players and editing existing players.
/// </summary>
public partial class AddPlayer
{
    #region Injected Dependencies

    [Inject]
    private IPlayerRepository PlayerRepository { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    #endregion

    #region Parameters

    /// <summary>
    /// Optional player ID for edit mode. If null, component is in add mode.
    /// </summary>
    [Parameter]
    public Guid? PlayerId { get; set; }

    #endregion

    #region Private Fields

    private Player? _existingPlayer;
    private string _playerName = string.Empty;
    private int _speed = 1;
    private int _technicalSkills = 1;
    private int _stamina = 1;
    private Position _primaryPosition = Position.Unspecified;
    private Position? _secondaryPosition;
    private string _errorMessage = string.Empty;
    private string _nameErrorMessage = string.Empty;
    private bool _showNameError = false;
    private string _positionErrorMessage = string.Empty;
    private bool _showPositionError = false;
    private bool _isLoading = false;

    #endregion

    #region Properties

    /// <summary>
    /// Gets whether the component is in edit mode.
    /// </summary>
    private bool IsEditMode => PlayerId.HasValue;

    /// <summary>
    /// Gets the page title based on the mode.
    /// </summary>
    private string PageTitle => IsEditMode ? Loc["addPlayer.editTitle"] : Loc["addPlayer.addTitle"];

    /// <summary>
    /// Gets the save button text based on the mode.
    /// </summary>
    private string SaveButtonText => IsEditMode ? Loc["addPlayer.saveChanges"] : Loc["addPlayer.savePlayer"];

    /// <summary>
    /// Gets whether the form is valid and can be submitted.
    /// </summary>
    private bool IsFormValid
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_playerName))
                return false;

            // If there's a validation error showing, form is not valid
            if (_showNameError)
                return false;

            // A primary position is required; the secondary one is optional
            if (_primaryPosition == Position.Unspecified)
                return false;

            // Create a temporary player to validate the name
            var tempPlayer = new Player { Name = _playerName.Trim() };
            return tempPlayer.IsNameValid();
        }
    }

    #endregion

    #region Lifecycle Methods

    /// <summary>
    /// Initializes the component and loads player data if in edit mode.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        if (IsEditMode)
        {
            await LoadPlayer();
        }

        ValidatePosition();
    }

    /// <summary>
    /// Called when parameters change. Reloads player if ID changes.
    /// </summary>
    protected override async Task OnParametersSetAsync()
    {
        if (IsEditMode && (_existingPlayer == null || _existingPlayer.Id != PlayerId!.Value))
        {
            await LoadPlayer();
        }
    }

    #endregion
     
    #region Private Methods

    /// <summary>
    /// Loads the player data for edit mode.
    /// </summary>
    private async Task LoadPlayer()
    {
        if (!PlayerId.HasValue)
            return;

        _isLoading = true;
        _errorMessage = string.Empty;

        try
        {
            _existingPlayer = await PlayerRepository.GetByIdAsync(PlayerId.Value);

            if (_existingPlayer != null)
            {
                // Populate form fields with existing player data
                _playerName = _existingPlayer.Name;
                _speed = _existingPlayer.Speed;
                _technicalSkills = _existingPlayer.TechnicalSkills;
                _stamina = _existingPlayer.Stamina;
                _primaryPosition = _existingPlayer.PrimaryPosition;
                _secondaryPosition = _existingPlayer.SecondaryPosition;
                ValidatePosition();
            }
            else
            {
                _errorMessage = Loc["addPlayer.notFound"];
            }
        }
        catch (Exception ex)
        {
            _errorMessage = Loc["addPlayer.loadError", ex.Message];
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>
    /// Validates the player name after binding.
    /// </summary>
    private async void ValidateName()
    {
        // Validate the name
        if (string.IsNullOrWhiteSpace(_playerName))
        {
            _showNameError = false;
            _nameErrorMessage = string.Empty;
            return;
        }

        var tempPlayer = new Player { Name = _playerName.Trim() };
        _showNameError = !tempPlayer.IsNameValid();

        if (_showNameError)
        {
            // Provide specific error message based on validation failure
            if (_playerName != _playerName.Trim())
            {
                _nameErrorMessage = Loc["validation.nameLeadingTrailingSpaces"];
            }
            else if (_playerName.Contains(','))
            {
                _nameErrorMessage = Loc["validation.nameCommas"];
            }
            else if (_playerName.Contains('"'))
            {
                _nameErrorMessage = Loc["validation.nameQuotes"];
            }
            else if (_playerName.Length > 15)
            {
                _nameErrorMessage = Loc["validation.nameTooLong"];
            }
            else if (_playerName.Length > 0 &&
                     (_playerName[0] == '=' || _playerName[0] == '+' ||
                      _playerName[0] == '-' || _playerName[0] == '@'))
            {
                _nameErrorMessage = Loc["validation.nameLeadingSymbol"];
            }
            else
            {
                _nameErrorMessage = Loc["validation.nameInvalid"];
            }
        }
        else
        {
            // Check if name already exists (only for new players or if name changed in edit mode)
            var trimmedName = _playerName.Trim();
            if (!IsEditMode || (_existingPlayer != null && !_existingPlayer.Name.Equals(trimmedName, StringComparison.OrdinalIgnoreCase)))
            {
                var existingPlayer = await PlayerRepository.GetByNameAsync(trimmedName);
                if (existingPlayer != null)
                {
                    _showNameError = true;
                    _nameErrorMessage = Loc["validation.nameDuplicate"];
                }
                else
                {
                    _nameErrorMessage = string.Empty;
                }
            }
            else
            {
                _nameErrorMessage = string.Empty;
            }
        }
    }

    /// <summary>
    /// Handles a primary position change from the selector and revalidates.
    /// </summary>
    private void OnPrimaryPositionChanged(Position position)
    {
        _primaryPosition = position;
        ValidatePosition();
    }

    /// <summary>
    /// Validates the selected positions, explaining why the save button is disabled when
    /// no primary position has been chosen.
    /// </summary>
    private void ValidatePosition()
    {
        _showPositionError = _primaryPosition == Position.Unspecified;
        _positionErrorMessage = _showPositionError
            ? Loc["validation.primaryPositionRequired"]
            : string.Empty;
    }

    /// <summary>
    /// Saves the player (adds new or updates existing).
    /// </summary>
    private async Task SavePlayer()
    {
        try
        {
            _errorMessage = string.Empty;

            if (IsEditMode && _existingPlayer != null)
            {
                // Update existing player
                _existingPlayer.Name = _playerName.Trim();
                _existingPlayer.Speed = _speed;
                _existingPlayer.TechnicalSkills = _technicalSkills;
                _existingPlayer.Stamina = _stamina;
                _existingPlayer.PrimaryPosition = _primaryPosition;
                _existingPlayer.SecondaryPosition = _secondaryPosition;

                await PlayerRepository.UpdateAsync(_existingPlayer);
                await PlayerRepository.SaveChangesAsync();
            }
            else
            {
                // Add new player
                var player = new Player
                {
                    Name = _playerName.Trim(),
                    Speed = _speed,
                    TechnicalSkills = _technicalSkills,
                    Stamina = _stamina,
                    PrimaryPosition = _primaryPosition,
                    SecondaryPosition = _secondaryPosition
                };

                await PlayerRepository.AddAsync(player);
                await PlayerRepository.SaveChangesAsync();
            }

            // Navigate back to previous page
            await JSRuntime.InvokeVoidAsync("history.back");
        }
        catch (Exception ex)
        {
            _errorMessage = Loc["addPlayer.saveError", ex.Message];
        }
    }

    /// <summary>
    /// Cancels the operation and navigates back.
    /// </summary>
    private async Task Cancel()
    {
        await JSRuntime.InvokeVoidAsync("history.back");
    }

    #endregion
}
