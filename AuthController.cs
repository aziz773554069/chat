using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;
using customerplatform.Api.Domain.Entities;
using customerplatform.Api.DTOs;
using customerplatform.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace customerplatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public AuthController(
        AppDbContext context,
        IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    // ==========================================
    // POST: api/auth/login
    // ==========================================

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        // التحقق من البيانات
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new
            {
                message = "البريد الإلكتروني مطلوب."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new
            {
                message = "كلمة المرور مطلوبة."
            });
        }

        // البحث عن المستخدم
        var user = await _context.Users
            .FirstOrDefaultAsync(x =>
                x.Email.ToLower() == request.Email.ToLower());

        if (user == null)
        {
            return Unauthorized(new
            {
                message = "البريد الإلكتروني أو كلمة المرور غير صحيحة."
            });
        }

        // التحقق من حالة المستخدم
        if (!user.IsActive)
        {
            return Unauthorized(new
            {
                message = "هذا الحساب غير مفعل."
            });
        }

        // التحقق من كلمة المرور
        bool passwordValid;

        try
        {
            var verificationResult = _passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                request.Password);

            passwordValid = verificationResult != PasswordVerificationResult.Failed;

            // توافق مؤقت مع الحسابات التي كانت مخزنة سابقًا باستخدام BCrypt.
            if (!passwordValid && BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                passwordValid = true;
                user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
                await _context.SaveChangesAsync();
            }
        }
        catch
        {
            passwordValid = false;
        }

        if (!passwordValid)
        {
            return Unauthorized(new
            {
                message = "البريد الإلكتروني أو كلمة المرور غير صحيحة."
            });
        }

        // إنشاء JWT
        var token = GenerateToken(user);

        // إرجاع بيانات تسجيل الدخول
        var response = new LoginResponse
        {
            Token = token,
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role
        };

        return Ok(response);
    }

    // ==========================================
    // إنشاء JWT Token
    // ==========================================

    private string GenerateToken(User user)
    {
        var key = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException(
                "JWT Key is not configured.");

        var issuer = _configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException(
                "JWT Issuer is not configured.");

        var audience = _configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException(
                "JWT Audience is not configured.");

        var expirationMinutes =
            _configuration.GetValue<int>(
                "Jwt:ExpirationMinutes");

        if (expirationMinutes <= 0)
        {
            expirationMinutes = 480;
        }

        var claims = new List<Claim>
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                user.Id.ToString()),

            new Claim(
                ClaimTypes.Name,
                user.FullName),

            new Claim(
                ClaimTypes.Email,
                user.Email),

            new Claim(
                ClaimTypes.Role,
                user.Role)
        };

        var securityKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(key));

        var credentials = new SigningCredentials(
            securityKey,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(
                expirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }
}
