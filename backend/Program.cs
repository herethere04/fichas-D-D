using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using DnDSheetApi.Application.Interfaces;
using DnDSheetApi.Application.Services;
using DnDSheetApi.Domain.Interfaces;
using DnDSheetApi.Infrastructure.Data;
using DnDSheetApi.Infrastructure.Repositories;
using DnDSheetApi.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

// === DATABASE ===
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// === JWT AUTHENTICATION ===
var jwtKey = builder.Configuration["Jwt:Key"] ?? "DnDSheetApiSuperSecretKeyThatIsAtLeast32Chars!";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "DnDSheetApi",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "DnDSheetApp",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

// === DEPENDENCY INJECTION ===
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ISheetRepository, SheetRepository>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ISheetService, SheetService>();

// === RATE LIMITER (Singleton) ===
builder.Services.AddSingleton<RateLimitService>();

// === CONTROLLERS ===
builder.Services.AddControllers();

// === CORS (for local dev) ===
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

// === APPLY MIGRATIONS ON STARTUP + SEED ADMIN ===
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    // Seed admin user if not exists
    if (!db.Users.Any(u => u.Username == "admin"))
    {
        db.Users.Add(new DnDSheetApi.Domain.Entities.User
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("mestrejohn")
        });
        db.SaveChanges();
    }
}

// === DYNAMIC PORT BINDING (For Render/Heroku) ===
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Urls.Add($"http://*:{port}");

// === MIDDLEWARE PIPELINE ===
app.UseCors();

// Global rate limit middleware (100 requests/min/IP)
var globalRateLimiter = app.Services.GetRequiredService<RateLimitService>();
app.Use(async (context, next) =>
{
    var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    if (globalRateLimiter.IsRateLimited($"global:{ip}", 100, TimeSpan.FromMinutes(1)))
    {
        context.Response.StatusCode = 429;
        await context.Response.WriteAsJsonAsync(new { message = "Limite de requisições excedido. Tente novamente mais tarde." });
        return;
    }
    await next();
});

// Security headers
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

// Serve static files from the frontend folder
var frontendPath = Environment.GetEnvironmentVariable("FRONTEND_PATH") ?? Path.Combine(app.Environment.ContentRootPath, "..");
var absoluteFrontendPath = Path.GetFullPath(frontendPath);

if (Directory.Exists(absoluteFrontendPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(absoluteFrontendPath),
        RequestPath = ""
    });

    // Default file (login.html)
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(absoluteFrontendPath),
        DefaultFileNames = new[] { "login.html" }
    });
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Fallback: serve login.html for root
app.MapGet("/", async context =>
{
    context.Response.ContentType = "text/html";
    var loginPath = Path.Combine(absoluteFrontendPath, "login.html");
    if (File.Exists(loginPath))
        await context.Response.SendFileAsync(loginPath);
    else
        context.Response.StatusCode = 404;
});

// Periodic cleanup of rate limiter (every 5 minutes)
var cleanupTimer = new Timer(_ => globalRateLimiter.Cleanup(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

app.Run();
