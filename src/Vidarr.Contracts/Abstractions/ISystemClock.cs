namespace Vidarr.Contracts.Abstractions;

public interface ISystemClock
{
    DateTimeOffset UtcNow { get; }
}

public interface IRandom
{
    int Next(int minInclusive, int maxExclusive);
    double NextDouble();
    void NextBytes(Span<byte> buffer);
}

public interface IEnvironment
{
    string? GetVariable(string name);
    string MachineName { get; }
    string ProcessArchitecture { get; }
}
