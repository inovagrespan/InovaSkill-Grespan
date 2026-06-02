namespace InovaSkill.Importer.Api.Contracts;

public sealed record LoginRequest(string UserOrEmail, string Password);

public sealed record RegisterUserRequest(string Name, string Email, string Password, string ConfirmPassword);

public sealed record LoginResponse(string Token);
