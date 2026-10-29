namespace Pexilio.Application.Health;

public static class HealthEvaluation
{
    public const string Healthy = "healthy";
    public const string Unhealthy = "unhealthy";
    public const string DatabaseUp = "up";
    public const string DatabaseDown = "down";

    public static string Overall(bool databaseConnected) =>
        databaseConnected ? Healthy : Unhealthy;

    public static string Database(bool databaseConnected) =>
        databaseConnected ? DatabaseUp : DatabaseDown;
}

public sealed record HealthSnapshot(string Status, string Database);
