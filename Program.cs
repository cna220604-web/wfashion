using System.Security.Claims;
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
    ?? builder.Configuration["DATABASE_URL"]
    ?? throw new InvalidOperationException("Set ConnectionStrings:DefaultConnection (or DATABASE_URL) to the database connection string.");
var configuredProvider = builder.Configuration["Database:Provider"]?.Trim().ToLowerInvariant();
var isPostgresUri = connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
    || connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);
var looksLikePostgresConnection = isPostgresUri
    || connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase)
    || connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase);
var databaseProvider = configuredProvider switch
{
    null or "" => looksLikePostgresConnection ? "postgresql" : "sqlite",
    "sqlite" when looksLikePostgresConnection => throw new InvalidOperationException("Database:Provider is set to SQLite, but the connection string is PostgreSQL. Set Database__Provider=PostgreSQL or provide a SQLite connection string."),
    "sqlite" => "sqlite",
    "postgres" or "postgresql" => "postgresql",
    var provider => throw new InvalidOperationException($"Unsupported Database:Provider '{provider}'. Use Sqlite or PostgreSQL.")
};

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient("fashion-news", client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddSingleton<SupportChatPresence>();
builder.Services.AddSingleton<ObjectMediaStorage>();
builder.Services.AddSingleton<FashionMagazineService>();
builder.Services.AddHostedService(services => services.GetRequiredService<FashionMagazineService>());
builder.Services.AddScoped<OrderNotificationService>();
builder.Services.AddScoped<PromotionService>();
Action<DbContextOptionsBuilder> configureDatabase;
if (databaseProvider == "sqlite")
{
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

    configureDatabase = options => options.UseSqlite(sqliteConnection.ConnectionString);
}
else
{
    var postgresConnectionString = connectionString;
    if (isPostgresUri)
    {
        if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var postgresUri)
            || string.IsNullOrWhiteSpace(postgresUri.Host)
            || string.IsNullOrWhiteSpace(postgresUri.AbsolutePath.Trim('/')))
        {
            throw new InvalidOperationException("The PostgreSQL URL is invalid. Copy the full connection string from your database provider.");
        }

        var credentials = postgresUri.UserInfo.Split(':', 2);
        if (credentials.Length != 2)
        {
            throw new InvalidOperationException("The PostgreSQL URL must include its username and password. Set it as a secret in Render Environment.");
        }

        var postgresBuilder = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = postgresUri.Host,
            Port = postgresUri.IsDefaultPort ? 5432 : postgresUri.Port,
            Database = Uri.UnescapeDataString(postgresUri.AbsolutePath.Trim('/')),
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = Uri.UnescapeDataString(credentials[1]),
            SslMode = Npgsql.SslMode.Require
        };
        postgresConnectionString = postgresBuilder.ConnectionString;
    }

    configureDatabase = options => options.UseNpgsql(postgresConnectionString);
}

builder.Services.AddDbContext<ApplicationDbContext>(configureDatabase);
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 1;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

var authentication = builder.Services.AddAuthentication();
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (string.IsNullOrWhiteSpace(googleClientId) != string.IsNullOrWhiteSpace(googleClientSecret))
{
    throw new InvalidOperationException("Configure both Authentication:Google:ClientId and Authentication:Google:ClientSecret to enable Google sign-in.");
}
if (!string.IsNullOrWhiteSpace(googleClientId))
{
    authentication.AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret!;
        options.Scope.Add("email");
        options.Events.OnCreatingTicket = context =>
        {
            if (context.User.TryGetProperty("email_verified", out var emailVerified)
                && emailVerified.ValueKind == System.Text.Json.JsonValueKind.True)
            {
                context.Identity?.AddClaim(new Claim("email_verified", "true"));
            }

            return Task.CompletedTask;
        };
    });
}

