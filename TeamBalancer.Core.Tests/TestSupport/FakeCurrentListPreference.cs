namespace TeamBalancer.Core.Tests.TestSupport;

using TeamBalancer.Core.Services.Interfaces;

/// <summary>
/// Holds the active player list in memory, standing in for the platform's preferences.
/// </summary>
public sealed class FakeCurrentListPreference : ICurrentListPreference
{
    /// <param name="stored">The list id already on the device, if any.</param>
    public FakeCurrentListPreference(Guid? stored = null)
    {
        Stored = stored;
    }

    /// <summary>
    /// Gets the id currently stored, as the next launch would read it.
    /// </summary>
    public Guid? Stored { get; private set; }

    public Guid? Read() => Stored;

    public void Write(Guid listId) => Stored = listId;
}
