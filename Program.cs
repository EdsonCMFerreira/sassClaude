using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using sassClaude.Data;
using sassClaude.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<SassDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SassDbContext>();
    try
    {
        db.Database.EnsureCreated();
        var passwordHasher = new PasswordHasher<Login>();
        var usersWithPlaintextPasswords = db.Logins
            .ToList();
        usersWithPlaintextPasswords = usersWithPlaintextPasswords
            .Where(login => !login.Password.StartsWith("AQAAAA", StringComparison.Ordinal))
            .ToList();

        foreach (var login in usersWithPlaintextPasswords)
        {
            login.Password = passwordHasher.HashPassword(login, login.Password);
        }

        if (usersWithPlaintextPasswords.Count > 0)
        {
            db.SaveChanges();
        }

        app.Logger.LogInformation("Banco de dados confirmado.");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "O banco de dados não pôde ser inicializado. Verifique a connection string.");
    }
}

app.Run();
