using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Vidarr.Contracts.Abstractions;

namespace Vidarr.Infrastructure;

[ExcludeFromCodeCoverage(Justification = "Trivial wrappers over framework primitives.")]
public sealed class SystemClock : ISystemClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

[ExcludeFromCodeCoverage(Justification = "Trivial wrappers over framework primitives.")]
public sealed class RandomAdapter : IRandom
{
    private readonly Random _random = Random.Shared;

    public int Next(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);

    public double NextDouble() => _random.NextDouble();

    public void NextBytes(Span<byte> buffer) => _random.NextBytes(buffer);
}

[ExcludeFromCodeCoverage(Justification = "Trivial wrappers over framework primitives.")]
public sealed class EnvironmentAdapter : IEnvironment
{
    public string? GetVariable(string name) => Environment.GetEnvironmentVariable(name);

    public string MachineName => Environment.MachineName;

    public string ProcessArchitecture => RuntimeInformation.ProcessArchitecture.ToString();
}
