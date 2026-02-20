using System;
using System.Linq;
using ljp_itsolutions.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
var connectionString = "Server=db38192.public.databaseasp.net,1433;Database=db38192;User Id=db38192;Password=bY%9@3StT?r2;Encrypt=True;TrustServerCertificate=True;Connect Timeout=60;";
services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));

var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

Console.WriteLine($"Ingredients Count: {db.Ingredients.Count()}");
foreach(var ing in db.Ingredients.ToList()) {
    Console.WriteLine($"- {ing.IngredientID}: {ing.Name}");
}
Console.WriteLine($"Recipes Count: {db.ProductRecipes.Count()}");
