using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MobileOpsConnect.Data;
using MobileOpsConnect.Hubs;
using MobileOpsConnect.Models;
using MobileOpsConnect.Services;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Set QuestPDF Community license at startup
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// === CHANGE STARTS HERE ===
// I added .AddRoles<IdentityRole>() to the chain below.
builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>() // <--- THIS IS THE NEW LINE
    .AddEntityFrameworkStores<ApplicationDbContext>();
// === CHANGE ENDS HERE ===

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddMemoryCache();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, _) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString();
        }

        return ValueTask.CompletedTask;
    };

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var path = httpContext.Request.Path.Value ?? string.Empty;

        if (path.StartsWith("/Identity/Account/Login", StringComparison.OrdinalIgnoreCase))
        {
            return RateLimitPartition.GetFixedWindowLimiter("auth-login", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
        }

        if (path.StartsWith("/Notification", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/InAppNotification", StringComparison.OrdinalIgnoreCase))
        {
            return RateLimitPartition.GetSlidingWindowLimiter("notifications", _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueLimit = 0,
                AutoReplenishment = true
            });
        }

        return RateLimitPartition.GetNoLimiter("default");
    });
});

// === FIREBASE CLOUD MESSAGING SETUP ===
var firebaseKeyPath = builder.Configuration["Firebase:ServiceAccountKeyPath"] ?? "firebase-service-account.json";
if (File.Exists(firebaseKeyPath))
{
    FirebaseApp.Create(new AppOptions
    {
        Credential = GoogleCredential.FromFile(firebaseKeyPath),
    });
}
else
{
    // Log a warning but don't crash — the app still works without push notifications
    Console.WriteLine($"⚠️  Firebase service account key not found at: {firebaseKeyPath}");
    Console.WriteLine("   Push notifications will NOT work until you add the key file.");
}
builder.Services.AddScoped<INotificationService, FcmNotificationService>();
builder.Services.AddScoped<IInAppNotificationService, InAppNotificationService>();

builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
builder.Services.AddHostedService<QueuedHostedService>();

// === EXTERNAL API SERVICES ===
builder.Services.AddHttpClient<ExchangeRateService>();
builder.Services.AddHttpClient<HolidayService>();
// === END EXTERNAL API SERVICES ===

var app = builder.Build();

// === AUTO-GENERATE VAPID KEYS (for standard Web Push / iOS) ===
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate(); // ensure all migrations applied

    var vapidFile = Path.Combine(AppContext.BaseDirectory, "vapid-keys.json");
    string vapidPublic, vapidPrivate;

    if (File.Exists(vapidFile))
    {
        var json = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(vapidFile))!;
        vapidPublic = json["publicKey"];
        vapidPrivate = json["privateKey"];
        Console.WriteLine("🔑 VAPID keys loaded from vapid-keys.json");
    }
    else
    {
        var keys = WebPush.VapidHelper.GenerateVapidKeys();
        vapidPublic = keys.PublicKey;
        vapidPrivate = keys.PrivateKey;
        File.WriteAllText(vapidFile, System.Text.Json.JsonSerializer.Serialize(new { publicKey = vapidPublic, privateKey = vapidPrivate }));
        Console.WriteLine("🔑 VAPID keys generated and saved to vapid-keys.json");
    }

    app.Configuration["Vapid:PublicKey"] = vapidPublic;
    app.Configuration["Vapid:PrivateKey"] = vapidPrivate;
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapHub<InventoryHub>("/hubs/inventory");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();
// === NEW SEEDER BLOCK START ===
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        // Call the static method we just created
        await MobileOpsConnect.Data.ContextSeed.SeedRolesAsync(userManager, roleManager);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}
// === NEW SEEDER BLOCK END ===
app.Run();