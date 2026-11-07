
using customerplatform.Api.Domain.Entities;
using customerplatform.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace customerplatform.Api.Data;

public static class UserSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        var passwordHasher = new PasswordHasher<User>();

        // ==========================================
        // Admin
        // ==========================================

        var admin = await context.Users
            .FirstOrDefaultAsync(x =>
                x.Email == "admin@customerplatform.com");

        if (admin == null)
        {
            admin = new User
            {
                FullName = "مدير النظام",
                Email = "admin@customerplatform.com",
                Role = "Admin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            admin.PasswordHash = passwordHasher.HashPassword(
                admin,
                "Admin@12345"
            );

            context.Users.Add(admin);
        }
        else
        {
            admin.PasswordHash = passwordHasher.HashPassword(
                admin,
                "Admin@12345"
            );

            admin.Role = "Admin";
            admin.IsActive = true;
        }


        // ==========================================
        // Agent
        // ==========================================

        var agent = await context.Users
            .FirstOrDefaultAsync(x =>
                x.Email == "agent1@customerplatform.com");

        if (agent == null)
        {
            agent = new User
            {
                FullName = "أحمد علي",
                Email = "agent1@customerplatform.com",
                Role = "Agent",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            agent.PasswordHash = passwordHasher.HashPassword(
                agent,
                "Agent@12345"
            );

            context.Users.Add(agent);
        }
        else
        {
            agent.PasswordHash = passwordHasher.HashPassword(
                agent,
                "Agent@12345"
            );

            agent.Role = "Agent";
            agent.IsActive = true;
        }


        // ==========================================
        // حفظ التغييرات
        // ==========================================

        await context.SaveChangesAsync();
    }
}
