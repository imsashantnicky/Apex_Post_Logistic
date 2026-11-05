namespace ApexPost.Courier.Harness;

/// <summary>Writes rows as a plain-text table whose columns are padded to the widest cell.</summary>
internal static class TableWriter
{
    private const string ColumnSeparator = " | ";
    private const string RuleSeparator = "-+-";
    private const char RuleCharacter = '-';
    private const string Ellipsis = "...";

    /// <summary>Writes a header line, a rule and one line per row.</summary>
    public static void Write(TextWriter output, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        var allRows = rows.ToList();
        var widths = headers
            .Select((header, column) => allRows.Select(row => row[column].Length).Prepend(header.Length).Max())
            .ToArray();

        output.WriteLine(Line(headers, widths));
        output.WriteLine(string.Join(RuleSeparator, widths.Select(width => new string(RuleCharacter, width))));
        foreach (var row in allRows)
        {
            output.WriteLine(Line(row, widths));
        }
    }

    /// <summary>Shortens <paramref name="text"/> to at most <paramref name="maxLength"/> characters.</summary>
    public static string Truncate(string? text, int maxLength) =>
        text is null || text.Length <= maxLength
            ? text ?? string.Empty
            : text[..(maxLength - Ellipsis.Length)] + Ellipsis;

    private static string Line(IReadOnlyList<string> cells, int[] widths) =>
        string.Join(ColumnSeparator, cells.Select((cell, column) => cell.PadRight(widths[column]))).TrimEnd();
}
