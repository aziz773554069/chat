
using customerplatform.Api.Domain.Entities;
using customerplatform.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace customerplatform.Api.Controllers;

[ApiController]
[Route("api/incoming-messages")]
public class IncomingMessagesController : ControllerBase
{
    private readonly AppDbContext _context;

    public IncomingMessagesController(AppDbContext context)
    {
        _context = context;
    }


    // ==========================================
    // POST: api/incoming-messages
    //
    // استقبال رسالة جديدة من العميل
    // ==========================================

    [HttpPost]
    public async Task<IActionResult> ReceiveMessage(
        [FromBody] IncomingMessageRequest request)
    {
        // ==========================================
        // التحقق من البيانات
        // ==========================================

        if (request == null)
        {
            return BadRequest(new
            {
                message = "بيانات الرسالة مطلوبة."
            });
        }


        if (request.CustomerId <= 0)
        {
            return BadRequest(new
            {
                message = "رقم العميل غير صحيح."
            });
        }


        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new
            {
                message = "محتوى الرسالة مطلوب."
            });
        }


        // ==========================================
        // التحقق من نوع الرسالة
        // ==========================================

        var allowedMessageTypes = new[]
        {
            "Text",
            "Image",
            "Video",
            "Audio",
            "Document"
        };

        var messageType = string.IsNullOrWhiteSpace(request.MessageType)
            ? "Text"
            : request.MessageType.Trim();


        if (!allowedMessageTypes.Contains(
                messageType,
                StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "نوع الرسالة غير صحيح.",
                allowedTypes = allowedMessageTypes
            });
        }


        // ==========================================
        // البحث عن العميل
        // ==========================================

        var customer = await _context.Customers
            .FirstOrDefaultAsync(x =>
                x.Id == request.CustomerId);

        if (customer == null)
        {
            return NotFound(new
            {
                message = "العميل غير موجود.",
                customerId = request.CustomerId
            });
        }


        // ==========================================
        // منع الرسائل المكررة
        //
        // إذا كانت الرسالة قادمة من WhatsApp/Meta
        // فإن ExternalMessageId يجب أن يكون فريدًا.
        // ==========================================

        if (!string.IsNullOrWhiteSpace(
                request.ExternalMessageId))
        {
            var externalMessageId =
                request.ExternalMessageId.Trim();

            var existingMessage =
                await _context.Messages
                    .Include(x => x.Conversation)
                    .FirstOrDefaultAsync(x =>
                        x.ExternalMessageId ==
                        externalMessageId);

            if (existingMessage != null)
            {
                return Ok(new
                {
                    message = "تم استقبال هذه الرسالة مسبقًا.",
                    duplicate = true,

                    customerId = request.CustomerId,

                    conversationId =
                        existingMessage.ConversationId,

                    messageId =
                        existingMessage.Id,

                    senderType =
                        existingMessage.SenderType,

                    messageType =
                        existingMessage.MessageType,

                    content =
                        existingMessage.Content,

                    externalMessageId =
                        existingMessage.ExternalMessageId,

                    sentAt =
                        existingMessage.SentAt,

                    isRead =
                        existingMessage.IsRead
                });
            }
        }


        // ==========================================
        // البحث عن محادثة مفتوحة للعميل
        // ==========================================

        var conversation = await _context.Conversations
            .Where(x =>
                x.CustomerId == request.CustomerId &&
                x.Status == "Open")
            .OrderByDescending(x =>
                x.LastMessageAt ?? x.StartedAt)
            .FirstOrDefaultAsync();


        var conversationCreated = false;


        // ==========================================
        // إذا لم توجد محادثة مفتوحة
        // إنشاء محادثة جديدة
        // ==========================================

        if (conversation == null)
        {
            conversation = new Conversation
            {
                CustomerId = request.CustomerId,

                Status = "Open",

                StartedAt = DateTime.UtcNow,

                LastMessageAt = DateTime.UtcNow,

                ClosedAt = null
            };

            _context.Conversations.Add(conversation);

            await _context.SaveChangesAsync();

            conversationCreated = true;
        }


        // ==========================================
        // إنشاء رسالة العميل
        // ==========================================

        var message = new Message
        {
            Id = 0,

            ConversationId =
                conversation.Id,

            SenderType = "Customer",

            MessageType = messageType,

            Content =
                request.Content.Trim(),

            ExternalMessageId =
                string.IsNullOrWhiteSpace(
                    request.ExternalMessageId)
                    ? null
                    : request.ExternalMessageId.Trim(),

            SentAt = DateTime.UtcNow,

            IsRead = false
        };


        // ==========================================
        // إضافة الرسالة
        // ==========================================

        _context.Messages.Add(message);


        // ==========================================
        // تحديث آخر رسالة في المحادثة
        // ==========================================

        conversation.LastMessageAt =
            message.SentAt;


        // ==========================================
        // إذا كانت المحادثة مغلقة
        // نعيد فتحها
        // ==========================================

        if (conversation.Status == "Closed")
        {
            conversation.Status = "Open";

            conversation.ClosedAt = null;
        }


        // ==========================================
        // حفظ البيانات
        // ==========================================

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // ======================================
            // حماية إضافية من Race Condition
            //
            // في حالة وصول نفس الرسالة في نفس
            // اللحظة من أكثر من طلب.
            // ======================================

            if (!string.IsNullOrWhiteSpace(
                    request.ExternalMessageId))
            {
                var duplicateMessage =
                    await _context.Messages
                        .FirstOrDefaultAsync(x =>
                            x.ExternalMessageId ==
                            request.ExternalMessageId.Trim());

                if (duplicateMessage != null)
                {
                    return Ok(new
                    {
                        message =
                            "تم استقبال هذه الرسالة مسبقًا.",

                        duplicate = true,

                        customerId =
                            request.CustomerId,

                        conversationId =
                            duplicateMessage.ConversationId,

                        messageId =
                            duplicateMessage.Id,

                        senderType =
                            duplicateMessage.SenderType,

                        messageType =
                            duplicateMessage.MessageType,

                        content =
                            duplicateMessage.Content,

                        externalMessageId =
                            duplicateMessage.ExternalMessageId,

                        sentAt =
                            duplicateMessage.SentAt,

                        isRead =
                            duplicateMessage.IsRead
                    });
                }
            }

            throw;
        }


        // ==========================================
        // النتيجة
        // ==========================================

        return Ok(new
        {
            message =
                "تم استقبال رسالة العميل بنجاح.",

            duplicate = false,

            customerId =
                customer.Id,

            customerName =
                customer.FullName,

            conversationId =
                conversation.Id,

            conversationCreated,

            messageId =
                message.Id,

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
}


// ==========================================
// Request Model
// ==========================================

public class IncomingMessageRequest
{
    public int CustomerId { get; set; }

    public string Content { get; set; }
        = string.Empty;

    public string MessageType { get; set; }
        = "Text";

    public string? ExternalMessageId { get; set; }
}
