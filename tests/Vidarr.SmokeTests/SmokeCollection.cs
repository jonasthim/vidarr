namespace Vidarr.SmokeTests;

[CollectionDefinition(nameof(SmokeCollection))]
public sealed class SmokeCollection : ICollectionFixture<SmokeFactory>
{
    // Marker. Both ConnectivitySmokeTests and VerticalSmokeTests opt in via
    // [Collection(nameof(SmokeCollection))] so they share one SmokeFactory
    // instance (and one SQLite path) instead of racing on the env vars set
    // in SmokeFactory's constructor.
}
