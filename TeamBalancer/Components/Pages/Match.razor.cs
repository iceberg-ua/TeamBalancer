using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using TeamBalancer.Components.Layout;
using TeamBalancer.Core.Models;
using TeamBalancer.Core.Services.Interfaces;
using TeamBalancer.Services;

namespace TeamBalancer.Components.Pages;

/// <summary>
/// Code-behind for the Match screen: the game itself, recorded while it is being played.
/// Score, goalscorers, assists, players who turned up late, and the finish that writes the
/// result down.
/// </summary>
public partial class Match
{
    /// <summary>
    /// Where the back arrow goes once the match has been discarded: the split it was accepted
    /// from, which is still in memory and can be accepted again or drawn afresh.
    /// </summary>
    private const string TeamsRoute = "/teams";

    private const string HomeRoute = "/";

    private const string SelectPlayersRoute = "/create-teams";

    private const string AddPlayerRoute = "/add-player";

    #region Injected Dependencies

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private MatchStateService MatchState { get; set; } = default!;

    [Inject]
    private TeamStateService TeamState { get; set; } = default!;

    [Inject]
    private IMatchRepository MatchRepository { get; set; } = default!;

    [Inject]
    private IPlayerRepository PlayerRepository { get; set; } = default!;

    [Inject]
    private IActivePlayerRepository ActivePlayerRepository { get; set; } = default!;

    [Inject]
    private IPlayerListRepository PlayerListRepository { get; set; } = default!;

    [CascadingParameter]
    private MainLayout? Layout { get; set; }

    #endregion

    #region State

    /// <summary>
    /// Gets the match being played, or null when the screen was reached without one.
    /// </summary>
    private MatchRecord? CurrentMatch { get; set; }

    /// <summary>
    /// Every player on the list this squad was drawn from, for the mid-match add. Read once
    /// when the screen is built, which also picks up anyone created since it was last shown.
    /// </summary>
    private List<Player> _listPlayers = [];

    private string _activeListName = string.Empty;

    private int _activeTabIndex;

    /// <summary>
    /// The side a player being added is joining. Held apart from the side being shown so the
    /// choice made in the sheet is not undone by tapping through the lineups behind it.
    /// </summary>
    private int _addToTeamIndex;

    private string _saveError = string.Empty;

    /// <summary>
    /// Set while the finished match is being written, so the button that started the write is
    /// held down until it is over. Nothing else on the screen waits on a file.
    /// </summary>
    private bool _isSaving;

    /// <summary>
    /// Why the last score typed in was refused, or empty. Cleared by anything that moves a
    /// score, since the complaint is about a figure that is no longer on the screen.
    /// </summary>
    private string _scoreRefusal = string.Empty;

    /// <summary>
    /// Bumped every time a score is typed in, one counter per side. It is part of that side's
    /// score box key, so committing an entry rebuilds the box from the match - which is what
    /// makes a refused entry snap back instead of being left on screen as a score the match
    /// does not agree with.
    /// </summary>
    /// <remarks>
    /// Per side rather than one for the match: a single counter is in both boxes' keys, so
    /// typing into one would throw away and rebuild the other - discarding a half-finished
    /// entry, a selection or an open keyboard on a box whose value never changed.
    /// </remarks>
    private readonly List<int> _scoreRevisions = [];

    /// <summary>
    /// The question waiting on an answer, if any. Leaving and finishing are asked with the
    /// same block of markup; adding a player has a sheet of its own but takes its turn in the
    /// same field, so only one thing can ever be open.
    /// </summary>
    private enum PendingAction
    {
        None,
        Leave,
        Finish,
        AddPlayer
    }

    private PendingAction _pending = PendingAction.None;

    #endregion

    #region Lifecycle