var facebookAppId = builder.Configuration["Authentication:Facebook:AppId"];
var facebookAppSecret = builder.Configuration["Authentication:Facebook:AppSecret"];
if (string.IsNullOrWhiteSpace(facebookAppId) != string.IsNullOrWhiteSpace(facebookAppSecret))
{
    throw new InvalidOperationException("Configure both Authentication:Facebook:AppId and Authentication:Facebook:AppSecret to enable Facebook sign-in.");
}
if (!string.IsNullOrWhiteSpace(facebookAppId))
{
    authentication.AddFacebook(options =>
    {
        options.AppId = facebookAppId;
        options.AppSecret = facebookAppSecret!;
        options.Scope.Add("email");
        options.Fields.Add("name");
        options.Fields.Add("email");
    });
}

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
app.Logger.LogInformation("Using {DatabaseProvider} database", databaseProvider);
var objectStorage = app.Services.GetRequiredService<ObjectMediaStorage>();
app.Logger.LogInformation("Durable object storage is {ObjectStorageStatus}", objectStorage.IsConfigured ? "enabled" : "not configured; media uses local storage");
if (app.Environment.IsProduction() && !objectStorage.IsConfigured && !app.Configuration.GetValue<bool>("Storage:AllowLocalVideoStorage"))
{
    app.Logger.LogWarning("Fashion video uploads are disabled because durable object storage is not configured. Configure Storage:S3 or explicitly enable local video storage on a persistent volume.");
}
var persistentStoragePath = app.Configuration["Storage:PersistentPath"];
if (!string.IsNullOrWhiteSpace(persistentStoragePath))
{
    Directory.CreateDirectory(Path.Combine(persistentStoragePath, "images", "uploads"));
    Directory.CreateDirectory(Path.Combine(persistentStoragePath, "images", "avatars"));
    if (!objectStorage.IsConfigured)
    {
        Directory.CreateDirectory(Path.Combine(persistentStoragePath, "videos", "fashion"));
    }
}
await DatabaseSeeder.InitializeAsync(app.Services, app.Configuration, app.Logger);
if (objectStorage.IsConfigured && !string.IsNullOrWhiteSpace(persistentStoragePath))
{
    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var existingVideos = await database.FashionVideos.AsNoTracking().ToListAsync(app.Lifetime.ApplicationStopping);
    var migratedVideos = 0;
    foreach (var video in existingVideos)
    {
        var localPath = Path.Combine(persistentStoragePath, "videos", "fashion", video.VideoFileName);
        if (!File.Exists(localPath) || await objectStorage.ExistsAsync($"videos/fashion/{video.VideoFileName}", app.Lifetime.ApplicationStopping))
        {
            continue;
        }

        await using var videoStream = File.OpenRead(localPath);
        var contentType = video.VideoFileName.EndsWith(".webm", StringComparison.OrdinalIgnoreCase) ? "video/webm" : "video/mp4";
        await objectStorage.UploadAsync($"videos/fashion/{video.VideoFileName}", videoStream, videoStream.Length, contentType, app.Lifetime.ApplicationStopping);
        migratedVideos++;
    }

    if (migratedVideos > 0)
    {
        app.Logger.LogInformation("Migrated {VideoCount} existing fashion videos to durable object storage", migratedVideos);
    }
}

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
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = staticFileTypes,
    OnPrepareResponse = context =>
    {
        if (context.Context.Request.Path.StartsWithSegments("/images/uploads"))
        {
            context.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
        }
    }
});
if (!string.IsNullOrWhiteSpace(persistentStoragePath))
{
    var uploadsPath = Path.GetFullPath(Path.Combine(persistentStoragePath, "images", "uploads"));
    var avatarsPath = Path.GetFullPath(Path.Combine(persistentStoragePath, "images", "avatars"));
    var fashionVideosPath = Path.GetFullPath(Path.Combine(persistentStoragePath, "videos", "fashion"));
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(uploadsPath),
        RequestPath = "/images/uploads",
        ContentTypeProvider = staticFileTypes,
        OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable"
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(avatarsPath),
        RequestPath = "/images/avatars",
        ContentTypeProvider = staticFileTypes
    });
    if (!objectStorage.IsConfigured)
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(fashionVideosPath),
            RequestPath = "/videos/fashion",
            ContentTypeProvider = staticFileTypes
        });
    }
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
