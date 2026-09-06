using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using sassClaude.Data;
using sassClaude.Models;
using sassClaude.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDbContext<SassDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

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

        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS Clientes (
                Id INTEGER NOT NULL CONSTRAINT PK_Clientes PRIMARY KEY AUTOINCREMENT,
                Nome TEXT NOT NULL,
                TipoPessoa TEXT NOT NULL,
                CpfCnpj TEXT NOT NULL,
                Email TEXT NOT NULL,
                Site TEXT NOT NULL,
                Telefone TEXT NOT NULL,
                Cep TEXT NOT NULL,
                Endereco TEXT NOT NULL,
                Numero TEXT NOT NULL,
                Complemento TEXT NOT NULL,
                Bairro TEXT NOT NULL,
                Cidade TEXT NOT NULL,
                Uf TEXT NOT NULL,
                UltimaCompraData TEXT NULL,
                UltimaCompraValor TEXT NULL,
                CreatedAt TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_Clientes_CpfCnpj ON Clientes (CpfCnpj);
            """);
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS Fornecedores (
                Id INTEGER NOT NULL CONSTRAINT PK_Fornecedores PRIMARY KEY AUTOINCREMENT,
                Nome TEXT NOT NULL,
                TipoPessoa TEXT NOT NULL,
                CpfCnpj TEXT NOT NULL,
                ContatoResponsavel TEXT NOT NULL,
                Email TEXT NOT NULL,
                Site TEXT NOT NULL,
                Telefone TEXT NOT NULL,
                Cep TEXT NOT NULL,
                Endereco TEXT NOT NULL,
                Numero TEXT NOT NULL,
                Complemento TEXT NOT NULL,
                Bairro TEXT NOT NULL,
                Cidade TEXT NOT NULL,
                Uf TEXT NOT NULL,
                UltimaCompraData TEXT NULL,
                UltimaCompraValor TEXT NULL,
                CreatedAt TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_Fornecedores_CpfCnpj ON Fornecedores (CpfCnpj);
            """);

        var productColumnsAfterFornecedores = new List<string>();
        using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = "PRAGMA table_info(Products);";
            db.Database.OpenConnection();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                productColumnsAfterFornecedores.Add(reader.GetString(reader.GetOrdinal("name")));
            }
        }

        if (!productColumnsAfterFornecedores.Contains("FornecedorId"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Products ADD COLUMN FornecedorId INTEGER NULL;");
        }

        if (productColumnsAfterFornecedores.Contains("Fornecedor"))
        {
            var legacyFornecedorNames = new List<string>();
            using (var command = db.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = "SELECT DISTINCT Fornecedor FROM Products WHERE FornecedorId IS NULL AND TRIM(Fornecedor) <> '';";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    legacyFornecedorNames.Add(reader.GetString(0));
                }
            }

            foreach (var legacyName in legacyFornecedorNames)
            {
                var trimmedName = legacyName.Trim();
                var fornecedor = db.Fornecedores.FirstOrDefault(f => f.Nome == trimmedName);
                if (fornecedor is null)
                {
                    fornecedor = new Fornecedor
                    {
                        Nome = trimmedName,
                        TipoPessoa = "Jurídica",
                        CpfCnpj = $"PENDENTE-{Guid.NewGuid():N}"[..20],
                        CreatedAt = DateTime.UtcNow
                    };
                    db.Fornecedores.Add(fornecedor);
                    db.SaveChanges();
                }

                db.Database.ExecuteSqlRaw(
                    "UPDATE Products SET FornecedorId = {0} WHERE Fornecedor = {1} AND FornecedorId IS NULL;",
                    fornecedor.Id, legacyName);
            }

            db.Database.ExecuteSqlRaw("ALTER TABLE Products DROP COLUMN Fornecedor;");
        }

        var clienteColumns = new List<string>();
        using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = "PRAGMA table_info(Clientes);";
            db.Database.OpenConnection();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                clienteColumns.Add(reader.GetString(reader.GetOrdinal("name")));
            }
        }

        if (!clienteColumns.Contains("Site"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Clientes ADD COLUMN Site TEXT NOT NULL DEFAULT '';");
        }

        if (!clienteColumns.Contains("Observacoes"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Clientes ADD COLUMN Observacoes TEXT NOT NULL DEFAULT '';");
        }

        if (clienteColumns.Contains("Endereco") && !clienteColumns.Contains("CobrancaEndereco"))
        {
            db.Database.ExecuteSqlRaw("""
                ALTER TABLE Clientes ADD COLUMN CobrancaCep TEXT NOT NULL DEFAULT '';
                ALTER TABLE Clientes ADD COLUMN CobrancaEndereco TEXT NOT NULL DEFAULT '';
                ALTER TABLE Clientes ADD COLUMN CobrancaNumero TEXT NOT NULL DEFAULT '';
                ALTER TABLE Clientes ADD COLUMN CobrancaComplemento TEXT NOT NULL DEFAULT '';
                ALTER TABLE Clientes ADD COLUMN CobrancaBairro TEXT NOT NULL DEFAULT '';
                ALTER TABLE Clientes ADD COLUMN CobrancaCidade TEXT NOT NULL DEFAULT '';
                ALTER TABLE Clientes ADD COLUMN CobrancaUf TEXT NOT NULL DEFAULT '';
                ALTER TABLE Clientes ADD COLUMN EntregaCep TEXT NOT NULL DEFAULT '';
                ALTER TABLE Clientes ADD COLUMN EntregaEndereco TEXT NOT NULL DEFAULT '';
                ALTER TABLE Clientes ADD COLUMN EntregaNumero TEXT NOT NULL DEFAULT '';
                ALTER TABLE Clientes ADD COLUMN EntregaComplemento TEXT NOT NULL DEFAULT '';
                ALTER TABLE Clientes ADD COLUMN EntregaBairro TEXT NOT NULL DEFAULT '';
                ALTER TABLE Clientes ADD COLUMN EntregaCidade TEXT NOT NULL DEFAULT '';
                ALTER TABLE Clientes ADD COLUMN EntregaUf TEXT NOT NULL DEFAULT '';
                """);
            db.Database.ExecuteSqlRaw("""
                UPDATE Clientes SET
                    CobrancaCep = Cep, CobrancaEndereco = Endereco, CobrancaNumero = Numero,
                    CobrancaComplemento = Complemento, CobrancaBairro = Bairro, CobrancaCidade = Cidade, CobrancaUf = Uf,
                    EntregaCep = Cep, EntregaEndereco = Endereco, EntregaNumero = Numero,
                    EntregaComplemento = Complemento, EntregaBairro = Bairro, EntregaCidade = Cidade, EntregaUf = Uf;
                """);
            db.Database.ExecuteSqlRaw("""
                ALTER TABLE Clientes DROP COLUMN Cep;
                ALTER TABLE Clientes DROP COLUMN Endereco;
                ALTER TABLE Clientes DROP COLUMN Numero;
                ALTER TABLE Clientes DROP COLUMN Complemento;
                ALTER TABLE Clientes DROP COLUMN Bairro;
                ALTER TABLE Clientes DROP COLUMN Cidade;
                ALTER TABLE Clientes DROP COLUMN Uf;
                """);
        }

        var fornecedorColumns = new List<string>();
        using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = "PRAGMA table_info(Fornecedores);";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                fornecedorColumns.Add(reader.GetString(reader.GetOrdinal("name")));
            }
        }

        if (!fornecedorColumns.Contains("Site"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Fornecedores ADD COLUMN Site TEXT NOT NULL DEFAULT '';");
        }

        if (!fornecedorColumns.Contains("Observacoes"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Fornecedores ADD COLUMN Observacoes TEXT NOT NULL DEFAULT '';");
        }

        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS Compras (
                Id INTEGER NOT NULL CONSTRAINT PK_Compras PRIMARY KEY AUTOINCREMENT,
                FornecedorId INTEGER NULL,
                ProductId INTEGER NULL,
                Quantidade INTEGER NOT NULL,
                ValorUnitario TEXT NOT NULL,
                DataCompra TEXT NOT NULL,
                NumeroNota TEXT NOT NULL,
                FormaPagamento TEXT NOT NULL,
                Status TEXT NOT NULL,
                Observacoes TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                CONSTRAINT FK_Compras_Fornecedores_FornecedorId FOREIGN KEY (FornecedorId) REFERENCES Fornecedores (Id) ON DELETE SET NULL,
                CONSTRAINT FK_Compras_Products_ProductId FOREIGN KEY (ProductId) REFERENCES Products (Id) ON DELETE SET NULL
            );
            """);
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS Vendas (
                Id INTEGER NOT NULL CONSTRAINT PK_Vendas PRIMARY KEY AUTOINCREMENT,
                ClienteId INTEGER NULL,
                ProductId INTEGER NULL,
                Quantidade INTEGER NOT NULL,
                ValorUnitario TEXT NOT NULL,
                DataVenda TEXT NOT NULL,
                NumeroNota TEXT NOT NULL,
                FormaPagamento TEXT NOT NULL,
                Status TEXT NOT NULL,
                Observacoes TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                CONSTRAINT FK_Vendas_Clientes_ClienteId FOREIGN KEY (ClienteId) REFERENCES Clientes (Id) ON DELETE SET NULL,
                CONSTRAINT FK_Vendas_Products_ProductId FOREIGN KEY (ProductId) REFERENCES Products (Id) ON DELETE SET NULL
            );
            """);

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

        if (!loginColumns.Contains("Role"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Logins ADD COLUMN Role TEXT NOT NULL DEFAULT 'Colaborador';");
            db.Database.ExecuteSqlRaw("UPDATE Logins SET Role = 'Admin';");
        }

        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS AuditLogEntries (
                Id INTEGER NOT NULL CONSTRAINT PK_AuditLogEntries PRIMARY KEY AUTOINCREMENT,
                EntityName TEXT NOT NULL,
                EntityId INTEGER NOT NULL,
                Action TEXT NOT NULL,
                UserName TEXT NOT NULL,
                Details TEXT NOT NULL,
                Timestamp TEXT NOT NULL
            );
            """);

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
