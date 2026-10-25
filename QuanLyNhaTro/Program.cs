using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaTro.Data;
using QuanLyNhaTro.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.Cookie.Name = ".QuanLyNhaTro.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();
builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Room}/{action=Index}/{id?}");

// Seed the first manager only during explicit local setup; unset SeedOnStartup after creation.
// Credentials are supplied through user secrets and are never stored in source control.
if (app.Environment.IsDevelopment()
    && app.Configuration.GetValue<bool>("InitialAdmin:SeedOnStartup"))
{
    var initialUsername = app.Configuration["InitialAdmin:Username"]?.Trim();
    var initialPassword = app.Configuration["InitialAdmin:Password"];
    var initialFullName = app.Configuration["InitialAdmin:FullName"]?.Trim();

    if (!string.IsNullOrWhiteSpace(initialUsername)
        && !string.IsNullOrWhiteSpace(initialPassword))
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (!await db.Users.AnyAsync())
        {
            var initialUser = new User
            {
                Username = initialUsername,
                FullName = string.IsNullOrWhiteSpace(initialFullName)
                    ? "Quản lý trọ"
                    : initialFullName
            };

            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
            initialUser.PasswordHash = hasher.HashPassword(initialUser, initialPassword);
            db.Users.Add(initialUser);
            await db.SaveChangesAsync();
        }
    }
}

app.Run();
