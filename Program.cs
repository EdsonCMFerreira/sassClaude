using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using sassClaude.Data;
using sassClaude.Models;
using sassClaude.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<SassDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "sassClaude.Session";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    });
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/api"),
    branch => branch.UseStatusCodePagesWithReExecute("/Home/PageNotFound"));

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
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
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS PasswordResetTokens (
                Id INTEGER NOT NULL CONSTRAINT PK_PasswordResetTokens PRIMARY KEY AUTOINCREMENT,
                LoginId INTEGER NOT NULL,
                TokenHash TEXT NOT NULL,
                ExpiresAt TEXT NOT NULL,
                UsedAt TEXT NULL,
                CONSTRAINT FK_PasswordResetTokens_Logins_LoginId FOREIGN KEY (LoginId) REFERENCES Logins (Id) ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_PasswordResetTokens_TokenHash ON PasswordResetTokens (TokenHash);
            """);
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS EmailVerificationTokens (
                Id INTEGER NOT NULL CONSTRAINT PK_EmailVerificationTokens PRIMARY KEY AUTOINCREMENT,
                LoginId INTEGER NOT NULL,
                TokenHash TEXT NOT NULL,
                ExpiresAt TEXT NOT NULL,
                UsedAt TEXT NULL,
                CONSTRAINT FK_EmailVerificationTokens_Logins_LoginId FOREIGN KEY (LoginId) REFERENCES Logins (Id) ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_EmailVerificationTokens_TokenHash ON EmailVerificationTokens (TokenHash);
            """);
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS WorkspaceInvites (
                Id INTEGER NOT NULL CONSTRAINT PK_WorkspaceInvites PRIMARY KEY AUTOINCREMENT,
                Email TEXT NOT NULL,
                TokenHash TEXT NOT NULL,
                InvitedByLoginId INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                ExpiresAt TEXT NOT NULL,
                AcceptedAt TEXT NULL,
                CONSTRAINT FK_WorkspaceInvites_Logins_InvitedByLoginId FOREIGN KEY (InvitedByLoginId) REFERENCES Logins (Id) ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_WorkspaceInvites_TokenHash ON WorkspaceInvites (TokenHash);
            """);
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS Invoices (
                Id INTEGER NOT NULL CONSTRAINT PK_Invoices PRIMARY KEY AUTOINCREMENT,
                LoginId INTEGER NOT NULL,
                IssuedAt TEXT NOT NULL,
                Amount TEXT NOT NULL,
                Status TEXT NOT NULL,
                Description TEXT NOT NULL,
                CONSTRAINT FK_Invoices_Logins_LoginId FOREIGN KEY (LoginId) REFERENCES Logins (Id) ON DELETE CASCADE
            );
            """);
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS Products (
                Id INTEGER NOT NULL CONSTRAINT PK_Products PRIMARY KEY AUTOINCREMENT,
                Codigo TEXT NOT NULL,
                Descricao TEXT NOT NULL,
                Validade TEXT NOT NULL,
                ValorCompra TEXT NOT NULL,
                ValorVenda TEXT NOT NULL,
                Fornecedor TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_Products_Codigo ON Products (Codigo);
            """);

        var productColumns = new List<string>();
        using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = "PRAGMA table_info(Products);";
            db.Database.OpenConnection();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                productColumns.Add(reader.GetString(reader.GetOrdinal("name")));
            }
        }

        if (productColumns.Contains("Valor") && !productColumns.Contains("ValorCompra"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Products RENAME COLUMN Valor TO ValorCompra;");
        }

        if (!productColumns.Contains("ValorVenda"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Products ADD COLUMN ValorVenda TEXT NOT NULL DEFAULT '0';");
        }

        var loginColumns = new List<string>();
        using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = "PRAGMA table_info(Logins);";
            db.Database.OpenConnection();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                loginColumns.Add(reader.GetString(reader.GetOrdinal("name")));
            }
        }

        if (!loginColumns.Contains("EmailConfirmed"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Logins ADD COLUMN EmailConfirmed INTEGER NOT NULL DEFAULT 0;");
        }

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

public partial class Program;
