using Pexilio.Application.Health;

namespace Pexilio.Domain.Tests;

public sealed class HealthEvaluationTests
{
    [Fact]
    public void Overall_is_healthy_only_when_the_database_is_connected()
    {
        Assert.Equal(HealthEvaluation.Healthy, HealthEvaluation.Overall(true));
        Assert.Equal(HealthEvaluation.Unhealthy, HealthEvaluation.Overall(false));
    }

    [Fact]
    public void Database_label_follows_the_connection()
    {
        Assert.Equal(HealthEvaluation.DatabaseUp, HealthEvaluation.Database(true));
        Assert.Equal(HealthEvaluation.DatabaseDown, HealthEvaluation.Database(false));
    }
}
