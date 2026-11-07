using Microsoft.AspNetCore.Authorization;
using customerplatform.Api.Domain.Entities;
using customerplatform.Api.DTOs;
using customerplatform.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace customerplatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public UsersController(AppDbContext context)
    {
        _context = context;
    }

    // ========================================
    // GET: api/users
    // عرض جميع المستخدمين
    // Admin فقط
    // ========================================

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetUsers()
    {
        var users = await _context.Users
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.FullName,
                x.Email,
                x.Role,
                x.IsActive,
                x.CreatedAt
            })
            .ToListAsync();

        return Ok(users);
    }

    // ========================================
    // GET: api/users/1
    // عرض مستخدم واحد
    // Admin فقط
    // ========================================

    [HttpGet("{id:int}")]
    public async Task<ActionResult<object>> GetUser(int id)
    {
        var user = await _context.Users
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.FullName,
                x.Email,
                x.Role,
                x.IsActive,
                x.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return NotFound(new
            {
                message = "المستخدم غير موجود"
            });
        }

        return Ok(user);
    }

    // ========================================
    // POST: api/users
    // إنشاء مستخدم / Agent جديد
    // Admin فقط
    // ========================================

    [HttpPost]
    public async Task<ActionResult<object>> CreateUser(
        CreateUserRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var role = NormalizeRole(request.Role);

        if (role == null)
        {
            return BadRequest(new
            {
                message = "نوع المستخدم يجب أن يكون Admin أو Agent"
            });
        }

        // التحقق من البريد الإلكتروني
        var existingUser = await _context.Users
            .AnyAsync(x => x.Email.ToLower() == email);

        if (existingUser)
        {
            return Conflict(new
            {
                message = "البريد الإلكتروني مستخدم مسبقًا"
            });
        }

        // إنشاء المستخدم
        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        // تشفير كلمة المرور
        user.PasswordHash = _passwordHasher.HashPassword(
            user,
            request.Password);

        _context.Users.Add(user);

        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetUser),
            new { id = user.Id },
            new
            {
                user.Id,
                user.FullName,
                user.Email,
                user.Role,
                user.IsActive,
                user.CreatedAt
            }
        );
    }

    // ========================================
    // PUT: api/users/1
    // تعديل بيانات المستخدم
    // Admin فقط
    // ========================================

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateUser(
        int id,
        UpdateUserRequest request)
    {
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(x => x.Id == id);

        if (existingUser == null)
        {
            return NotFound(new
            {
                message = "المستخدم غير موجود"
            });
        }

        var email = request.Email.Trim().ToLowerInvariant();

        var role = NormalizeRole(request.Role);

        if (role == null)
        {
            return BadRequest(new
            {
                message = "نوع المستخدم يجب أن يكون Admin أو Agent"
            });
        }

        // التحقق من البريد الإلكتروني
        var emailExists = await _context.Users
            .AnyAsync(x =>
                x.Email.ToLower() == email &&
                x.Id != id);

        if (emailExists)
        {
            return Conflict(new
            {
                message = "البريد الإلكتروني مستخدم من قبل مستخدم آخر"
            });
        }

        // ========================================
        // حماية المدير الحالي
        // لا يمكن تحويل حسابه إلى Agent
        // ========================================

        var currentUserId = GetCurrentUserId();

        if (currentUserId == id &&
            existingUser.Role == "Admin" &&
            role != "Admin")
        {
            return BadRequest(new
            {
                message = "لا يمكنك تحويل حساب المدير الحالي إلى Agent"
            });
        }

        existingUser.FullName = request.FullName.Trim();
        existingUser.Email = email;
        existingUser.Role = role;
        existingUser.IsActive = request.IsActive;

        // تغيير كلمة المرور اختياري
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            existingUser.PasswordHash =
                _passwordHasher.HashPassword(
                    existingUser,
                    request.Password);
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "تم تحديث بيانات المستخدم بنجاح",

            user = new
            {
                existingUser.Id,
                existingUser.FullName,
                existingUser.Email,
                existingUser.Role,
                existingUser.IsActive,
                existingUser.CreatedAt
            }
        });
    }

    // ========================================
    // DELETE: api/users/1
    // تعطيل المستخدم
    // Admin فقط
    // ========================================

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(x => x.Id == id);

        if (user == null)
        {
            return NotFound(new
            {
                message = "المستخدم غير موجود"
            });
        }

        // ========================================
        // منع المدير من تعطيل نفسه
        // ========================================

        var currentUserId = GetCurrentUserId();

        if (currentUserId == id)
        {
            return BadRequest(new
            {
                message = "لا يمكنك تعطيل حسابك الحالي"
            });
        }

        // ========================================
        // منع تعطيل آخر مدير في النظام
        // ========================================

        if (user.Role == "Admin")
        {
            var activeAdminsCount = await _context.Users
                .CountAsync(x =>
                    x.Role == "Admin" &&
                    x.IsActive);

            if (activeAdminsCount <= 1)
            {
                return BadRequest(new
                {
                    message = "لا يمكن تعطيل آخر مدير فعال في النظام"
                });
            }
        }

        user.IsActive = false;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "تم تعطيل المستخدم بنجاح",
            userId = id
        });
    }

    // ========================================
    // PUT: api/users/1/activate
    // إعادة تفعيل المستخدم
    // Admin فقط
    // ========================================

    [HttpPut("{id:int}/activate")]
    public async Task<IActionResult> ActivateUser(int id)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(x => x.Id == id);

        if (user == null)
        {
            return NotFound(new
            {
                message = "المستخدم غير موجود"
            });
        }

        if (user.IsActive)
        {
            return BadRequest(new
            {
                message = "المستخدم مفعل بالفعل"
            });
        }

        user.IsActive = true;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "تم تفعيل المستخدم بنجاح",
            userId = id
        });
    }

    // ========================================
    // التحقق من نوع المستخدم
    // ========================================

    private static string? NormalizeRole(string? role)
    {
        if (string.Equals(
            role?.Trim(),
            "Admin",
            StringComparison.OrdinalIgnoreCase))
        {
            return "Admin";
        }

        if (string.Equals(
            role?.Trim(),
            "Agent",
            StringComparison.OrdinalIgnoreCase))
        {
            return "Agent";
        }

        return null;
    }

    // ========================================
    // الحصول على ID المستخدم الحالي
    // ========================================

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst(
            System.Security.Claims.ClaimTypes.NameIdentifier);

        if (claim == null)
        {
            return null;
        }

        if (int.TryParse(claim.Value, out var userId))
        {
            return userId;
        }

        return null;
    }
}