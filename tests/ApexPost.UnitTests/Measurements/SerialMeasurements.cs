namespace ApexPost.UnitTests.Measurements;

/// <summary>Measurements run one at a time, so they do not compete with each other for the CPU.</summary>
[CollectionDefinition(nameof(SerialMeasurements), DisableParallelization = true)]
public sealed class SerialMeasurements;
