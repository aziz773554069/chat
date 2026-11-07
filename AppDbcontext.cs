using customerplatform.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace customerplatform.Api.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }


    // ================================
    // Tables
    // ================================

    public DbSet<User> Users => Set<User>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<Message> Messages => Set<Message>();

    public DbSet<ConversationAssignment> ConversationAssignments
        => Set<ConversationAssignment>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);


        // ========================================
        // User
        // ========================================

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.FullName)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(x => x.Email)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(x => x.PasswordHash)
                .HasMaxLength(500)
                .IsRequired();
        });


        // ========================================
        // Customer
        // ========================================

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.PhoneNumber)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(x => x.FullName)
                .HasMaxLength(150);

            entity.Property(x => x.Email)
                .HasMaxLength(150);

            entity.Property(x => x.Notes);

            // لا نسمح بتكرار رقم العميل
            entity.HasIndex(x => x.PhoneNumber)
                .IsUnique();
        });


        // ========================================
        // Conversation
        // ========================================

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Status)
                .HasMaxLength(30)
                .IsRequired();


            // Customer 1 ---> Many Conversations

            entity.HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });


        // ========================================
        // Message
        // ========================================

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.SenderType)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(x => x.MessageType)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(x => x.Content)
                .IsRequired();

            entity.Property(x => x.ExternalMessageId)
                .HasMaxLength(200);


            // ========================================
            // منع تكرار الرسائل القادمة من الأنظمة
            // الخارجية مثل WhatsApp / Meta
            //
            // NULL مسموح للرسائل الداخلية.
            // ========================================

            entity.HasIndex(x => x.ExternalMessageId)
                .IsUnique()
                .HasFilter("[ExternalMessageId] IS NOT NULL");


            // Conversation 1 ---> Many Messages

            entity.HasOne(x => x.Conversation)
                .WithMany()
                .HasForeignKey(x => x.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });


        // ========================================
        // Conversation Assignment
        // ========================================

        modelBuilder.Entity<ConversationAssignment>(entity =>
        {
            entity.HasKey(x => x.Id);


            // Conversation 1 ---> Many Assignments

            entity.HasOne(x => x.Conversation)
                .WithMany()
                .HasForeignKey(x => x.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);


            // User 1 ---> Many Assignments

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
