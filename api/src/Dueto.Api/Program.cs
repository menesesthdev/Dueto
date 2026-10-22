using Dueto.Api.Endpoints;
using Dueto.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Erros no formato ProblemDetails (RFC 7807): nunca vazamos stack trace.
builder.Services.AddProblemDetails();

// Tempo sempre via TimeProvider, nunca DateTime.UtcNow direto na lógica.
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddHealthChecks();

builder.Services.AddDuetoInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapHealthEndpoints();

app.Run();

// Exposto para os testes de integração (WebApplicationFactory<Program>).
public partial class Program;
