using Vidarr.Contracts.Abstractions;

namespace Vidarr.Tests.Common;

public sealed class FakeEnvironment : IEnvironment
{
    private readonly Dictionary<string, string> _vars;

    public FakeEnvironment(IDictionary<string, string>? vars = null, string machineName = "fake-host", string processArchitecture = "X64")
    {
        _vars = vars is null ? new Dictionary<string, string>() : new Dictionary<string, string>(vars);
        MachineName = machineName;
        ProcessArchitecture = processArchitecture;
    }

    public string? GetVariable(string name) => _vars.TryGetValue(name, out var value) ? value : null;

    public string MachineName { get; }

    public string ProcessArchitecture { get; }

    public void Set(string name, string value) => _vars[name] = value;
}