    protected override void OnInitialized()
    {
        base.OnInitialized();

        CurrentMatch = MatchState.CurrentMatch;

        // Coming back from creating a player. The screen was rebuilt from scratch while the
        // form was open, so the sheet is reopened here rather than having survived - on the
        // side that was chosen before leaving, with the new player now among those offered.
        if (MatchState.ResumeAddingPlayer)
        {
            _addToTeamIndex = MatchState.AddingToTeamIndex;
            _activeTabIndex = _addToTeamIndex;
            _pending = PendingAction.AddPlayer;

            MatchState.ResumeAddingPlayer = false;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        _listPlayers = [.. await PlayerRepository.GetAllAsync()];

        var lists = await PlayerListRepository.GetAllAsync();

        _activeListName = lists
            .FirstOrDefault(l => l.Id == ActivePlayerRepository.CurrentListId)?.Name
            ?? string.Empty;

        // The subline and both footer buttons belong to the layout rather than to this page.
        Layout?.Refresh();
    }

    /// <summary>
    /// Repaints the header and footer along with the page, since the title, the subline and
    /// both footer buttons are translated and none of them are this component's own markup.
    /// </summary>
    protected override void OnLanguageChanged()
    {
        base.OnLanguageChanged();

        Layout?.Refresh();
    }

    #endregion

    #region Reading the match

    /// <summary>
    /// Gets the index of the side being shown, clamped in case the match changed underneath
    /// the stored index.
    /// </summary>
    private int ActiveIndex => CurrentMatch is { Teams.Count: > 0 }
        ? Math.Clamp(_activeTabIndex, 0, CurrentMatch.Teams.Count - 1)
        : 0;

    /// <summary>
    /// Gets whether a player can be sent across. As on the Teams screen, moving assumes there
    /// is exactly one other side to move to.
    /// </summary>
    private bool CanMovePlayers => CurrentMatch is { Teams.Count: 2 };

    /// <summary>
    /// Gets the label on the button that sends a player over: the team they are not on.
    /// </summary>
    private string MoveTitle => Loc["teams.moveTo", TeamName(1 - ActiveIndex)];

    /// <summary>
    /// Gets the line under the title: the list the sides were drawn from, and how many players
    /// are on the pitch - which, unlike on the Teams screen, can grow during the game.
    /// </summary>
    private string HeaderSubline
    {
        get
        {
            var count = Loc["playerList.playerCount", CurrentMatch?.PlayerCount ?? 0];

            return string.IsNullOrEmpty(_activeListName)
                ? count
                : $"{_activeListName} · {count}";
        }
    }

    /// <summary>
    /// Gets the result as one line, for the last look before it is written down.
    /// </summary>
    private string Scoreline
    {
        get
        {
            if (CurrentMatch is not { Teams.Count: 2 })
            {
                return string.Empty;
            }

            return Loc["match.scoreline",
                TeamName(0),
                CurrentMatch.Teams[0].Score,
                CurrentMatch.Teams[1].Score,
                TeamName(1)];
        }
    }

    /// <summary>
    /// Gets the players who could still join: everyone on the list who is not already on the
    /// pitch. Computed rather than stored, so adding someone takes them out of the offer
    /// without anything having to remember to.
    /// </summary>
    private List<Player> AvailablePlayers
    {
        get
        {
            // Captured, so the filter closes over a value the compiler knows is not null
            // rather than over a property it has to keep re-checking.
            var match = CurrentMatch;

            return match == null
                ? []
                : [.. _listPlayers
                    .Where(p => !match.Contains(p.Id))
                    .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)];
        }
    }

    /// <summary>
    /// Gets the name shown for the side at a position in the match, rebuilt from the index for
    /// the same reason the Teams screen rebuilds it: the stored name is generated in Core,
    /// which has no localization of its own.
    /// </summary>
    /// <param name="index">The side's index in the match.</param>
    private string TeamName(int index) => Loc["teams.name", (char)('A' + index)];

    /// <summary>
    /// Gets the modifier class tinting a side's chrome, alternating between the accent and
    /// its sibling shade exactly as the Teams screen does.
    /// </summary>
    /// <param name="index">The side's index in the match.</param>
    private static string TeamColorClass(int index) => index % 2 == 0 ? "team-a" : "team-b";

