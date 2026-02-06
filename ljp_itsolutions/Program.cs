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

        var store = scope.ServiceProvider.GetRequiredService<ljp_itsolutions.Services.InMemoryStore>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ljp_itsolutions.Data.ApplicationDbContext>();

        // 1. Seed Roles first
        foreach (var seededRole in store.Roles.Values)
        {
            if (!dbContext.Roles.Any(r => r.RoleName == seededRole.RoleName))
            {
                Console.WriteLine($"Seeding role: {seededRole.RoleName}");
                var role = new Role { RoleName = seededRole.RoleName };
                dbContext.Roles.Add(role);
                dbContext.SaveChanges(); 
            }
        }

        // 2. Seed Categories
        foreach (var seededCat in store.Categories.Values)
        {
            if (!dbContext.Categories.Any(c => c.CategoryName == seededCat.CategoryName))
            {
                Console.WriteLine($"Seeding category: {seededCat.CategoryName}");
                var cat = new Category { CategoryName = seededCat.CategoryName };
                dbContext.Categories.Add(cat);
                dbContext.SaveChanges();
            }
        }

        // 3. Seed Users
        var dbRoles = dbContext.Roles.ToList();
        foreach (var seeded in store.Users.Values)
        {
            if (!dbContext.Users.Any(u => u.Username == seeded.Username))
            {
                var roleInDb = dbRoles.FirstOrDefault(r => r.RoleName == seeded.Role.RoleName);
                if (roleInDb != null)
                {
                    Console.WriteLine($"Seeding user: {seeded.Username}");
                    var user = new User
                    {
                        UserID = Guid.NewGuid(),
                        Username = seeded.Username,
                        FullName = seeded.FullName,
                        Email = seeded.Email,
                        Password = seeded.Password,
                        RoleID = roleInDb.RoleID,
                        IsActive = seeded.IsActive,
                        CreatedAt = DateTime.Now
                    };
                    dbContext.Users.Add(user);
                }
            }
        }

        // 4. Seed Products
        var dbCats = dbContext.Categories.ToList();
        foreach (var p in store.Products.Values)
        {
            if (!dbContext.Products.Any(prod => prod.ProductName == p.ProductName))
            {
                var catInDb = dbCats.FirstOrDefault(c => c.CategoryName == p.Category.CategoryName);
                if (catInDb != null)
                {
                    Console.WriteLine($"Seeding product: {p.ProductName}");
                    var product = new Product
                    {
                        ProductName = p.ProductName,
                        Price = p.Price,
                        StockQuantity = p.StockQuantity,
                        CategoryID = catInDb.CategoryID,
                        IsAvailable = true
                    };
                    dbContext.Products.Add(product);
                }
            }
        }

        dbContext.SaveChanges();
        Console.WriteLine("Seeding completed successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Migration/seed error: {ex.Message}");
    }
}
app.Run();
