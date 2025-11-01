namespace TeamBalancer.Core.Exceptions;

/// <summary>
/// Exception thrown when CSV parsing fails.
/// </summary>
public class CsvParseException : Exception
{
    /// <summary>
    /// Gets the line number where the parsing error occurred.
    /// </summary>
    public int LineNumber { get; }

    /// <summary>
    /// Gets the content of the line that failed to parse.
    /// </summary>
    public string? LineContent { get; }

    public CsvParseException(string message) : base(message)
    {
    }

    public CsvParseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public CsvParseException(string message, int lineNumber, string? lineContent = null)
        : base(message)
    {
        LineNumber = lineNumber;
        LineContent = lineContent;
    }

    public CsvParseException(string message, int lineNumber, string? lineContent, Exception innerException)
        : base(message, innerException)
    {
        LineNumber = lineNumber;
        LineContent = lineContent;
    }
}