    /// <summary>
    /// Gets the wording for the button that takes a goal off a side's score. It explains
    /// itself when it is disabled, because "why can I not press this" is the question a score
    /// pinned to its scorers will otherwise raise.
    /// </summary>
    /// <param name="team">The side the button belongs to.</param>
    /// <param name="index">That side's index in the match.</param>
    private string ScoreDownTitle(MatchTeam team, int index)
    {
        if (team.CanDecrementScore)
        {
            return Loc["match.scoreDown", TeamName(index)];
        }

        // Nothing is pinned to anyone and the score is nil, so there is no goal to take off
        // and nothing for the user to go and undo first.
        if (team.ScoreFloor == 0)
        {
            return Loc["match.scoreDownAtZero"];
        }

        // Either tally can be the one holding the floor up, and the message has to name the
        // right one - a score with every goal assisted but no scorer named is held by the
        // assists, and being told to take a goal off a scorer would send the user looking for
        // a scorer who is not there. Level, both hold it: doing only what either of the other
        // two messages says would leave the button just as dead, which is the one thing a
        // message explaining a dead button must not do.
        if (team.AttributedAssists == team.AttributedGoals)
        {
            return Loc["match.scoreDownBlockedByBoth"];
        }

        return team.AttributedAssists > team.AttributedGoals
            ? Loc["match.scoreDownBlockedByAssist"]
            : Loc["match.scoreDownBlocked"];
    }

    #endregion

    #region Recording the game

    /// <summary>
    /// Shows a side's lineup.
    /// </summary>
    /// <param name="index">The side to show.</param>
    private void ShowTeam(int index) => _activeTabIndex = index;

    /// <summary>
    /// Switches sides from the keyboard. The scoreboard halves are divs rather than buttons -
    /// they hold the score's own controls - so the keys a button would have handled are
    /// handled here instead.
    /// </summary>
    /// <param name="args">The key that was pressed.</param>
    /// <param name="index">The side the key was pressed on.</param>
    private void HandleTabKey(KeyboardEventArgs args, int index)
    {
        if (args.Key is "Enter" or " " or "Spacebar")
        {
            ShowTeam(index);
        }
    }

    /// <summary>
    /// Adds a goal to a side without naming who scored it - the corner-of-the-eye goal, or one
    /// whose scorer is settled later.
    /// </summary>
    /// <param name="team">The side that scored.</param>
    private void IncrementScore(MatchTeam team)
    {
        ClearScoreRefusal();

        team.IncrementScore();
    }

    /// <summary>
    /// Takes a goal off a side's score.
    /// </summary>
    /// <param name="team">The side to take it off.</param>
    private void DecrementScore(MatchTeam team)
    {
        ClearScoreRefusal();

        team.DecrementScore();
    }

    /// <summary>
    /// Gets the revision of a side's score box, which is part of its key. Sides that have never
    /// been typed into are at zero rather than being pre-seeded, so this reads past the end of
    /// the list rather than growing it during a render.
    /// </summary>
    /// <param name="index">The side's index in the match.</param>
    private int ScoreRevision(int index) =>
        index >= 0 && index < _scoreRevisions.Count ? _scoreRevisions[index] : 0;

    /// <summary>
    /// Sets a side's score to a figure typed in. Anything below the goals and assists already
    /// pinned to players is refused by the match itself, and anything unreadable is ignored;
    /// either way the box goes back to showing the score as it stands, and a refusal says so
    /// rather than letting the entry disappear without a word.
    /// </summary>
    /// <param name="team">The side being scored.</param>
    /// <param name="index">That side's index in the match.</param>
    /// <param name="args">The value that was typed.</param>
    private void SetScore(MatchTeam team, int index, ChangeEventArgs args)
    {
        while (_scoreRevisions.Count <= index)
        {
            _scoreRevisions.Add(0);
        }

        _scoreRevisions[index]++;
        _scoreRefusal = string.Empty;

        if (!int.TryParse(
                args.Value?.ToString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var entered))
        {
            return;
        }

        if (!team.TrySetScore(entered))
        {
            _scoreRefusal = Loc["match.scoreTooLow", TeamName(index), team.ScoreFloor];
        }
    }

