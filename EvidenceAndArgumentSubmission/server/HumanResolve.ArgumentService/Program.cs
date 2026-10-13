using System.Text;
using HumanResolve.ArgumentService.Auth;
using HumanResolve.ArgumentService.Repositories;
using HumanResolve.ArgumentService.Services;
using HumanResolve.ArgumentService.Messaging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var grpcPort = builder.Configuration.GetValue<int?>("GRPC_PORT") ?? 50051;
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.Issuer) || string.IsNullOrWhiteSpace(jwt.Audience) || jwt.SigningKey.Length < 32)
    throw new InvalidOperationException("Authentication:Jwt must specify Issuer, Audience, and a SigningKey of at least 32 characters.");

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
builder.Services.AddSingleton<AuthRepository>();
builder.Services.AddSingleton(jwt);
builder.Services.AddSingleton<IPasswordService, PasswordService>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddSingleton<RabbitMqAiPublisher>();
builder.Services.AddSingleton<AiParticipantRegistry>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => options.TokenValidationParameters = new()
    {
        ValidateIssuer = true, ValidIssuer = jwt.Issuer,
        ValidateAudience = true, ValidAudience = jwt.Audience,
        ValidateLifetime = true, ClockSkew = TimeSpan.FromMinutes(1),
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey))
    });
builder.Services.AddAuthorization();

var app = builder.Build();
PostgresArgumentRepository.Initialize(app.Services.GetRequiredService<NpgsqlDataSource>());
AuthRepository.Initialize(app.Services.GetRequiredService<NpgsqlDataSource>());
if (builder.Configuration["import-legacy"] is string importPath)
{
    var count = PostgresArgumentRepository.ImportLegacy(app.Services.GetRequiredService<NpgsqlDataSource>(), importPath);
    Console.WriteLine($"Imported {count} legacy arguments");
    return;
}
app.UseAuthentication();
app.UseAuthorization();
app.MapGrpcService<AuthenticationGrpcService>();
app.MapGrpcService<ArgumentGrpcService>().RequireAuthorization();
app.MapGet("/", () => "This endpoint is a gRPC server. Use a gRPC client to communicate.");
app.Run();
