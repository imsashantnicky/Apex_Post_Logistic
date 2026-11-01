namespace ApexPost.Application.Settings;

/// <summary>The "Catalog" section: how long products and zones stay in the in-memory cache.</summary>
public sealed class CatalogSettings
{
    public const string SectionName = "Catalog";

    public int CacheSeconds { get; set; } = 300;
}
