namespace TeamBalancer.Extensions;

using TeamBalancer.Core.Localization;
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
    /// items and team summaries. These stay untranslated deliberately: GK/DEF/MID/FWD are
    /// the abbreviations football uses whatever language it is watched in, and they have to
    /// fit a badge that is only a few characters wide.
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
    /// Gets the full, human-readable name used in form controls, in the app's current
    /// language. The service is a parameter rather than an injected dependency because an
    /// extension method has nowhere for a container to inject one.
    /// </summary>
    /// <param name="position">The position to name.</param>
    /// <param name="loc">The localization service to read the name from.</param>
    /// <returns>The display name, or the "not set" wording when no position is set.</returns>
    public static string ToDisplayName(this Position position, ILocalizationService loc) => position switch
    {
        Position.Goalkeeper => loc["position.goalkeeper"],
        Position.Defender => loc["position.defender"],
        Position.Midfielder => loc["position.midfielder"],
        Position.Forward => loc["position.forward"],
        _ => loc["position.notSet"]
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
}
