using ApexPost.Specs.Support;

namespace ApexPost.Specs.Contexts;

/// <summary>
/// The state of one Shipment creation scenario. Reqnroll creates one instance per scenario and injects it into the
/// hooks and step classes through their constructors, so steps share state without static fields.
/// </summary>
public sealed class ShipmentContext
{
    private SpecApiFactory? _factory;
    private HttpClient? _client;

    /// <summary>The API host of this scenario, created by the BeforeScenario hook.</summary>
    public SpecApiFactory Factory
    {
        get => _factory ?? throw new InvalidOperationException("The BeforeScenario hook did not create the host.");
        set => _factory = value;
    }

    /// <summary>The signed-in caller, set by the Background.</summary>
    public HttpClient Client
    {
        get => _client ?? throw new InvalidOperationException("No one is signed in yet.");
        set => _client = value;
    }

    /// <summary>True once a client was created, so the AfterScenario hook knows what to dispose.</summary>
    public bool HasClient => _client is not null;

    /// <summary>True once the BeforeScenario hook created the host.</summary>
    public bool HasFactory => _factory is not null;

    /// <summary>The answer to the last registration attempt.</summary>
    public HttpResponseMessage? LastResponse { get; set; }
}
