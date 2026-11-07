namespace customerplatform.Api.Domain.Entities;

public class User
{
    public int Id { get; set; }

    // اسم الموظف
    public string FullName { get; set; } = string.Empty;

    // البريد الإلكتروني المستخدم لتسجيل الدخول
    public string Email { get; set; } = string.Empty;

    // كلمة المرور المشفرة
    public string PasswordHash { get; set; } = string.Empty;

    // صلاحية المستخدم
    // Admin = مدير
    // Agent = موظف خدمة عملاء
    public string Role { get; set; } = "Agent";

    // هل الحساب فعال؟
    public bool IsActive { get; set; } = true;

    // تاريخ إنشاء الحساب
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}