using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Pexilio.Application.Health;

namespace Pexilio.Api.Health;

internal static class HealthResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        var databaseUp = report.Entries.TryGetValue("database", out var entry)
            && entry.Status == HealthStatus.Healthy;

        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";

        var snapshot = new HealthSnapshot(
            HealthEvaluation.Overall(databaseUp),
            HealthEvaluation.Database(databaseUp));

        return context.Response.WriteAsJsonAsync(snapshot, JsonOptions);
    }
}
