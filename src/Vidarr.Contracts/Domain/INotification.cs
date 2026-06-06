using Vidarr.Contracts.Events;
using Vidarr.Contracts.Models;

namespace Vidarr.Contracts.Domain;

public interface INotification
{
    int Id { get; }
    string Name { get; }
    IReadOnlySet<NotificationEventType> SupportedEvents { get; }

    Task OnGrabAsync(GrabEvent evt, CancellationToken ct);
    Task OnImportAsync(ImportEvent evt, CancellationToken ct);
    Task OnUpgradeAsync(UpgradeEvent evt, CancellationToken ct);
    Task OnDeleteAsync(DeleteEvent evt, CancellationToken ct);
    Task OnHealthIssueAsync(HealthIssueEvent evt, CancellationToken ct);
    Task<NotificationTestResult> OnTestAsync(CancellationToken ct);
}

public sealed record NotificationTestResult(bool Success, string? Message);
