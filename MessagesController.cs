
using System.Security.Claims;
using customerplatform.Api.Domain.Entities;
using customerplatform.Api.Infrastructure.Persistence;
using customerplatform.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace customerplatform.Api.Controllers;

[ApiController]
[Route("api/conversations/{conversationId:int}/messages")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWhatsAppService _whatsAppService;

    public MessagesController(
        AppDbContext context,
        IWhatsAppService whatsAppService)
    {
        _context = context;
        _whatsAppService = whatsAppService;
    }

    // ==========================================
    // GET:
    // api/conversations/{conversationId}/messages
    //
    // جلب جميع رسائل المحادثة
    //
    // Admin:
    // جميع المحادثات
    //
    // Agent:
    // المحادثات المسندة إليه فقط
    // ==========================================

    [HttpGet]
    [Authorize(Roles = "Admin,Agent")]
    public async Task<IActionResult> GetMessages(
        int conversationId)
    {
        var accessResult =
            await CheckConversationAccess(conversationId);

        if (accessResult != null)
        {
            return accessResult;
        }

        var messages = await _context.Messages
            .Where(x => x.ConversationId == conversationId)
            .OrderBy(x => x.SentAt)
            .Select(x => new
            {
                id = x.Id,
                conversationId = x.ConversationId,
                senderType = x.SenderType,
                messageType = x.MessageType,
                content = x.Content,
                externalMessageId = x.ExternalMessageId,
                sentAt = x.SentAt,
                isRead = x.IsRead
            })
            .ToListAsync();

        return Ok(messages);
    }


    // ==========================================
    // GET:
    // api/conversations/{conversationId}/messages/{id}
    //
    // جلب رسالة واحدة
    // ==========================================

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Admin,Agent")]
    public async Task<IActionResult> GetMessage(
        int conversationId,
        int id)
    {
        var accessResult =
            await CheckConversationAccess(conversationId);

        if (accessResult != null)
        {
            return accessResult;
        }

        var message = await _context.Messages
            .Where(x =>
                x.Id == id &&
                x.ConversationId == conversationId)
            .Select(x => new
            {
                id = x.Id,
                conversationId = x.ConversationId,
                senderType = x.SenderType,
                messageType = x.MessageType,
                content = x.Content,
                externalMessageId = x.ExternalMessageId,
                sentAt = x.SentAt,
                isRead = x.IsRead
            })
            .FirstOrDefaultAsync();

        if (message == null)
        {
            return NotFound(new
            {
                message = "الرسالة غير موجودة."
            });
        }

        return Ok(message);
    }


    // ==========================================
    // POST:
    // api/conversations/{conversationId}/messages
    //
    // إرسال رد من الموظف إلى العميل
    //
    // التسلسل:
    //
    // 1. التحقق من البيانات
    // 2. التحقق من صلاحية الموظف
    // 3. جلب المحادثة
    // 4. جلب العميل
    // 5. التحقق من رقم الهاتف
    // 6. إرسال الرسالة إلى WhatsApp
    // 7. حفظ الرسالة في قاعدة البيانات
    // ==========================================

    [HttpPost]
    [Authorize(Roles = "Admin,Agent")]
    public async Task<IActionResult> CreateMessage(
        int conversationId,
        [FromBody] CreateAgentMessageRequest request)
    {
        // ------------------------------------------
        // التحقق من البيانات
        // ------------------------------------------

        if (request == null)
        {
            return BadRequest(new
            {
                message = "بيانات الرسالة مطلوبة."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new
            {
                message = "محتوى الرسالة مطلوب."
            });
        }

        var content = request.Content.Trim();

        if (content.Length > 5000)
        {
            return BadRequest(new
            {
                message = "محتوى الرسالة لا يمكن أن يتجاوز 5000 حرف."
            });
        }

        // ------------------------------------------
        // نوع الرسالة
        // ------------------------------------------

        var messageType = string.IsNullOrWhiteSpace(
            request.MessageType)
            ? "Text"
            : request.MessageType.Trim();

        if (!messageType.Equals(
                "Text",
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message =
                    "إرسال الصور والفيديو والصوت والمستندات عبر WhatsApp غير مضاف حاليًا. استخدم Text."
            });
        }

        messageType = "Text";

        // ------------------------------------------
        // التحقق من صلاحية المستخدم
        // ------------------------------------------

        var accessResult =
            await CheckConversationAccess(conversationId);

        if (accessResult != null)
        {
            return accessResult;
        }

        // ------------------------------------------
        // الحصول على UserId من JWT
        // ------------------------------------------

        var userIdClaim =
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new
            {
                message = "تعذر تحديد المستخدم الحالي."
            });
        }

        // ------------------------------------------
        // جلب المستخدم الحالي
        // ------------------------------------------

        var user = await _context.Users
            .FirstOrDefaultAsync(x =>
                x.Id == userId &&
                x.IsActive);

        if (user == null)
        {
            return Unauthorized(new
            {
                message = "المستخدم غير موجود أو غير مفعل."
            });
        }

        // ------------------------------------------
        // التحقق من الدور
        // ------------------------------------------

        if (user.Role != "Admin" &&
            user.Role != "Agent")
        {
            return Forbid();
        }

        // ------------------------------------------
        // جلب المحادثة
        // ------------------------------------------

        var conversation = await _context.Conversations
            .FirstOrDefaultAsync(x =>
                x.Id == conversationId);

        if (conversation == null)
        {
            return NotFound(new
            {
                message = "المحادثة غير موجودة."
            });
        }

        // ------------------------------------------
        // منع الإرسال في محادثة مغلقة
        // ------------------------------------------

        if (conversation.Status == "Closed")
        {
            return BadRequest(new
            {
                message =
                    "لا يمكن إرسال رسالة في محادثة مغلقة. يجب إعادة فتح المحادثة أولاً."
            });
        }

        // ------------------------------------------
        // جلب العميل
        // ------------------------------------------

        var customer = await _context.Customers
            .FirstOrDefaultAsync(x =>
                x.Id == conversation.CustomerId);

        if (customer == null)
        {
            return BadRequest(new
            {
                message = "العميل المرتبط بالمحادثة غير موجود."
            });
        }

        // ------------------------------------------
        // التحقق من رقم هاتف العميل
        // ------------------------------------------

        if (string.IsNullOrWhiteSpace(customer.PhoneNumber))
        {
            return BadRequest(new
            {
                message =
                    "لا يوجد رقم WhatsApp مسجل لهذا العميل."
            });
        }

        var phoneNumber =
            customer.PhoneNumber.Trim();

        // ------------------------------------------
        // إرسال الرسالة إلى WhatsApp
        // ------------------------------------------

        var whatsAppResult =
            await _whatsAppService.SendTextMessageAsync(
                phoneNumber,
                content);

        // ------------------------------------------
        // إذا فشل إرسال WhatsApp
        // ------------------------------------------

        if (!whatsAppResult.Success)
        {
            return BadRequest(new
            {
                message =
                    whatsAppResult.ErrorMessage
                    ?? "فشل إرسال الرسالة إلى WhatsApp.",

                sent = false
            });
        }

        // ------------------------------------------
        // إنشاء الرسالة في قاعدة البيانات
        // ------------------------------------------

        var sentAt = DateTime.UtcNow;

        var message = new Message
        {
            Id = 0,

            ConversationId = conversationId,

            // يحددها النظام
            SenderType = "Agent",

            MessageType = messageType,

            Content = content,

            // ID الخاص برسالة WhatsApp
            ExternalMessageId =
                whatsAppResult.ExternalMessageId,

            SentAt = sentAt,

            // رسالة الموظف تعتبر مقروءة
            IsRead = true
        };

        // ------------------------------------------
        // إضافة الرسالة
        // ------------------------------------------

        _context.Messages.Add(message);

        // ------------------------------------------
        // تحديث آخر رسالة في المحادثة
        // ------------------------------------------

        conversation.LastMessageAt = sentAt;

        // ------------------------------------------
        // حفظ البيانات
        // ------------------------------------------

        await _context.SaveChangesAsync();

        // ------------------------------------------
        // إرجاع النتيجة
        // ------------------------------------------

        return CreatedAtAction(
            nameof(GetMessage),
            new
            {
                conversationId = conversation.Id,
                id = message.Id
            },
            new
            {
                message =
                    "تم إرسال الرد إلى WhatsApp بنجاح.",

                sent = true,

                conversationId =
                    conversation.Id,

                messageId =
                    message.Id,

                senderId =
                    user.Id,

                senderName =
                    user.FullName,

                senderRole =
                    user.Role,

                senderType =
                    message.SenderType,

                messageType =
                    message.MessageType,

                content =
                    message.Content,

                externalMessageId =
                    message.ExternalMessageId,

                sentAt =
                    message.SentAt,

                isRead =
                    message.IsRead
            });
    }


    // ==========================================
    // PUT:
    // api/conversations/{conversationId}/messages/{id}/read
    //
    // تحديد الرسالة كمقروءة
    // ==========================================

    [HttpPut("{id:int}/read")]
    [Authorize(Roles = "Admin,Agent")]
    public async Task<IActionResult> MarkAsRead(
        int conversationId,
        int id)
    {
        var accessResult =
            await CheckConversationAccess(conversationId);

        if (accessResult != null)
        {
            return accessResult;
        }

        var message = await _context.Messages
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.ConversationId == conversationId);

        if (message == null)
        {
            return NotFound(new
            {
                message = "الرسالة غير موجودة."
            });
        }

        message.IsRead = true;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "تم تحديد الرسالة كمقروءة.",
            messageId = message.Id,
            conversationId
        });
    }


    // ==========================================
    // التحقق من صلاحية المستخدم على المحادثة
    // ==========================================

    private async Task<ActionResult?> CheckConversationAccess(
        int conversationId)
    {
        // ------------------------------------------
        // التأكد من وجود المحادثة
        // ------------------------------------------

        var conversationExists =
            await _context.Conversations
                .AnyAsync(x =>
                    x.Id == conversationId);

        if (!conversationExists)
        {
            return NotFound(new
            {
                message = "المحادثة غير موجودة."
            });
        }

        // ------------------------------------------
        // الحصول على UserId من JWT
        // ------------------------------------------

        var userIdClaim =
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new
            {
                message = "تعذر تحديد المستخدم الحالي."
            });
        }

        // ------------------------------------------
        // Admin:
        // يستطيع الوصول إلى جميع المحادثات
        // ------------------------------------------

        if (User.IsInRole("Admin"))
        {
            return null;
        }

        // ------------------------------------------
        // Agent:
        // يجب أن يكون Agent
        // ------------------------------------------

        if (!User.IsInRole("Agent"))
        {
            return Forbid();
        }

        // ------------------------------------------
        // Agent:
        // يجب أن تكون المحادثة مسندة إليه حاليًا
        // ------------------------------------------

        var isAssigned =
            await _context.ConversationAssignments
                .AnyAsync(x =>
                    x.ConversationId == conversationId &&
                    x.UserId == userId &&
                    x.IsActive &&
                    x.UnassignedAt == null);

        if (!isAssigned)
        {
            return Forbid();
        }

        return null;
    }
}


// ==========================================
// Request Model
// ==========================================

public class CreateAgentMessageRequest
{
    public string Content { get; set; } = string.Empty;

    public string MessageType { get; set; } = "Text";
}
