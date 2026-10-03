using HumanResolve.ArgumentService.Repositories;
using HumanResolve.ArgumentService.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);
var grpcPort = builder.Configuration.GetValue<int?>("GRPC_PORT") ?? 50051;

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(grpcPort, listen => listen.Protocols = HttpProtocols.Http2);
});

builder.Services.AddGrpc();
builder.Services.AddSingleton<IArgumentRepository, InMemoryArgumentRepository>();

var app = builder.Build();
app.MapGrpcService<ArgumentGrpcService>();
app.MapGet("/", () => "This endpoint is a gRPC server. Use a gRPC client to communicate.");
app.Run();
