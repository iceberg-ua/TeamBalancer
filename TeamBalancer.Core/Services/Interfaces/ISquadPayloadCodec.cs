namespace TeamBalancer.Core.Services.Interfaces;

using TeamBalancer.Core.Models;

/// <summary>
/// Turns a squad into the text a QR code carries, and back again.
/// </summary>
public interface ISquadPayloadCodec
{
    /// <summary>
    /// Encodes a squad as QR text.
    /// </summary>
    /// <param name="payload">The list name and players to carry.</param>
    /// <returns>The text to put in the QR code.</returns>
    string Encode(SquadPayload payload);

    /// <summary>
    /// Decodes QR text back into a squad.
    /// </summary>
    /// <param name="qrText">The text read from a QR code.</param>
    /// <returns>The list name and players the code carried.</returns>
    /// <exception cref="Exceptions.SquadPayloadException">
    /// The text is not a squad this version can read.
    /// </exception>
    SquadPayload Decode(string qrText);

    /// <summary>
    /// Reports whether text looks like a squad code at all, without doing the work of decoding
    /// it. This is what tells "you scanned a bus timetable" apart from "you scanned a damaged
    /// squad" - two things the user needs to hear differently.
    /// </summary>
    /// <param name="qrText">The text read from a QR code.</param>
    /// <returns>True if the text carries the squad marker, false otherwise.</returns>
    bool IsSquadCode(string? qrText);
}
