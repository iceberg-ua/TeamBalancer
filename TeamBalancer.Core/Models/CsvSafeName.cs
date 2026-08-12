namespace TeamBalancer.Core.Models;

/// <summary>
/// The naming rules shared by everything the user types and the app then writes into a CSV
/// cell of its own - player names and player list names. They live in one place so the two
/// cannot drift apart: a name one screen accepts and the other rejects would be a name that
/// survives storage or not depending on where it happened to be entered.
/// </summary>
public static class CsvSafeName
{
    /// <summary>
    /// The longest name accepted. This is a display limit - names are shown in full on narrow
    /// phone rows - rather than anything the CSV format imposes.
    /// </summary>
    public const int MaxLength = 15;

    /// <summary>
    /// Validates that a name neither breaks the CSV format nor carries a formula a spreadsheet
    /// would execute when the exported file is opened.
    /// </summary>
    /// <param name="name">The name to validate.</param>
    /// <returns>True if the name is valid, false otherwise.</returns>
    public static bool IsValid(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        // Trim to check for leading/trailing whitespace
        if (name != name.Trim())
        {
            return false;
        }

        // Check for CSV special characters that would break parsing
        if (name.Contains(',') || name.Contains('"') || name.Contains('\n') || name.Contains('\r'))
        {
            return false;
        }

        // Check for CSV injection characters (formula injection attack prevention)
        // These characters at the start of a cell can cause Excel/Sheets to execute formulas
        char firstChar = name[0];
        if (firstChar == '=' || firstChar == '+' || firstChar == '-' || firstChar == '@' ||
            firstChar == '\t' || firstChar == '\r')
        {
            return false;
        }

        // Reasonable length limit for UI display
        if (name.Length > MaxLength)
        {
            return false;
        }

        return true;
    }
}
