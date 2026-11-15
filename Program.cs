using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using WebApplication1.Data;
using WebApplication1.Models;
using WebApplication1.Services;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<SupportChatPresence>();
builder.Services.AddScoped<OrderNotificationService>();
var sqliteConnection = new SqliteConnectionStringBuilder(connectionString);
if (!string.IsNullOrWhiteSpace(sqliteConnection.DataSource) &&
    !string.Equals(sqliteConnection.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase) &&
    !sqliteConnection.DataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase) &&
    !Path.IsPathRooted(sqliteConnection.DataSource))
{
    sqliteConnection.DataSource = Path.GetFullPath(
        Path.Combine(builder.Environment.ContentRootPath, sqliteConnection.DataSource));
}

if (!string.IsNullOrWhiteSpace(sqliteConnection.DataSource) &&
    !string.Equals(sqliteConnection.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase) &&
    !sqliteConnection.DataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
{
    var databaseDirectory = Path.GetDirectoryName(sqliteConnection.DataSource);
    if (!string.IsNullOrEmpty(databaseDirectory))
    {
        Directory.CreateDirectory(databaseDirectory);
    }
}

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(sqliteConnection.ConnectionString));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();
builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/Orders") || context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/Orders") || context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return context.Response.WriteAsJsonAsync(new { error = "Tài khoản hiện tại không có quyền thực hiện thao tác này." });
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});
builder.Services.AddHttpClient("ai-provider", client =>
{
    client.Timeout = TimeSpan.FromSeconds(40);
});
builder.Services.AddScoped<AiProviderService>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("chat", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});

var app = builder.Build();
app.Logger.LogInformation("Using SQLite database at {DatabasePath}", sqliteConnection.DataSource);
var persistentStoragePath = app.Configuration["Storage:PersistentPath"];
if (!string.IsNullOrWhiteSpace(persistentStoragePath))
{
    Directory.CreateDirectory(Path.Combine(persistentStoragePath, "images", "uploads"));
    Directory.CreateDirectory(Path.Combine(persistentStoragePath, "images", "avatars"));
    Directory.CreateDirectory(Path.Combine(persistentStoragePath, "videos", "fashion"));
}
await DatabaseSeeder.InitializeAsync(app.Services, app.Configuration, app.Logger);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
var staticFileTypes = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
staticFileTypes.Mappings[".avif"] = "image/avif";
staticFileTypes.Mappings[".heic"] = "image/heic";
staticFileTypes.Mappings[".tiff"] = "image/tiff";
staticFileTypes.Mappings[".mp4"] = "video/mp4";
staticFileTypes.Mappings[".webm"] = "video/webm";
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = staticFileTypes });
if (!string.IsNullOrWhiteSpace(persistentStoragePath))
{
    var uploadsPath = Path.GetFullPath(Path.Combine(persistentStoragePath, "images", "uploads"));
    var avatarsPath = Path.GetFullPath(Path.Combine(persistentStoragePath, "images", "avatars"));
    var fashionVideosPath = Path.GetFullPath(Path.Combine(persistentStoragePath, "videos", "fashion"));
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(uploadsPath),
        RequestPath = "/images/uploads",
        ContentTypeProvider = staticFileTypes
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(avatarsPath),
        RequestPath = "/images/avatars",
        ContentTypeProvider = staticFileTypes
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(fashionVideosPath),
        RequestPath = "/videos/fashion",
        ContentTypeProvider = staticFileTypes
    });
}
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapControllers();

app.Run();
