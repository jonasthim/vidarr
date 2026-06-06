using Vidarr.Contracts.Abstractions;

namespace Vidarr.Tests.Common;

public sealed class FakeProcessRunner : IProcessRunner
{
    private readonly List<ProcessInvocation> _invocations = [];
    private readonly List<ProcessStubRule> _rules = [];
    private ProcessResult _default = new(0, string.Empty, string.Empty, TimeSpan.Zero);

    public IReadOnlyList<ProcessInvocation> Invocations => _invocations;

    public FakeProcessRunner WhenExecutable(string executable, ProcessResult result)
    {
        _rules.Add(new ProcessStubRule(inv => inv.Executable == executable, _ => Task.FromResult(result), null));
        return this;
    }

    public FakeProcessRunner WhenInvocation(Func<ProcessInvocation, bool> predicate, ProcessResult result)
    {
        _rules.Add(new ProcessStubRule(predicate, _ => Task.FromResult(result), null));
        return this;
    }

    public FakeProcessRunner WhenInvocation(
        Func<ProcessInvocation, bool> predicate,
        ProcessResult result,
        IReadOnlyList<string> streamingLines)
    {
        _rules.Add(new ProcessStubRule(predicate, _ => Task.FromResult(result), streamingLines));
        return this;
    }

    public FakeProcessRunner SetDefault(ProcessResult result)
    {
        _default = result;
        return this;
    }

    public Task<ProcessResult> RunAsync(ProcessInvocation invocation, CancellationToken ct) =>
        RunStreamingAsync(invocation, null, null, ct);

    public async Task<ProcessResult> RunStreamingAsync(
        ProcessInvocation invocation,
        Func<string, CancellationToken, Task>? onStdoutLine,
        Func<string, CancellationToken, Task>? onStderrLine,
        CancellationToken ct)
    {
        _invocations.Add(invocation);
        foreach (var rule in _rules)
        {
            if (rule.Predicate(invocation))
            {
                if (rule.StreamingLines is not null && onStdoutLine is not null)
                {
                    foreach (var line in rule.StreamingLines)
                    {
                        await onStdoutLine(line, ct);
                    }
                }
                return await rule.Respond(invocation);
            }
        }
        return _default;
    }

    private sealed record ProcessStubRule(
        Func<ProcessInvocation, bool> Predicate,
        Func<ProcessInvocation, Task<ProcessResult>> Respond,
        IReadOnlyList<string>? StreamingLines);
}
