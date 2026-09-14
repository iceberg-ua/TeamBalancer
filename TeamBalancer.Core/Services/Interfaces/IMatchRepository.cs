namespace TeamBalancer.Core.Services.Interfaces;

using TeamBalancer.Core.Models;

/// <summary>
/// Defines how finished matches are stored and read back.
/// </summary>
public interface IMatchRepository
{
    /// <summary>
    /// Stores a finished match. A stored match is never edited afterwards - only added, and
    /// removed whole if the user deletes it - which is what makes finishing a game one cheap
    /// write however many have been played before it.
    /// </summary>
    /// <param name="match">The match to store.</param>
    Task AppendAsync(MatchRecord match);

    /// <summary>
    /// Reads back every match ever finished, most recent first.
    /// </summary>
    /// <remarks>
    /// Everything is returned rather than the caller's list alone: filtering is a decision for
    /// the screen, and a repository that could only answer for one list would have to be asked
    /// again for every other. Storage is one flat file, so reading all of it is what reading
    /// any of it costs anyway.
    /// </remarks>
    /// <returns>The finished matches, newest first; empty when none have been played.</returns>
    Task<IReadOnlyList<FinishedMatch>> GetAllAsync();

    /// <summary>
    /// Reads back one finished match, or null when the storage no longer holds it.
    /// </summary>
    /// <remarks>
    /// Here so that a screen showing one game does not have to build every game to find it.
    /// A match that is no longer there is an ordinary answer rather than a failure: an address
    /// can outlive the match it names, and the screen says so.
    /// </remarks>
    /// <param name="matchId">The match to read.</param>
    /// <returns>The match, or null when no result is stored under that identifier.</returns>
    Task<FinishedMatch?> GetByIdAsync(Guid matchId);

    /// <summary>
    /// Removes one finished match from storage, all of it.
    /// </summary>
    /// <remarks>
    /// A match that is not there is an ordinary answer rather than a failure, for the reason
    /// <see cref="GetByIdAsync"/> gives: the screen asking can be showing a match that has
    /// already gone.
    /// </remarks>
    /// <param name="matchId">The match to remove.</param>
    /// <returns>
    /// True when the match was found and removed; false when nothing was stored under that
    /// identifier.
    /// </returns>
    Task<bool> DeleteAsync(Guid matchId);
}
