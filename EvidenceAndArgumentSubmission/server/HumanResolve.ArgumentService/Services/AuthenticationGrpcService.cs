using System.Net.Mail;
using Grpc.Core;
using HumanResolve.ArgumentService.Auth;
using Npgsql;
using Proto = HumanResolve.Argument.V1;

namespace HumanResolve.ArgumentService.Services;

public sealed class AuthenticationGrpcService(AuthRepository users, IPasswordService passwords, ITokenService tokens) : Proto.AuthenticationService.AuthenticationServiceBase
{
    public override Task<Proto.AuthenticationResponse> Register(Proto.RegisterRequest request, ServerCallContext context)
    {
        var email = Email(request.Email);
        var displayName = Required(request.DisplayName, "display_name", 100);
        if (request.Password.Length is < 12 or > 256) throw Rpc(StatusCode.InvalidArgument, "password must contain between 12 and 256 characters");
        if (users.FindByEmail(email) is not null) throw Rpc(StatusCode.AlreadyExists, "an account with that email already exists");
        try { return Task.FromResult(Response(users.Create(email, displayName, passwords.Hash(request.Password)))); }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        { throw Rpc(StatusCode.AlreadyExists, "an account with that email already exists"); }
    }

    public override Task<Proto.AuthenticationResponse> Login(Proto.LoginRequest request, ServerCallContext context)
    {
        var user = users.FindByEmail(Email(request.Email));
        if (user is null || !passwords.Verify(request.Password ?? string.Empty, user.PasswordHash))
            throw Rpc(StatusCode.Unauthenticated, "email or password is incorrect");
        return Task.FromResult(Response(user));
    }

    public override Task<Proto.User> GetCurrentUser(Proto.GetCurrentUserRequest request, ServerCallContext context)
    {
        var claim = context.GetHttpContext().User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(claim, out var personId) || users.FindById(personId) is not { } user)
            throw Rpc(StatusCode.Unauthenticated, "a valid bearer token is required");
        return Task.FromResult(ToProto(user));
    }

    private Proto.AuthenticationResponse Response(AuthUser user)
    {
        var token = tokens.Create(user);
        return new() { User = ToProto(user), AccessToken = token.Value, ExpiresAt = token.ExpiresAt.ToString("O") };
    }

    private static Proto.User ToProto(AuthUser user) => new() { PersonId = user.PersonId.ToString(), Email = user.Email, DisplayName = user.DisplayName, CreatedAt = user.CreatedAt.ToString("O") };
    private static string Email(string value)
    {
        var email = Required(value, "email", 320).ToLowerInvariant();
        try { return new MailAddress(email).Address == email ? email : throw Rpc(StatusCode.InvalidArgument, "email is invalid"); }
        catch (FormatException) { throw Rpc(StatusCode.InvalidArgument, "email is invalid"); }
    }
    private static string Required(string value, string field, int maxLength)
    {
        var result = value?.Trim();
        if (string.IsNullOrEmpty(result)) throw Rpc(StatusCode.InvalidArgument, $"{field} is required");
        if (result.Length > maxLength) throw Rpc(StatusCode.InvalidArgument, $"{field} must not exceed {maxLength} characters");
        return result;
    }
    private static RpcException Rpc(StatusCode code, string detail) => new(new Status(code, detail));
}
