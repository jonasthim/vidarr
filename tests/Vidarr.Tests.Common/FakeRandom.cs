using Vidarr.Contracts.Abstractions;

namespace Vidarr.Tests.Common;

public sealed class FakeRandom : IRandom
{
    private readonly Queue<int> _ints;
    private readonly Queue<double> _doubles;

    public FakeRandom(IEnumerable<int>? ints = null, IEnumerable<double>? doubles = null)
    {
        _ints = new Queue<int>(ints ?? []);
        _doubles = new Queue<double>(doubles ?? []);
    }

    public int Next(int minInclusive, int maxExclusive) =>
        _ints.Count > 0 ? _ints.Dequeue() : minInclusive;

    public double NextDouble() => _doubles.Count > 0 ? _doubles.Dequeue() : 0.0;

    public void NextBytes(Span<byte> buffer) => buffer.Clear();
}
