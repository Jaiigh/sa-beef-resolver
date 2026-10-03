using HumanResolve.ArgumentService.Clients;
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
builder.Services.AddGrpcReflection();
builder.Services.AddSingleton<IArgumentRepository, InMemoryArgumentRepository>();
var beefApiUrl = builder.Configuration["BEEF_API_URL"] ?? "http://localhost:3000";
builder.Services.AddHttpClient<IBeefClient, HttpBeefClient>(client =>
{
    client.BaseAddress = new Uri(beefApiUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(3);
});

var app = builder.Build();
app.MapGrpcService<ArgumentGrpcService>();
app.MapGrpcReflectionService(); // lets Postman/grpcurl discover methods without the .proto file
app.MapGet("/", () => "This endpoint is a gRPC server. Use a gRPC client to communicate.");
app.Run();
