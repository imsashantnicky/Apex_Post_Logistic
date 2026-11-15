#:property PublishAot=false
// PublishAot is on by default for file-based apps and would pull in the ILCompiler package, which NuGet audit flags.
// Pretty-prints one JSON file for scripts/export-contracts.*: two-space indent, readable characters (→, +) instead of
// \u escapes, LF line endings and a final newline, so Windows and Linux write byte-identical contracts.
// Usage: dotnet run scripts/format-json.cs -- <input.json> <output.json>
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

if (args.Length != 2)
{
    await Console.Error.WriteLineAsync("Usage: dotnet run scripts/format-json.cs -- <input.json> <output.json>");
    return 2;
}

var document = JsonNode.Parse(await File.ReadAllTextAsync(args[0]))
    ?? throw new InvalidDataException($"{args[0]} holds no JSON value.");
var options = new JsonSerializerOptions
{
    WriteIndented = true,
    IndentSize = 2,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
};
await File.WriteAllTextAsync(args[1], document.ToJsonString(options).ReplaceLineEndings("\n") + "\n");
return 0;
