namespace TeamBalancer.Services;

using TeamBalancer.Core.Services.Interfaces;

/// <summary>
/// Stores the active player list in the platform's local preferences. Like the language, it
/// stays out of the CSV files on purpose: which list this device happens to be showing is a
/// setting of the install, not part of the squad data the user imports and exports.
/// </summary>
public sealed class MauiCurrentListPreference : ICurrentListPreference
{
    /// <summary>
    /// The preferences key the active list's identifier is stored under.
    /// </summary>
    private const string PreferenceKey = "ActivePlayerListId";

    /// <inheritdoc />
    public Guid? Read()
    {
        var stored = Preferences.Default.Get(PreferenceKey, string.Empty);

        return Guid.TryParse(stored, out var listId) ? listId : null;
    }

    /// <inheritdoc />
    public void Write(Guid listId) => Preferences.Default.Set(PreferenceKey, listId.ToString());
}
