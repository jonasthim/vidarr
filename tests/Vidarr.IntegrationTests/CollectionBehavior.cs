using Xunit;

// Process-wide env vars (VIDARR_API_KEY, VIDARR_SQLITE_PATH, ...) are read by WebApplication.CreateBuilder
// at factory construction time. Running tests in parallel across classes risks env-var stomping; serialise.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
