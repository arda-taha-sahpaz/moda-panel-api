using Microsoft.AspNetCore.Authentication.Cookies;
using ModaPanelApi.models;
using ModaPanelApi.Security;
using System.Text.Json;
using CloudinaryDotNet;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


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


// ================== AUTH ==================
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/admin/login.html";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.None;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();


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
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();


// ================== ADMIN INIT ==================
string adminPath = Path.Combine(app.Environment.ContentRootPath, "admin.json");

if (!System.IO.File.Exists(adminPath))
{
    var (hash, salt) = PasswordHelper.HashPassword("ElanurGece33");

    var admin = new AdminUser
    {
        Username = "admin",
        PasswordHash = hash,
        Salt = salt
    };

    var json = JsonSerializer.Serialize(admin, new JsonSerializerOptions
    {
        WriteIndented = true
    });

    System.IO.File.WriteAllText(adminPath, json);
}


// ================== MIDDLEWARE ==================
app.UseSwagger();
app.UseSwaggerUI();

app.UseRouting();

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

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

app.Run();