namespace customerplatform.Api.Domain.Entities;

public class Customer
{
    public int Id { get; set; }

    // رقم هاتف العميل في WhatsApp
    public string PhoneNumber { get; set; } = string.Empty;

    // اسم العميل
    public string? FullName { get; set; }

    // البريد الإلكتروني للعميل
    public string? Email { get; set; }

    // ملاحظات خاصة بالعميل
    public string? Notes { get; set; }

    // حالة العميل
    public bool IsActive { get; set; } = true;

    // تاريخ إضافة العميل
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // تاريخ آخر تعديل
    public DateTime? UpdatedAt { get; set; }
}