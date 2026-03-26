using AutoTestLab.Shared.Protos;
using Grpc.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using server.Data;
using server.Models;
using server.Services.Tokens;

namespace server.Services.Grpc
{
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
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null)
            {
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid email or password."));
            }

            var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

            if (verificationResult == PasswordVerificationResult.Failed)
            {
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid email or password."));
            }

            return await GenerateAndSaveTokensAsync(user);
        }

        public override async Task<AuthResponse> SignUp(SignUpRequest request, ServerCallContext context)
        {
            // ensure email and username are unique
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email || u.Username == request.UserName);

            if (existingUser != null)
            {
                if (existingUser.Email == request.Email)
                    throw new RpcException(new Status(StatusCode.AlreadyExists, "User with this email already exists."));

                if (existingUser.Username == request.UserName)
                    throw new RpcException(new Status(StatusCode.AlreadyExists, "User with this username already exists."));
            }

            // create new user
            var newUser = new User
            {
                Id = Guid.NewGuid(),
                Email = request.Email,
                Username = request.UserName,
                FirstName = request.FirstName ?? "", 
                LastName = request.LastName ?? "",   
                TimeRegister = DateTime.UtcNow,      
                Role = "User"
            };

            // password hashing
            newUser.PasswordHash = _passwordHasher.HashPassword(newUser, request.Password);

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User registered: {Email}, Time: {Time}", newUser.Email, newUser.TimeRegister);

            return await GenerateAndSaveTokensAsync(newUser);
        }

        public override async Task<AuthResponse> RefreshToken(RefreshTokenRequest request, ServerCallContext context)
        {
            if (string.IsNullOrEmpty(request.RefreshToken))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Refresh token is required."));
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.RefreshToken == request.RefreshToken);

            if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
            {
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid or expired refresh token."));
            }

            return await GenerateAndSaveTokensAsync(user);
        }

        private async Task<AuthResponse> GenerateAndSaveTokensAsync(User user)
        {
            var accessToken = _tokenService.GenerateAccessToken(user);
            var refreshToken = _tokenService.GenerateRefreshToken();

            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

            await _context.SaveChangesAsync();

            return new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(60)) 
            };
        }
    }
}