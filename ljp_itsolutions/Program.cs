using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using ljp_itsolutions.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<ljp_itsolutions.Services.InMemoryStore>();
builder.Services.AddRazorPages();
builder.Services.AddSession();
builder.Services.AddSingleton<ljp_itsolutions.Services.IEmailSender, ljp_itsolutions.Services.EmailSender>();
builder.Services.AddScoped<ljp_itsolutions.Services.IPhotoService, ljp_itsolutions.Services.PhotoService>();
builder.Services.Configure<ljp_itsolutions.Services.CloudinarySettings>(builder.Configuration.GetSection("CloudinarySettings"));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
    });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Configure EF Core DbContext with SQL Server
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=.\\SQLEXPRESS;Initial Catalog=ljp_itsolutions;Integrated Security=True;Trust Server Certificate=True";
builder.Services.AddDbContext<ljp_itsolutions.Data.ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, sqlServerOptionsAction: sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 15, // Increased to handle transient cloud DB issues
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
    }));

builder.Services.AddScoped<Microsoft.AspNetCore.Identity.IPasswordHasher<ljp_itsolutions.Models.User>, Microsoft.AspNetCore.Identity.PasswordHasher<ljp_itsolutions.Models.User>>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

using (var scope = app.Services.CreateScope())
{
    ljp_itsolutions.Data.DbInitializer.Initialize(app.Services);
}

app.Run();