    /// <summary>
    /// Credits a player with a goal, which also raises their side's score once the goals named
    /// exceed whatever was entered by hand.
    /// </summary>
    /// <param name="team">The side they scored for.</param>
    /// <param name="participant">The scorer.</param>
    private void AddGoal(MatchTeam team, MatchPlayer participant)
    {
        ClearScoreRefusal();

        team.AddGoal(participant);
    }

    /// <summary>
    /// Takes a goal back off a player.
    /// </summary>
    /// <param name="team">The side it counted for.</param>
    /// <param name="participant">The player it was credited to.</param>
    private void RemoveGoal(MatchTeam team, MatchPlayer participant)
    {
        ClearScoreRefusal();

        team.RemoveGoal(participant);
    }

    /// <summary>
    /// Credits a player with an assist. The side is asked rather than the player, because
    /// whether there is a goal left to assist is a fact about the side.
    /// </summary>
    /// <param name="team">The side they assisted for.</param>
    /// <param name="participant">The player who made it.</param>
    private void AddAssist(MatchTeam team, MatchPlayer participant)
    {
        ClearScoreRefusal();

        team.AddAssist(participant);
    }

    /// <summary>
    /// Takes an assist back off a player.
    /// </summary>
    /// <param name="team">The side it counted for.</param>
    /// <param name="participant">The player it was credited to.</param>
    private void RemoveAssist(MatchTeam team, MatchPlayer participant)
    {
        ClearScoreRefusal();

        team.RemoveAssist(participant);
    }

    /// <summary>
    /// Drops the complaint about a refused score. Every tally on the screen moves the floor
    /// that refusal quoted, so leaving it up would leave a figure on screen that is no longer
    /// the one the match would refuse.
    /// </summary>
    private void ClearScoreRefusal() => _scoreRefusal = string.Empty;

    /// <summary>
    /// Gets the wording on the button that credits an assist. When there is no goal left to
    /// assist it says what to do about that instead, since a button that is simply dead
    /// invites the question rather than answering it.
    /// </summary>
    /// <param name="team">The side being credited.</param>
    /// <param name="playerName">The player the button belongs to.</param>
    private string AssistUpTitle(MatchTeam team, string playerName) => team.CanAddAssist
        ? Loc["match.assistUp", playerName]
        : Loc["match.assistBlocked"];

    /// <summary>
    /// Sends a player to the other side, goals and assists included. Nothing is rebalanced
    /// around the move: the game is being played, and this is a correction to who is on which
    /// side rather than a new draw.
    /// </summary>
    /// <param name="participant">The player to move off the side being shown.</param>
    private void MovePlayer(MatchPlayer participant)
    {
        if (CurrentMatch is not { Teams.Count: 2 })
        {
            return;
        }

        var index = ActiveIndex;

        ClearScoreRefusal();

        // Their goals go with them, so both scores may move. Both are on this page, which
        // re-renders itself once this handler returns - only the header subline lives in the
        // layout, and a move leaves the number of players on the pitch unchanged.
        MatchRecord.Move(participant, CurrentMatch.Teams[index], CurrentMatch.Teams[1 - index]);
    }

    #endregion

    #region Adding a player mid-match

    /// <summary>
    /// Opens the sheet that adds a late arrival, offering the side being shown first since
    /// that is the one the user was just looking at.
    /// </summary>
    private void AskToAddPlayer()
    {
        _addToTeamIndex = ActiveIndex;
        _pending = PendingAction.AddPlayer;

        RefreshArmedButtons();
    }

    /// <summary>
    /// Puts a player from the list onto the chosen side and shows that side, so the row that
    /// was just added is the one on screen.
    /// </summary>
    /// <param name="player">The player joining.</param>
    private void AddPlayerToSide(Player player)
    {
        if (CurrentMatch == null || _addToTeamIndex < 0 || _addToTeamIndex >= CurrentMatch.Teams.Count)
        {
            return;
        }

        CurrentMatch.Teams[_addToTeamIndex].Add(player);

        _activeTabIndex = _addToTeamIndex;
        _pending = PendingAction.None;

        RefreshArmedButtons();
    }

