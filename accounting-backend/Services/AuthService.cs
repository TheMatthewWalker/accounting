using BCrypt.Net;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AccountingApp.Data;
using AccountingApp.Models;
using AccountingApp.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace AccountingApp.Services;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(ApplicationDbContext context, IConfiguration configuration, ILogger<AuthService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(string email, string password, string firstName, string lastName)
    {
        // Validate password strength
        if (!ValidatePasswordStrength(password))
        {
            _logger.LogWarning("Registration attempt with weak password for email: {Email}", email);
            throw new ValidationException(
                "Password must be at least 8 characters and contain uppercase, lowercase, digit, and special character");
        }

        // Check if user already exists
        var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (existingUser != null)
        {
            _logger.LogWarning("Registration attempt with duplicate email: {Email}", email);
            throw new DuplicateResourceException("User", "Email", email);
        }

        // Create new user
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            EmailConfirmed = false,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var token = GenerateJwtToken(user.Id, user.Email);

        _logger.LogInformation("User registered successfully: {UserId} ({Email})", user.Id, user.Email);

        return new AuthResponse
        {
            Success = true,
            Message = "User registered successfully",
            AccessToken = token,
            UserId = user.Id
        };
    }

    public async Task<AuthResponse> LoginAsync(string email, string password)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            _logger.LogWarning("Login attempt with non-existent email: {Email}", email);
            throw new UnauthorizedException("Invalid email or password");
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Login attempt with inactive user: {Email}", email);
            throw new UnauthorizedException("User account is not active");
        }

        if (user.PasswordHash == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            _logger.LogWarning("Login attempt with invalid password for email: {Email}", email);
            throw new UnauthorizedException("Invalid email or password");
        }

        user.LastLogin = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var token = GenerateJwtToken(user.Id, user.Email);

        _logger.LogInformation("User logged in successfully: {UserId} ({Email})", user.Id, user.Email);

        return new AuthResponse
        {
            Success = true,
            Message = "Login successful",
            AccessToken = token,
            UserId = user.Id
        };
    }

    public async Task<AuthResponse> RefreshTokenAsync(string refreshToken)
    {
        // Implement refresh token logic
        return await Task.FromResult(new AuthResponse { Success = false, Message = "Not implemented" });
    }

    public string GenerateJwtToken(Guid userId, string email)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = Encoding.ASCII.GetBytes(jwtSettings["SecretKey"] ?? "your-super-secret-key-change-in-production");
        var expirationMinutes = int.Parse(jwtSettings["ExpirationInMinutes"] ?? "60");

        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Email, email)
            }),
            Expires = DateTime.UtcNow.AddMinutes(expirationMinutes),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(secretKey),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    /// <summary>
    /// Validates password strength:
    /// - Minimum 8 characters
    /// - At least one uppercase letter
    /// - At least one lowercase letter
    /// - At least one digit
    /// - At least one special character
    /// </summary>
    private bool ValidatePasswordStrength(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8)
            return false;

        bool hasUppercase = password.Any(char.IsUpper);
        bool hasLowercase = password.Any(char.IsLower);
        bool hasDigit = password.Any(char.IsDigit);
        bool hasSpecialChar = password.Any(c => !char.IsLetterOrDigit(c));

        return hasUppercase && hasLowercase && hasDigit && hasSpecialChar;
    }
}

