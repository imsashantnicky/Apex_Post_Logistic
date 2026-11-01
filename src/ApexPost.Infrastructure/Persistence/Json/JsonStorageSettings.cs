namespace ApexPost.Infrastructure.Persistence.Json;

/// <summary>
/// The "Storage" section: the folder holding the JSON data files. A relative path is resolved against the
/// content root when the options are built.
/// </summary>
public sealed class JsonStorageSettings
{
    public const string SectionName = "Storage";

    public string DataFolder { get; set; } = "data";
}
