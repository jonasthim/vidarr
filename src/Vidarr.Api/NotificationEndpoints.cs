using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vidarr.Contracts.Models;
using Vidarr.Notifications;

namespace Vidarr.Api;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapVidarrNotificationApi(this IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/api/v1");

        v1.MapGet("/notification/schema", (IEnumerable<INotificationFactory> factories) =>
            Results.Ok(factories.Select(f => new NotificationSchemaDto(
                Implementation: f.Implementation,
                DisplayName: f.DisplayName,
                Fields: f.SettingsSchema.Select(s =>
                    new NotificationSchemaFieldDto(s.Name, s.Label, s.Type, s.Required, s.HelpText)).ToArray(),
                SupportedEvents: f.SupportedEvents.Select(e => e.ToString()).ToArray())).ToArray()));

        v1.MapPost("/notification/test", async (
            NotificationTestRequest req,
            IEnumerable<INotificationFactory> factories,
            CancellationToken ct) =>
        {
            var factory = factories.FirstOrDefault(f =>
                string.Equals(f.Implementation, req.Implementation, StringComparison.OrdinalIgnoreCase));
            if (factory is null)
            {
                return Results.BadRequest(new ApiErrorResponse([new ApiError("implementation", $"Unknown implementation {req.Implementation}")]));
            }
            try
            {
                var n = factory.Create(0, "test", req.SettingsJson ?? "{}",
                    new HashSet<NotificationEventType>(factory.SupportedEvents));
                var result = await n.OnTestAsync(ct);
                return Results.Ok(new NotificationTestResultDto(result.Success, result.Message));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Results.Ok(new NotificationTestResultDto(false, ex.Message));
            }
        });

        return app;
    }
}

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record NotificationSchemaDto(
    string Implementation, string DisplayName,
    IReadOnlyList<NotificationSchemaFieldDto> Fields,
    IReadOnlyList<string> SupportedEvents);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record NotificationSchemaFieldDto(string Name, string Label, string Type, bool Required, string? HelpText);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record NotificationTestRequest(string Implementation, string? SettingsJson);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record NotificationTestResultDto(bool Success, string? Message);
