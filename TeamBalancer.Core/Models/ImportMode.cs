namespace TeamBalancer.Core.Models;

/// <summary>
/// What an import should do about a player the list already holds.
/// </summary>
public enum ImportMode
{
    /// <summary>
    /// Add players the list does not have and leave the ones it does exactly as they are. This
    /// is what a fresh list wants, where nothing can collide in the first place.
    /// </summary>
    AddOnly = 0,

    /// <summary>
    /// Add players the list does not have, and take the sender's ratings and positions for the
    /// ones it does. Nothing is ever removed: a player in the list but absent from the import
    /// stays, because the sender not having someone is not the same as saying they left.
    /// </summary>
    /// <remarks>
    /// This is what makes re-sharing a squad useful. Under <see cref="AddOnly"/>, receiving the
    /// same squad again after the organiser adjusted a few ratings reports every player as a
    /// duplicate and changes nothing - which is technically correct and completely useless.
    /// </remarks>
    Merge = 1
}
