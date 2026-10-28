using Dueto.Api.Authentication;
using Dueto.Api.Endpoints;
using Dueto.Api.Hubs;
using Dueto.Api.Security;
using Dueto.Core.State;
using Dueto.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Erros no formato ProblemDetails (RFC 7807): nunca vazamos stack trace.
builder.Services.AddProblemDetails();

// Tempo sempre via TimeProvider, nunca DateTime.UtcNow direto na lógica.
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddHealthChecks();

builder.Services.AddDuetoInfrastructure(builder.Configuration);
builder.Services.AddDuetoSecurity(builder.Configuration);

builder.Services
    .AddAuthentication(DeviceTokenAuthenticationHandler.SchemeName)
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, DeviceTokenAuthenticationHandler>(
        DeviceTokenAuthenticationHandler.SchemeName,
        _ => { });
builder.Services.AddAuthorization();

builder.Services.AddSignalR();
builder.Services.AddScoped<IStateNotifier, SignalRStateNotifier>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseCors(CorsPolicyOptions.PolicyName);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthEndpoints();
app.MapWallpaperEndpoints();
app.MapPairingEndpoints();
app.MapStateEndpoints();
app.MapPushEndpoints();
app.MapHub<SyncHub>(SyncHub.Path);

app.Run();

// Exposto para os testes de integração (WebApplicationFactory<Program>).
public partial class Program;
