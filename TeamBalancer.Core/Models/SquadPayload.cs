namespace TeamBalancer.Core.Models;

/// <summary>
/// A squad as it travels between two phones: the name the sender's list had, and the players
/// in the same CSV the app exports and imports everywhere else. Reusing the CSV rather than
/// inventing a wire format is deliberate - a shared squad and an exported file are then the
/// same thing carried differently, and there is only one parser to keep correct.
/// </summary>
/// <param name="ListName">
/// The name the list had on the sending device. It is a suggestion, not a decision: the
/// receiver is asked where the players should go and can name the list whatever they like.
/// </param>
/// <param name="PlayersCsv">The players, in the app's export CSV format.</param>
public sealed record SquadPayload(string ListName, string PlayersCsv);
