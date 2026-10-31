using Api.Services;

namespace Api;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/api/health", async (ExternalConnectionHealthService healthService, CancellationToken cancellationToken) =>
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var health = await healthService.CheckAsync(timeout.Token);
            return TypedResults.Json(health, statusCode: health.Status == "healthy"
                ? StatusCodes.Status200OK
                : StatusCodes.Status503ServiceUnavailable);
        });
    }
}