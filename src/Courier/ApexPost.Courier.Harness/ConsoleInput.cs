using ApexPost.Courier.Domain.Enums;

namespace ApexPost.Courier.Harness;

/// <summary>
/// Reads and validates menu input. Every method returns <see langword="null"/> when the user leaves a required
/// value empty (cancel) or the input ends (end of file or Ctrl+C), so the caller goes back to the menu.
/// </summary>
internal sealed class ConsoleInput(TextReader input, TextWriter output)
{
    private const string CancelHint = "leave empty to cancel";

    /// <summary>Prompts once and returns the trimmed line, or <see langword="null"/> when the input ended.</summary>
    public string? ReadLine(string prompt)
    {
        output.Write($"{prompt}: ");
        return input.ReadLine()?.Trim();
    }

    /// <summary>Prompts until the value is empty (cancel) or passes <paramref name="isValid"/>.</summary>
    /// <param name="prompt">The question shown to the user.</param>
    /// <param name="isValid">The rule the normalised value must pass.</param>
    /// <param name="rule">Explains the rule when the value is rejected.</param>
    /// <param name="normalise">Applied before validation, for example upper-casing.</param>
    public string? ReadValid(
        string prompt, Func<string, bool> isValid, string rule, Func<string, string>? normalise = null)
    {
        while (true)
        {
            var text = ReadLine($"{prompt} ({CancelHint})");
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            var value = normalise is null ? text : normalise(text);
            if (isValid(value))
            {
                return value;
            }

            output.WriteLine($"  Invalid value '{text}': {rule}");
        }
    }

    /// <summary>Prompts for any non-empty text.</summary>
    public string? ReadRequired(string prompt) => ReadValid(prompt, _ => true, string.Empty);

    /// <summary>Prompts for text that may be empty. Returns an empty string for no value.</summary>
    public string? ReadOptional(string prompt) => ReadLine(prompt);

    /// <summary>Prompts for a scan type by number (1–4) or by name.</summary>
    public ScanType? ReadScanType()
    {
        var types = Enum.GetValues<ScanType>();
        output.WriteLine("  Scan types: " + string.Join("  ", types.Select(type => $"{(int)type} {type}")));
        var text = ReadValid("Scan type", text => TryParseScanType(text, out _), "enter 1-4 or a scan type name.");
        return text is not null && TryParseScanType(text, out var scanType) ? scanType : null;
    }

    /// <summary>Asks a yes/no question. Anything other than y or yes counts as no.</summary>
    public bool ReadYesNo(string prompt) =>
        ReadLine($"{prompt} (y/N)") is { } answer
        && (answer.Equals("y", StringComparison.OrdinalIgnoreCase)
            || answer.Equals("yes", StringComparison.OrdinalIgnoreCase));

    // Only defined names or their numbers; Enum.TryParse alone would accept any number such as "42".
    private static bool TryParseScanType(string text, out ScanType scanType)
    {
        scanType = default;
        var match = Enum.GetValues<ScanType>().Where(type =>
            text.Equals(type.ToString(), StringComparison.OrdinalIgnoreCase)
            || text == ((int)type).ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList();
        if (match.Count != 1)
        {
            return false;
        }

        scanType = match[0];
        return true;
    }
}
