using TeamBalancer.Core.Models;

namespace TeamBalancer.Services;

/// <summary>
/// Carries the match being played between the screen that accepts a split and the screen that
/// records the game.
/// </summary>
/// <remarks>
/// Unlike <see cref="TeamStateService"/> this raises no change event. Only the Match screen
/// reads a match, and it is also the only thing that changes one - it re-renders itself off
/// its own event handlers, so an event here would have no second listener to tell.
///
/// It does outlive the Match screen, which is what lets the user step out to the Add Player
/// form and come back to a game still in progress.
/// </remarks>
public class MatchStateService
{
    /// <summary>
    /// Gets the match being played, or null when there is none.
    /// </summary>
    public MatchRecord? CurrentMatch { get; private set; }

    /// <summary>
    /// Gets or sets whether the Match screen should reopen its add-a-player sheet when it is
    /// next shown. Creating a player leaves the screen for the Add Player form and comes back
    /// to a freshly built component, so the intent to add someone has to be left somewhere
    /// that survives the trip.
    /// </summary>
    public bool ResumeAddingPlayer { get; set; }

    /// <summary>
    /// Gets or sets which side a player being created is destined for, held across the same
    /// trip and for the same reason.
    /// </summary>
    public int AddingToTeamIndex { get; set; }

    /// <summary>
    /// Starts a match, replacing any that was in progress.
    /// </summary>
    /// <param name="match">The match to play.</param>
    public void StartMatch(MatchRecord match)
    {
        CurrentMatch = match;
        ResumeAddingPlayer = false;
        AddingToTeamIndex = 0;
    }

    /// <summary>
    /// Ends the match, whether it was finished or discarded.
    /// </summary>
    public void ClearMatch()
    {
        CurrentMatch = null;
        ResumeAddingPlayer = false;
        AddingToTeamIndex = 0;
    }
}
