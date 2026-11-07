
using customerplatform.Api.Data;
using customerplatform.Api.Infrastructure.Persistence;
using customerplatform.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// إنشاء WebApplication Builder
// ==========================================



// ==========================================
// Database
// ==========================================

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

// ==========================================
// JWT Authentication
// ==========================================

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "JWT Key is not configured."
    );

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException(
        "JWT Issuer is not configured."
    );

var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException(
        "JWT Audience is not configured."
    );

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            // التحقق من مفتاح التوقيع
            ValidateIssuerSigningKey = true,

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey)
            ),

            // التحقق من Issuer
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,

            // التحقق من Audience
            ValidateAudience = true,
            ValidAudience = jwtAudience,

            // التحقق من انتهاء صلاحية Token
            ValidateLifetime = true,

            // عدم السماح بأي فترة سماح
            ClockSkew = TimeSpan.Zero
        };
    });

// ==========================================
// Authorization
// ==========================================

builder.Services.AddAuthorization();

// ==========================================
// WhatsApp Service
// ==========================================
// تسجيل خدمة WhatsApp حتى يمكن استخدامها
// داخل Controllers

builder.Services.AddHttpClient<IWhatsAppService, WhatsAppService>();

// ==========================================
// Controllers
// ==========================================

builder.Services.AddControllers();

// ==========================================
// OpenAPI
// ==========================================

builder.Services.AddOpenApi();

// ==========================================
// بناء التطبيق
// ==========================================

var app = builder.Build();

// ==========================================
// Seed Initial Users
// ==========================================

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider
        .GetRequiredService<AppDbContext>();

    await UserSeeder.SeedAsync(context);
}

// ==========================================
// Development
// ==========================================

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// ==========================================
// HTTPS
// ==========================================

app.UseHttpsRedirection();

// ==========================================
// Authentication
// ==========================================

app.UseAuthentication();

// ==========================================
// Authorization
// ==========================================

app.UseAuthorization();

// ==========================================
// Controllers
// ==========================================

app.MapControllers();

// ==========================================
// Run
// ==========================================

app.Run();
