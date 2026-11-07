namespace customerplatform.Api.Domain.Entities;

public class Conversation
{
    public int Id { get; set; }

    // العميل صاحب المحادثة
    public int CustomerId { get; set; }

    public Customer? Customer { get; set; } = null!;

    // حالة المحادثة
    // Open     = مفتوحة
    // Pending  = معلقة
    // Closed   = مغلقة
    public string Status { get; set; } = "Open";

    // تاريخ بداية المحادثة
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    // تاريخ إغلاق المحادثة
    public DateTime? ClosedAt { get; set; }

    // تاريخ آخر رسالة
    public DateTime? LastMessageAt { get; set; }
}