
using System.Security.Claims;
using customerplatform.Api.Domain.Entities;
using customerplatform.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace customerplatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConversationsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ConversationsController(AppDbContext context)
    {
        _context = context;
    }

    // ==========================================
    // GET: api/conversations
    // Admin:
    //   يرى جميع المحادثات
    //
    // Agent:
    //   يرى المحادثات المسندة إليه فقط
    // ==========================================

    [HttpGet]
    [Authorize(Roles = "Admin,Agent")]
    public async Task<ActionResult<IEnumerable<Conversation>>> GetConversations()
    {
        var userId = GetCurrentUserId();

        if (userId == null)
        {
            return Unauthorized(new
            {
                message = "تعذر تحديد هوية المستخدم."
            });
        }

        var isAdmin = User.IsInRole("Admin");

        IQueryable<Conversation> query = _context.Conversations
            .Include(x => x.Customer);

        // إذا كان المستخدم Agent
        // نعرض له فقط المحادثات المسندة إليه
        if (!isAdmin)
        {
            query = query.Where(conversation =>
                _context.ConversationAssignments.Any(assignment =>
                    assignment.ConversationId == conversation.Id &&
                    assignment.UserId == userId.Value &&
                    assignment.IsActive));
        }

        var conversations = await query
            .OrderByDescending(x => x.LastMessageAt ?? x.StartedAt)
            .ToListAsync();

        return Ok(conversations);
    }


    // ==========================================
    // GET: api/conversations/5
    // جلب محادثة واحدة
    //
    // Admin:
    //   يستطيع مشاهدة أي محادثة
    //
    // Agent:
    //   يستطيع مشاهدة المحادثة إذا كانت مسندة إليه
    // ==========================================

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Admin,Agent")]
    public async Task<ActionResult<Conversation>> GetConversation(int id)
    {
        var conversation = await _context.Conversations
            .Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (conversation == null)
        {
            return NotFound(new
            {
                message = "المحادثة غير موجودة."
            });
        }

        var isAdmin = User.IsInRole("Admin");

        // الـ Admin يستطيع مشاهدة أي محادثة
        if (!isAdmin)
        {
            var userId = GetCurrentUserId();

            if (userId == null)
            {
                return Unauthorized(new
                {
                    message = "تعذر تحديد هوية المستخدم."
                });
            }

            var isAssigned = await _context.ConversationAssignments
                .AnyAsync(x =>
                    x.ConversationId == id &&
                    x.UserId == userId.Value &&
                    x.IsActive);

            if (!isAssigned)
            {
                return Forbid();
            }
        }

        return Ok(conversation);
    }


    // ==========================================
    // POST: api/conversations
    // إنشاء محادثة جديدة
    //
    // Admin + Agent
    // ==========================================

    [HttpPost]
    [Authorize(Roles = "Admin,Agent")]
    public async Task<ActionResult<Conversation>> CreateConversation(
        Conversation conversation)
    {
        // التأكد من وجود العميل
        var customer = await _context.Customers
            .FirstOrDefaultAsync(x => x.Id == conversation.CustomerId);

        if (customer == null)
        {
            return BadRequest(new
            {
                message = "العميل غير موجود."
            });
        }

        // منع إرسال Id من العميل
        conversation.Id = 0;

        // تحديد القيم من السيرفر
        conversation.Status = "Open";
        conversation.StartedAt = DateTime.UtcNow;
        conversation.LastMessageAt = DateTime.UtcNow;
        conversation.ClosedAt = null;

        _context.Conversations.Add(conversation);

        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetConversation),
            new { id = conversation.Id },
            conversation
        );
    }


    // ==========================================
    // PUT: api/conversations/5/close
    // إغلاق المحادثة
    //
    // Admin:
    //   يستطيع إغلاق أي محادثة
    //
    // Agent:
    //   يستطيع إغلاق المحادثة المسندة إليه فقط
    // ==========================================

    [HttpPut("{id:int}/close")]
    [Authorize(Roles = "Admin,Agent")]
    public async Task<IActionResult> CloseConversation(int id)
    {
        var conversation = await _context.Conversations
            .FirstOrDefaultAsync(x => x.Id == id);

        if (conversation == null)
        {
            return NotFound(new
            {
                message = "المحادثة غير موجودة."
            });
        }

        var permissionResult = await CheckConversationAccess(id);

        if (permissionResult != null)
        {
            return permissionResult;
        }

        // إذا كانت مغلقة بالفعل
        if (conversation.Status == "Closed")
        {
            return BadRequest(new
            {
                message = "المحادثة مغلقة بالفعل."
            });
        }

        conversation.Status = "Closed";
        conversation.ClosedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "تم إغلاق المحادثة بنجاح.",
            conversationId = conversation.Id,
            status = conversation.Status,
            closedAt = conversation.ClosedAt
        });
    }


    // ==========================================
    // PUT: api/conversations/5/reopen
    // إعادة فتح المحادثة
    //
    // Admin:
    //   يستطيع إعادة فتح أي محادثة
    //
    // Agent:
    //   يستطيع إعادة فتح المحادثة المسندة إليه فقط
    // ==========================================

    [HttpPut("{id:int}/reopen")]
    [Authorize(Roles = "Admin,Agent")]
    public async Task<IActionResult> ReopenConversation(int id)
    {
        var conversation = await _context.Conversations
            .FirstOrDefaultAsync(x => x.Id == id);

        if (conversation == null)
        {
            return NotFound(new
            {
                message = "المحادثة غير موجودة."
            });
        }

        var permissionResult = await CheckConversationAccess(id);

        if (permissionResult != null)
        {
            return permissionResult;
        }

        // إذا كانت مفتوحة بالفعل
        if (conversation.Status == "Open")
        {
            return BadRequest(new
            {
                message = "المحادثة مفتوحة بالفعل."
            });
        }

        conversation.Status = "Open";
        conversation.ClosedAt = null;
        conversation.LastMessageAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "تم إعادة فتح المحادثة بنجاح.",
            conversationId = conversation.Id,
            status = conversation.Status
        });
    }


    // ==========================================
    // GET: api/conversations/5/access
    //
    // اختبار صلاحية المستخدم الحالي
    //
    // مفيد للاختبار أثناء التطوير
    // ==========================================

    [HttpGet("{id:int}/access")]
    [Authorize(Roles = "Admin,Agent")]
    public async Task<IActionResult> CheckAccess(int id)
    {
        var conversationExists = await _context.Conversations
            .AnyAsync(x => x.Id == id);

        if (!conversationExists)
        {
            return NotFound(new
            {
                message = "المحادثة غير موجودة."
            });
        }

        var userId = GetCurrentUserId();

        if (userId == null)
        {
            return Unauthorized(new
            {
                message = "تعذر تحديد هوية المستخدم."
            });
        }

        var isAdmin = User.IsInRole("Admin");

        if (isAdmin)
        {
            return Ok(new
            {
                allowed = true,
                role = "Admin",
                userId,
                message = "المستخدم Admin ويمكنه الوصول إلى المحادثة."
            });
        }

        var isAssigned = await _context.ConversationAssignments
            .AnyAsync(x =>
                x.ConversationId == id &&
                x.UserId == userId.Value &&
                x.IsActive);

        return Ok(new
        {
            allowed = isAssigned,
            role = "Agent",
            userId,
            message = isAssigned
                ? "المحادثة مسندة إلى هذا الموظف."
                : "المحادثة غير مسندة إلى هذا الموظف."
        });
    }


    // ==========================================
    // دالة مساعدة:
    // الحصول على UserId من JWT
    // ==========================================

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userIdClaim))
        {
            return null;
        }

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return null;
        }

        return userId;
    }


    // ==========================================
    // دالة مساعدة:
    // التحقق من صلاحية المستخدم للوصول للمحادثة
    //
    // Admin -> مسموح
    // Agent -> يجب أن تكون المحادثة مسندة إليه
    // ==========================================

    private async Task<IActionResult?> CheckConversationAccess(
        int conversationId)
    {
        // Admin لديه صلاحية كاملة
        if (User.IsInRole("Admin"))
        {
            return null;
        }

        var userId = GetCurrentUserId();

        if (userId == null)
        {
            return Unauthorized(new
            {
                message = "تعذر تحديد هوية المستخدم."
            });
        }

        // التحقق من الإسناد النشط
        var isAssigned = await _context.ConversationAssignments
            .AnyAsync(x =>
                x.ConversationId == conversationId &&
                x.UserId == userId.Value &&
                x.IsActive);

        if (!isAssigned)
        {
            return Forbid();
        }

        return null;
    }
}
