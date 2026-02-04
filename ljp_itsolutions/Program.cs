using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<ljp_itsolutions.Services.InMemoryStore>();
builder.Services.AddRazorPages();
builder.Services.AddSession();
builder.Services.AddSingleton<ljp_itsolutions.Services.IEmailSender, ljp_itsolutions.Services.EmailSender>();

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
    options.UseSqlServer(connectionString));

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
    var db = scope.ServiceProvider.GetRequiredService<ljp_itsolutions.Data.ApplicationDbContext>();
    try
    {
        db.Database.Migrate();
        try
        {
            db.Database.ExecuteSqlRaw(@"IF COL_LENGTH('dbo.Users', 'Email') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD Email NVARCHAR(MAX) NULL;
END");

            db.Database.ExecuteSqlRaw(@"IF COL_LENGTH('dbo.Users', 'Password') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD Password NVARCHAR(MAX) NULL;
END");

            db.Database.ExecuteSqlRaw(@"IF COL_LENGTH('dbo.Users', 'PasswordHash') IS NOT NULL
BEGIN
    UPDATE dbo.Users SET Password = PasswordHash WHERE Password IS NULL;
    ALTER TABLE dbo.Users DROP COLUMN PasswordHash;
END");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Schema fixup error: {ex.Message}");
        }

        var store = scope.ServiceProvider.GetRequiredService<ljp_itsolutions.Services.InMemoryStore>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ljp_itsolutions.Data.ApplicationDbContext>();

        foreach (var seeded in store.Users.Values)
        {
            if (!dbContext.Users.Any(u => u.Username == seeded.Username))
            {
                dbContext.Users.Add(seeded);
                Console.WriteLine($"Seeding new user: {seeded.Username}, Password length={seeded.Password?.Length}");
            }
            else
            {
                var existing = dbContext.Users.First(u => u.Username == seeded.Username);
                existing.Email = seeded.Email;
                existing.FullName = seeded.FullName;
                existing.Role = seeded.Role;
                if (!string.IsNullOrEmpty(seeded.Password))
                {
                    existing.Password = seeded.Password;
                }
                existing.IsArchived = seeded.IsArchived;
                Console.WriteLine($"Updated existing user: {existing.Username}, Password length={existing.Password?.Length}");
            }
        }
        dbContext.SaveChanges();
        try
        {
            foreach (var u in dbContext.Users)
            {
                Console.WriteLine($"DB user: {u.Username}, Password length={u.Password?.Length}");
            }
        }
        catch { }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Migration/seed error: {ex.Message}");
    }
}
app.Run();
