namespace TeamBalancer.Extensions;

using TeamBalancer.Core.Models;

/// <summary>
/// Display helpers for rendering <see cref="Position"/> values in the UI.
/// </summary>
public static class PositionExtensions
{
    /// <summary>
    /// Gets the positions a user is allowed to pick, i.e. every position except
    /// <see cref="Position.Unspecified"/>, which exists only to represent missing data.
    /// </summary>
    public static IReadOnlyList<Position> SelectablePositions { get; } =
    [
        Position.Goalkeeper,
        Position.Defender,
        Position.Midfielder,
        Position.Forward
    ];

    /// <summary>
    /// Gets the compact abbreviation used where horizontal space is tight, such as list
    /// items and team summaries.
    /// </summary>
    /// <param name="position">The position to abbreviate.</param>
    /// <returns>A short abbreviation, or "?" when no position is set.</returns>
    public static string ToAbbreviation(this Position position) => position switch
    {
        Position.Goalkeeper => "GK",
        Position.Defender => "DEF",
        Position.Midfielder => "MID",
        Position.Forward => "FWD",
        _ => "?"
    };

    /// <summary>
    /// Gets the full, human-readable name used in form controls.
    /// </summary>
    /// <param name="position">The position to name.</param>
    /// <returns>The display name, or "Not set" when no position is set.</returns>
    public static string ToDisplayName(this Position position) => position switch
    {
        Position.Goalkeeper => "Goalkeeper",
        Position.Defender => "Defender",
        Position.Midfielder => "Midfielder",
        Position.Forward => "Forward",
        _ => "Not set"
    };

    /// <summary>
    /// Gets the rank that orders players down the pitch - goalkeeper, defender, midfielder,
    /// forward - with unset positions last. <see cref="Position.Unspecified"/> is zero in the
    /// enum, so casting to int would sort it first instead; hence the explicit ranks.
    /// </summary>
    /// <param name="position">The position to rank.</param>
    /// <returns>A sort key, lowest first.</returns>
    public static int ToSortOrder(this Position position) => position switch
    {
        Position.Goalkeeper => 0,
        Position.Defender => 1,
        Position.Midfielder => 2,
        Position.Forward => 3,
        _ => 4
    };

    /// <summary>
    /// Gets the CSS modifier class that colours a badge or chip for this position.
    /// </summary>
    /// <param name="position">The position to style.</param>
    /// <returns>A CSS class name.</returns>
    public static string ToBadgeClass(this Position position) => position switch
    {
        Position.Goalkeeper => "pos-gk",
        Position.Defender => "pos-def",
        Position.Midfielder => "pos-mid",
        Position.Forward => "pos-fwd",
        _ => "pos-unset"
    };
}
