using HumanResolve.ArgumentService.Repositories;
using HumanResolve.ArgumentService.Services;
using HumanResolve.ArgumentService.Messaging;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var grpcPort = builder.Configuration.GetValue<int?>("GRPC_PORT") ?? 50051;

builder.WebHost.ConfigureKestrel(options =>
{
    if (builder.Configuration.GetValue<bool>("GRPC_BIND_ANY"))
        options.ListenAnyIP(grpcPort, listen => listen.Protocols = HttpProtocols.Http2);
    else
        options.ListenLocalhost(grpcPort, listen => listen.Protocols = HttpProtocols.Http2);
});

builder.Services.AddGrpc();
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(builder.Configuration["EVIDENCE_DB_CONNECTION"]
    ?? "Host=localhost;Port=5432;Database=evidence_db;Username=evidence;Password=evidence-local-only"));
builder.Services.AddSingleton<IArgumentRepository, PostgresArgumentRepository>();
builder.Services.AddSingleton<RabbitMqAiPublisher>();
builder.Services.AddSingleton<AiParticipantRegistry>();

var app = builder.Build();
PostgresArgumentRepository.Initialize(app.Services.GetRequiredService<NpgsqlDataSource>());
if (builder.Configuration["import-legacy"] is string importPath)
{
    var count = PostgresArgumentRepository.ImportLegacy(app.Services.GetRequiredService<NpgsqlDataSource>(), importPath);
    Console.WriteLine($"Imported {count} legacy arguments");
    return;
}
app.MapGrpcService<ArgumentGrpcService>();
app.MapGet("/", () => "This endpoint is a gRPC server. Use a gRPC client to communicate.");
app.Run();
