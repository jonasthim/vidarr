using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vidarr.Catalog.Repositories;

namespace Vidarr.Api;

/// <summary>
/// Resolves and rotates the REST API key. Resolution order:
///   1. Override env / appsettings value (when set at boot, locked in)
///   2. ApplicationConfig.ApiKey in the database
///   3. Generate, persist, return
/// </summary>
public interface IApiKeyService
{
    Task<string> GetCurrentAsync(CancellationToken ct);
    Task<string> RotateAsync(CancellationToken ct);
}

/// <summary>
/// Static override resolved at host startup (env var or appsettings). When non-null,
/// the service returns this verbatim and refuses to rotate (the operator owns the value).
/// </summary>
public sealed record ApiKeyOverride(string? Value);

public sealed class ApiKeyService : IApiKeyService
{
    private readonly ApiKeyOverride _override;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ApiKeyService> _logger;
    private readonly object _gate = new();
    private string? _cached;

    public ApiKeyService(
        ApiKeyOverride @override,
        IServiceScopeFactory scopeFactory,
        ILogger<ApiKeyService> logger)
    {
        _override = @override;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<string> GetCurrentAsync(CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(_override.Value))
        {
            return _override.Value;
        }
        string? snapshot;
        lock (_gate) { snapshot = _cached; }
        if (!string.IsNullOrEmpty(snapshot)) return snapshot;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IApplicationConfigRepository>();
        var cfg = await repo.GetAsync(ct);
        var generated = false;
        if (string.IsNullOrEmpty(cfg.ApiKey))
        {
            cfg.ApiKey = GenerateKey();
            await repo.UpdateAsync(cfg, ct);
            generated = true;
        }
        lock (_gate) { _cached = cfg.ApiKey; }
        if (generated) _logger.LogInformation("Generated new API key on first boot");
        return cfg.ApiKey!;
    }

    public async Task<string> RotateAsync(CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(_override.Value))
        {
            throw new InvalidOperationException(
                "API key is fixed by an environment variable or appsettings override; clear that to rotate.");
        }
        await using var scope = _scopeFactory.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IApplicationConfigRepository>();
        var cfg = await repo.GetAsync(ct);
        cfg.ApiKey = GenerateKey();
        await repo.UpdateAsync(cfg, ct);
        lock (_gate) { _cached = cfg.ApiKey; }
        _logger.LogInformation("API key rotated");
        return cfg.ApiKey;
    }

    private static string GenerateKey() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}
