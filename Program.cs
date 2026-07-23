using Microsoft.AspNetCore.Authentication.Cookies;
using ModaPanelApi.models;
using CloudinaryDotNet;
using MongoDB.Driver;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var adminUsername = Environment.GetEnvironmentVariable("ADMIN_USERNAME");
var adminPassword = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");

if (string.IsNullOrWhiteSpace(adminUsername) || string.IsNullOrWhiteSpace(adminPassword))
{
    throw new Exception("ADMIN_USERNAME ve ADMIN_PASSWORD environment variable değerleri zorunludur.");
}


// ================== CLOUDINARY ==================
var cloudinaryUrl = Environment.GetEnvironmentVariable("CLOUDINARY_URL");

if (string.IsNullOrWhiteSpace(cloudinaryUrl))
{
    throw new Exception("CLOUDINARY_URL environment variable bulunamadı.");
}

var cloudinary = new Cloudinary(cloudinaryUrl);
cloudinary.Api.Secure = true;
builder.Services.AddSingleton(cloudinary);


// ================== MONGODB ==================
builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var connectionString = Environment.GetEnvironmentVariable("MONGO_URL");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new Exception("MONGO_URL environment variable bulunamadı.");
    }

    return new MongoClient(connectionString);
});

builder.Services.AddSingleton(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    var database = client.GetDatabase("modapanel");
    return database.GetCollection<Post>("posts");
});

builder.Services.AddSingleton(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    var database = client.GetDatabase("modapanel");
    return database.GetCollection<DesignCollection>("collections");
});


// ================== AUTH ==================
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/admin/login.html";
        options.Cookie.Name = "__Host-AnatolianEssenceAdmin";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.IsEssential = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("login", limiter =>
    {
        limiter.PermitLimit = 5;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
});

// ================== CORS ==================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5500",
                "http://127.0.0.1:5500",
                "https://anatolianessence.com",
                "https://www.anatolianessence.com",
                "https://moda-panel-api.onrender.com"
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

// ================== MIDDLEWARE ==================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();

app.UseCors("AllowFrontend");

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; img-src 'self' https://res.cloudinary.com data:; " +
        "media-src 'self'; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com; script-src 'self' 'unsafe-inline'; " +
        "connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";

    await next();
});

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/admin/panel.html") &&
        !(context.User.Identity?.IsAuthenticated ?? false))
    {
        context.Response.Redirect("/admin/login.html");
        return;
    }

    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

// Existing single-photo posts are kept and moved into one editable album
// the first time the album-enabled version starts.
var collections = app.Services.GetRequiredService<IMongoCollection<DesignCollection>>();
var legacyPosts = app.Services.GetRequiredService<IMongoCollection<Post>>();

if (await collections.CountDocumentsAsync(_ => true) == 0)
{
    var oldPosts = await legacyPosts.Find(_ => true).ToListAsync();

    if (oldPosts.Count > 0)
    {
        await collections.InsertOneAsync(new DesignCollection
        {
            Title = "Previous Designs",
            Description = "",
            Images = oldPosts
                .Where(x => !string.IsNullOrWhiteSpace(x.ImageUrl))
                .Select(x => new DesignImage
                {
                    ImageUrl = x.ImageUrl,
                    PublicId = ""
                })
                .ToList()
        });
    }
}

app.Run();
