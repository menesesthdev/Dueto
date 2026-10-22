namespace Dueto.Api.Endpoints;

/// <summary>Endpoint de saúde do serviço.</summary>
public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        // Responde "Healthy" em texto puro (padrão do health check do ASP.NET Core).
        app.MapHealthChecks("/health");
        return app;
    }
}
