
namespace customerplatform.Api.Domain.Entities;

public class Message
{
    public int Id { get; set; }


    // ==========================================
    // المحادثة التي تنتمي إليها الرسالة
    // ==========================================

    public int ConversationId { get; set; }

    public Conversation? Conversation { get; set; }


    // ==========================================
    // مصدر الرسالة
    //
    // Customer = العميل
    // Agent    = الموظف
    // ==========================================

    public string SenderType { get; set; } = string.Empty;


    // ==========================================
    // نوع الرسالة
    //
    // Text
    // Image
    // Video
    // Audio
    // Document
    // ==========================================

    public string MessageType { get; set; } = "Text";


    // ==========================================
    // محتوى الرسالة
    // ==========================================

    public string Content { get; set; } = string.Empty;


    // ==========================================
    // المعرف الخارجي للرسالة
    //
    // يستخدم مع:
    // WhatsApp
    // Meta
    // Webhooks
    //
    // يمكن أن يكون NULL للرسائل الداخلية.
    // ==========================================

    public string? ExternalMessageId { get; set; }


    // ==========================================
    // وقت إرسال الرسالة
    // ==========================================

    public DateTime SentAt { get; set; } = DateTime.UtcNow;


    // ==========================================
    // هل قرأ الموظف الرسالة؟
    //
    // false = غير مقروءة
    // true  = مقروءة
    // ==========================================

    public bool IsRead { get; set; } = false;
}
