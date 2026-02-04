using AutoTestLab.Shared.Protos;
using Grpc.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using server.Data;  
using server.Models;
using server.Services.Tokens;  

public class AuthGrpcService : AutorisationService.AutorisationServiceBase
{
    private readonly ApplicationDbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthGrpcService> _logger;

    public AuthGrpcService(ApplicationDbContext context, IPasswordHasher<User> passwordHasher, ITokenService tokenService, ILogger<AuthGrpcService> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _logger = logger;
    }

    public override async Task<AuthResponse> SiginIn(SignInRequest request, ServerCallContext context)
    {
        _logger.LogInformation("->SignIn Request received for Email: {Email}", request.Email);

        // Пошук користувача в базі даних за email
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null)
        {
            // Якщо користувача не знайдено, повертаємо помилку Unauthenticated
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid email or password."));
        }

        // Верифікація пароля
        var passwordVerificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (passwordVerificationResult == PasswordVerificationResult.Failed)
        {
            // Якщо пароль невірний, повертаємо помилку Unauthenticated
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid email or password."));
        }

        //  Генерація JWT та Refresh токенів
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();

        // Оновлення refresh токена в БД та збереження
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7); // Встановлюємо термін дії,  7 днів
        await _context.SaveChangesAsync();

        // Повернення відповіді з токенами
        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow.AddHours(1)) // Термін дії access токена
        };
    }

    // Тут також мають бути реалізовані методи SignUp та RefreshToken для повної функціональності
}