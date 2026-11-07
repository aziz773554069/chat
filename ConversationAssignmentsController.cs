
using System.Security.Claims;
using customerplatform.Api.Domain.Entities;
using customerplatform.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace customerplatform.Api.Controllers;

[ApiController]
[Route("api/conversations")]
[Authorize]
public class ConversationAssignmentsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ConversationAssignmentsController(AppDbContext context)
    {
        _context = context;
    }


    // ==========================================
    // POST:
    // api/conversations/2/assign
    //
    // إسناد المحادثة إلى Agent
    //
    // Admin فقط
    // ==========================================

    [HttpPost("{conversationId:int}/assign")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AssignConversation(
        int conversationId,
        [FromBody] AssignConversationRequest request)
    {
        // ------------------------------------------
        // التحقق من البيانات
        // ------------------------------------------

        if (request == null || request.UserId <= 0)
        {
            return BadRequest(new
            {
                message = "رقم الموظف غير صحيح."
            });
        }


        // ------------------------------------------
        // التحقق من وجود المحادثة
        // ------------------------------------------

        var conversation = await _context.Conversations
            .FirstOrDefaultAsync(x => x.Id == conversationId);

        if (conversation == null)
        {
            return NotFound(new
            {
                message = "المحادثة غير موجودة."
            });
        }


        // ------------------------------------------
        // التحقق من وجود Agent مفعل
        // ------------------------------------------

        var user = await _context.Users
            .FirstOrDefaultAsync(x =>
                x.Id == request.UserId &&
                x.IsActive &&
                x.Role == "Agent");

        if (user == null)
        {
            return BadRequest(new
            {
                message =
                    "المستخدم غير موجود أو غير مفعل أو ليس Agent."
            });
        }


        // ------------------------------------------
        // البحث عن إسناد نشط لنفس Agent
        // ------------------------------------------

        var activeAssignment =
            await _context.ConversationAssignments
                .FirstOrDefaultAsync(x =>
                    x.ConversationId == conversationId &&
                    x.UserId == request.UserId &&
                    x.IsActive);

        if (activeAssignment != null)
        {
            return BadRequest(new
            {
                message =
                    "المحادثة مسندة بالفعل إلى هذا الموظف.",

                assignmentId = activeAssignment.Id
            });
        }


        // ------------------------------------------
        // إنشاء إسناد جديد
        // ------------------------------------------

        var assignment = new ConversationAssignment
        {
            ConversationId = conversationId,
            UserId = request.UserId,
            AssignedAt = DateTime.UtcNow,
            UnassignedAt = null,
            IsActive = true
        };

        _context.ConversationAssignments.Add(assignment);

        await _context.SaveChangesAsync();


        // ------------------------------------------
        // النتيجة
        // ------------------------------------------

        return Ok(new
        {
            message =
                "تم إسناد المحادثة إلى الموظف بنجاح.",

            assignmentId = assignment.Id,

            conversationId = conversation.Id,

            userId = user.Id,

            userName = user.FullName,

            userEmail = user.Email,

            assignedAt = assignment.AssignedAt,

            isActive = assignment.IsActive
        });
    }


    // ==========================================
    // GET:
    // api/conversations/2/assignments
    //
    // عرض سجل إسنادات المحادثة
    //
    // Admin فقط
    // ==========================================

    [HttpGet("{conversationId:int}/assignments")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAssignments(
        int conversationId)
    {
        // ------------------------------------------
        // التأكد من وجود المحادثة
        // ------------------------------------------

        var conversationExists =
            await _context.Conversations
                .AnyAsync(x => x.Id == conversationId);

        if (!conversationExists)
        {
            return NotFound(new
            {
                message = "المحادثة غير موجودة."
            });
        }


        // ------------------------------------------
        // جلب سجل الإسنادات
        // ------------------------------------------

        var assignments =
            await _context.ConversationAssignments

                .Where(x =>
                    x.ConversationId == conversationId)

                .OrderByDescending(x => x.AssignedAt)

                .Select(x => new
                {
                    assignmentId = x.Id,

                    conversationId = x.ConversationId,

                    userId = x.UserId,

                    userName = x.User!.FullName,

                    userEmail = x.User!.Email,

                    role = x.User!.Role,

                    assignedAt = x.AssignedAt,

                    unassignedAt = x.UnassignedAt,

                    isActive = x.IsActive
                })

                .ToListAsync();


        return Ok(assignments);
    }


    // ==========================================
    // GET:
    // api/conversations/2/assignment
    //
    // معرفة الموظف المسند إليه المحادثة حاليًا
    //
    // Admin + Agent
    // ==========================================

    [HttpGet("{conversationId:int}/assignment")]
    [Authorize(Roles = "Admin,Agent")]
    public async Task<IActionResult> GetActiveAssignment(
        int conversationId)
    {
        // ------------------------------------------
        // التأكد من وجود المحادثة
        // ------------------------------------------

        var conversationExists =
            await _context.Conversations
                .AnyAsync(x => x.Id == conversationId);

        if (!conversationExists)
        {
            return NotFound(new
            {
                message = "المحادثة غير موجودة."
            });
        }


        // ------------------------------------------
        // إذا كان Agent
        // نتأكد أنه مسند إلى المحادثة
        // ------------------------------------------

        if (User.IsInRole("Agent"))
        {
            var userIdClaim =
                User.FindFirst(
                    ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdClaim, out var currentUserId))
            {
                return Unauthorized(new
                {
                    message =
                        "تعذر تحديد المستخدم الحالي."
                });
            }

            var agentHasAccess =
                await _context.ConversationAssignments
                    .AnyAsync(x =>
                        x.ConversationId == conversationId &&
                        x.UserId == currentUserId &&
                        x.IsActive);

            if (!agentHasAccess)
            {
                return Forbid();
            }
        }


        // ------------------------------------------
        // جلب الإسناد النشط
        // ------------------------------------------

        var assignment =
            await _context.ConversationAssignments

                .Where(x =>
                    x.ConversationId == conversationId &&
                    x.IsActive)

                .OrderByDescending(x => x.AssignedAt)

                .Select(x => new
                {
                    assignmentId = x.Id,

                    conversationId = x.ConversationId,

                    userId = x.UserId,

                    userName = x.User!.FullName,

                    userEmail = x.User!.Email,

                    role = x.User!.Role,

                    assignedAt = x.AssignedAt,

                    isActive = x.IsActive
                })

                .FirstOrDefaultAsync();


        // ------------------------------------------
        // لا يوجد موظف مسند حاليًا
        // ------------------------------------------

        if (assignment == null)
        {
            return Ok(new
            {
                assigned = false,

                message =
                    "لا يوجد موظف مسند إلى هذه المحادثة حاليًا."
            });
        }


        return Ok(new
        {
            assigned = true,

            assignment
        });
    }


    // ==========================================
    // GET:
    // api/conversations/my
    //
    // المحادثات المسندة إلى Agent الحالي
    //
    // Agent فقط
    // ==========================================

    [HttpGet("my")]
    [Authorize(Roles = "Agent")]
    public async Task<IActionResult> GetMyConversations()
    {
        // ------------------------------------------
        // الحصول على UserId من JWT
        // ------------------------------------------

        var userIdClaim =
            User.FindFirst(
                ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new
            {
                message =
                    "تعذر تحديد المستخدم الحالي."
            });
        }


        // ------------------------------------------
        // جلب المحادثات المسندة للـ Agent
        // ------------------------------------------

        var conversations =
            await _context.ConversationAssignments

                .Where(x =>
                    x.UserId == userId &&
                    x.IsActive)

                .Include(x => x.Conversation)
                    .ThenInclude(x => x.Customer)

                .OrderByDescending(x =>
                    x.Conversation!.LastMessageAt
                    ?? x.Conversation.StartedAt)

                .Select(x => new
                {
                    conversationId =
                        x.Conversation!.Id,

                    customerId =
                        x.Conversation.CustomerId,

                    customerName =
                        x.Conversation.Customer!.FullName,

                    customerPhone =
                        x.Conversation.Customer!.PhoneNumber,

                    customerEmail =
                        x.Conversation.Customer!.Email,

                    status =
                        x.Conversation.Status,

                    startedAt =
                        x.Conversation.StartedAt,

                    closedAt =
                        x.Conversation.ClosedAt,

                    lastMessageAt =
                        x.Conversation.LastMessageAt,

                    assignmentId =
                        x.Id,

                    assignedAt =
                        x.AssignedAt
                })

                .ToListAsync();


        return Ok(conversations);
    }


    // ==========================================
    // DELETE:
    // api/conversations/2/assign/2
    //
    // إزالة إسناد الموظف
    //
    // Admin فقط
    //
    // لا نحذف السجل من قاعدة البيانات
    // ==========================================

    [HttpDelete("{conversationId:int}/assign/{userId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RemoveAssignment(
        int conversationId,
        int userId)
    {
        // ------------------------------------------
        // البحث عن الإسناد النشط
        // ------------------------------------------

        var assignment =
            await _context.ConversationAssignments

                .FirstOrDefaultAsync(x =>
                    x.ConversationId == conversationId &&
                    x.UserId == userId &&
                    x.IsActive);


        if (assignment == null)
        {
            return NotFound(new
            {
                message =
                    "الإسناد النشط غير موجود."
            });
        }


        // ------------------------------------------
        // إنهاء الإسناد
        // ------------------------------------------

        assignment.IsActive = false;

        assignment.UnassignedAt =
            DateTime.UtcNow;


        await _context.SaveChangesAsync();


        // ------------------------------------------
        // النتيجة
        // ------------------------------------------

        return Ok(new
        {
            message =
                "تم إزالة إسناد الموظف من المحادثة بنجاح.",

            conversationId,

            userId,

            assignmentId = assignment.Id,

            assignedAt = assignment.AssignedAt,

            unassignedAt = assignment.UnassignedAt,

            isActive = assignment.IsActive
        });
    }
}


// ==========================================
// Request Model
// ==========================================

public class AssignConversationRequest
{
    public int UserId { get; set; }
}