    /// <summary>
    /// Goes off to create a player who is not on the list at all, remembering the side they
    /// are meant for so the sheet can reopen on it.
    /// </summary>
    /// <remarks>
    /// The full Add Player form is reused rather than a cut-down one being put in the sheet.
    /// A player created here is a real player on the list, with skills and a position like any
    /// other, because a player without them would leave the balance figures on the screen this
    /// match came from unable to account for someone who played in it.
    /// </remarks>
    private void GoToNewPlayer()
    {
        MatchState.ResumeAddingPlayer = true;
        MatchState.AddingToTeamIndex = _addToTeamIndex;

        _pending = PendingAction.None;

        Navigation.NavigateTo(AddPlayerRoute);
    }

    #endregion

    #region Leaving and finishing

    /// <summary>
    /// Asks before leaving. Nothing on this screen has been written down yet, so going back is
    /// what throws the result away.
    /// </summary>
    private void AskToLeave()
    {
        if (CurrentMatch == null)
        {
            Navigation.NavigateTo(TeamsRoute);
            return;
        }

        _pending = PendingAction.Leave;

        RefreshArmedButtons();
    }

    /// <summary>
    /// Leaves, discarding the score, the tallies and anyone added since kick-off. The split
    /// itself is left alone - it is what the user goes back to.
    /// </summary>
    private void ConfirmLeave()
    {
        _pending = PendingAction.None;

        MatchState.ClearMatch();

        Navigation.NavigateTo(TeamsRoute);
    }

    /// <summary>
    /// Asks before finishing, showing the result that is about to be written down.
    /// </summary>
    private void AskToFinish()
    {
        _saveError = string.Empty;
        _pending = PendingAction.Finish;

        RefreshArmedButtons();
    }

    /// <summary>
    /// Writes the match to storage and clears the screen behind it.
    /// </summary>
    /// <remarks>
    /// A failed write leaves everything exactly as it was, sheet included, with the reason on
    /// it. Clearing the match and navigating away on a write that did not happen would throw
    /// away the only copy of a game that had just been played.
    ///
    /// The write is the one thing on this screen that gives way part-done: the sheet is
    /// repainted while it is in flight, so the flag closes the door behind the first tap. The
    /// file is only ever appended to, which makes a second tap a second copy of the match
    /// rather than an overwrite of the first, and no reader could tell that from a game that
    /// really was played twice. It is released only when the write failed and the sheet is
    /// staying open to be tried again; a write that worked leaves the screen entirely.
    /// </remarks>
    private async Task ConfirmFinish()
    {
        if (CurrentMatch == null || _isSaving)
        {
            return;
        }

        _isSaving = true;
        _saveError = string.Empty;

        try
        {
            await MatchRepository.AppendAsync(CurrentMatch);
        }
        catch (Exception ex)
        {
            _saveError = Loc["match.saveError", ex.Message];
            _isSaving = false;

            return;
        }

        _pending = PendingAction.None;

        MatchState.ClearMatch();

        // The split has been played and stored. Leaving it behind would offer a stale draw to
        // whoever opens the Teams screen next.
        TeamState.ClearTeams();

        Navigation.NavigateTo(HomeRoute);
    }

    /// <summary>
    /// Closes whichever sheet is open, changing nothing. It refuses while the match is being
    /// written: the write finishes either way, and closing the sheet over it would leave the
    /// user back on a match that has already been recorded.
    /// </summary>
    private void CancelPending()
    {
        if (_isSaving)
        {
            return;
        }

        _pending = PendingAction.None;
        _saveError = string.Empty;

        RefreshArmedButtons();
    }

    /// <summary>
    /// Repaints the header and footer, which hold two of the three controls a sheet can be
    /// opened from and belong to the layout rather than to this page.
    /// </summary>
    private void RefreshArmedButtons() => Layout?.Refresh();

    /// <summary>
    /// Goes off to pick players, from the empty screen where there is no match to lose.
    /// </summary>
    private void GoToCreateTeams() => Navigation.NavigateTo(SelectPlayersRoute);

    #endregion
}
