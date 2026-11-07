namespace customerplatform.Api.Domain.Entities;

public class ConversationAssignment
{
    public int Id { get; set; }

    // المحادثة التي تم تعيينها
    public int ConversationId { get; set; }

    public Conversation Conversation { get; set; } = null!;

    // الموظف الذي استلم المحادثة
    public int UserId { get; set; }

    public User User { get; set; } = null!;

    // وقت استلام الموظف للمحادثة
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    // وقت إلغاء التعيين
    public DateTime? UnassignedAt { get; set; }

    // هل الموظف ما زال مسؤولًا عن المحادثة؟
    public bool IsActive { get; set; } = true;
}