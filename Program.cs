using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using Saas.Data;
using Saas.Models;
using Saas.Services;

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
        options.Cookie.Name = "Saas.Session";
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
builder.Services.Configure<SuperAdminOptions>(builder.Configuration.GetSection("SuperAdmin"));
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<ICurrentTenantAccessor, HttpContextCurrentTenantAccessor>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SuperAdmin", policy => policy.RequireClaim(TenantClaimTypes.IsSuperAdmin, "true"));
});

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

        List<string> GetColumns(string tableName)
        {
            var columns = new List<string>();
            using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = $"PRAGMA table_info({tableName});";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                columns.Add(reader.GetString(reader.GetOrdinal("name")));
            }

            return columns;
        }

        // Multi-tenancy: cada empresa é uma linha em Empresas; todo dado de negócio já
        // existente é atribuído à "Empresa Padrão" para preservar o histórico gravado.
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS Empresas (
                Id INTEGER NOT NULL CONSTRAINT PK_Empresas PRIMARY KEY AUTOINCREMENT,
                Nome TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );
            """);

        // GetColumns() e os demais comandos ADO abaixo assumem uma conexão já aberta;
        // OpenConnection() precisa vir antes da primeira chamada a GetColumns().
        db.Database.OpenConnection();

        // Em um banco novo, EnsureCreated() já cria Empresas com a coluna Plano (NOT NULL,
        // sem default no schema); por isso essa coluna precisa existir antes do INSERT abaixo,
        // que já passa a fornecer o valor explicitamente.
        var empresaColumns = GetColumns("Empresas");
        if (!empresaColumns.Contains("Plano"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Empresas ADD COLUMN Plano TEXT NOT NULL DEFAULT 'Starter';");
        }

        db.Database.ExecuteSqlRaw("""
            INSERT INTO Empresas (Nome, Plano, CreatedAt)
            SELECT 'Empresa Padrão', 'Starter', CURRENT_TIMESTAMP
            WHERE NOT EXISTS (SELECT 1 FROM Empresas);
            """);

        long defaultEmpresaId;
        using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = "SELECT Id FROM Empresas ORDER BY Id LIMIT 1;";
            defaultEmpresaId = Convert.ToInt64(command.ExecuteScalar());
        }

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
                EmpresaId INTEGER NOT NULL,
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
                EmpresaId INTEGER NOT NULL,
                LoginId INTEGER NOT NULL,
                IssuedAt TEXT NOT NULL,
                Amount TEXT NOT NULL,
                Status TEXT NOT NULL,
                Description TEXT NOT NULL,
                CONSTRAINT FK_Invoices_Logins_LoginId FOREIGN KEY (LoginId) REFERENCES Logins (Id) ON DELETE CASCADE
            );
            """);
        // "Products"/"Product" foram renomeados para "Produtos"/"Produto" (terminologia do
        // negócio); o rename preserva todo o histórico já gravado.
        var tableNamesForProdutos = new List<string>();
        using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";
            db.Database.OpenConnection();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                tableNamesForProdutos.Add(reader.GetString(0));
            }
        }

        if (tableNamesForProdutos.Contains("Products") && !tableNamesForProdutos.Contains("Produtos"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Products RENAME TO Produtos;");
        }

        if (tableNamesForProdutos.Contains("PedidoItens"))
        {
            var pedidoItensColumnsForRename = new List<string>();
            using (var command = db.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = "PRAGMA table_info(PedidoItens);";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    pedidoItensColumnsForRename.Add(reader.GetString(reader.GetOrdinal("name")));
                }
            }

            if (pedidoItensColumnsForRename.Contains("ProductId") && !pedidoItensColumnsForRename.Contains("ProdutoId"))
            {
                db.Database.ExecuteSqlRaw("ALTER TABLE PedidoItens RENAME COLUMN ProductId TO ProdutoId;");
            }
        }

        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS Produtos (
                Id INTEGER NOT NULL CONSTRAINT PK_Produtos PRIMARY KEY AUTOINCREMENT,
                EmpresaId INTEGER NOT NULL,
                Codigo TEXT NOT NULL,
                Descricao TEXT NOT NULL,
                Validade TEXT NOT NULL,
                ValorCompra TEXT NOT NULL,
                ValorVenda TEXT NOT NULL,
                Fornecedor TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_Produtos_Codigo ON Produtos (Codigo);
            """);

        var productColumns = new List<string>();
        using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = "PRAGMA table_info(Produtos);";
            db.Database.OpenConnection();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                productColumns.Add(reader.GetString(reader.GetOrdinal("name")));
            }
        }

        if (productColumns.Contains("Valor") && !productColumns.Contains("ValorCompra"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Produtos RENAME COLUMN Valor TO ValorCompra;");
        }

        if (!productColumns.Contains("ValorVenda"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Produtos ADD COLUMN ValorVenda TEXT NOT NULL DEFAULT '0';");
        }

        if (!productColumns.Contains("EmpresaId"))
        {
            db.Database.ExecuteSqlRaw($"ALTER TABLE Produtos ADD COLUMN EmpresaId INTEGER NOT NULL DEFAULT {defaultEmpresaId};");
        }

        // "IX_Products_Codigo" é o nome original do índice, de antes do rename da tabela
        // "Products" para "Produtos" — ALTER TABLE RENAME TO não renomeia índices, então
        // esse nome antigo sobrevive e precisa ser removido explicitamente.
        db.Database.ExecuteSqlRaw("DROP INDEX IF EXISTS IX_Products_Codigo;");
        db.Database.ExecuteSqlRaw("DROP INDEX IF EXISTS IX_Produtos_Codigo;");
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_Produtos_EmpresaId_Codigo ON Produtos (EmpresaId, Codigo);");

        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS Clientes (
                Id INTEGER NOT NULL CONSTRAINT PK_Clientes PRIMARY KEY AUTOINCREMENT,
                EmpresaId INTEGER NOT NULL,
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
                EmpresaId INTEGER NOT NULL,
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
            command.CommandText = "PRAGMA table_info(Produtos);";
            db.Database.OpenConnection();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                productColumnsAfterFornecedores.Add(reader.GetString(reader.GetOrdinal("name")));
            }
        }

        if (!productColumnsAfterFornecedores.Contains("FornecedorId"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Produtos ADD COLUMN FornecedorId INTEGER NULL;");
        }

        if (productColumnsAfterFornecedores.Contains("Fornecedor"))
        {
            var legacyFornecedorNames = new List<string>();
            using (var command = db.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = "SELECT DISTINCT Fornecedor FROM Produtos WHERE FornecedorId IS NULL AND TRIM(Fornecedor) <> '';";
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
                        EmpresaId = (int)defaultEmpresaId,
                        Nome = trimmedName,
                        TipoPessoa = "Jurídica",
                        CpfCnpj = $"PENDENTE-{Guid.NewGuid():N}"[..20],
                        CreatedAt = DateTime.UtcNow
                    };
                    db.Fornecedores.Add(fornecedor);
                    db.SaveChanges();
                }

                db.Database.ExecuteSqlRaw(
                    "UPDATE Produtos SET FornecedorId = {0} WHERE Fornecedor = {1} AND FornecedorId IS NULL;",
                    fornecedor.Id, legacyName);
            }

            db.Database.ExecuteSqlRaw("ALTER TABLE Produtos DROP COLUMN Fornecedor;");
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

        if (!clienteColumns.Contains("EmpresaId"))
        {
            db.Database.ExecuteSqlRaw($"ALTER TABLE Clientes ADD COLUMN EmpresaId INTEGER NOT NULL DEFAULT {defaultEmpresaId};");
        }

        db.Database.ExecuteSqlRaw("DROP INDEX IF EXISTS IX_Clientes_CpfCnpj;");
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_Clientes_EmpresaId_CpfCnpj ON Clientes (EmpresaId, CpfCnpj);");

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

        if (!fornecedorColumns.Contains("EmpresaId"))
        {
            db.Database.ExecuteSqlRaw($"ALTER TABLE Fornecedores ADD COLUMN EmpresaId INTEGER NOT NULL DEFAULT {defaultEmpresaId};");
        }

        db.Database.ExecuteSqlRaw("DROP INDEX IF EXISTS IX_Fornecedores_CpfCnpj;");
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_Fornecedores_EmpresaId_CpfCnpj ON Fornecedores (EmpresaId, CpfCnpj);");

        if (!fornecedorColumns.Contains("Site"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Fornecedores ADD COLUMN Site TEXT NOT NULL DEFAULT '';");
        }

        if (!fornecedorColumns.Contains("Observacoes"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Fornecedores ADD COLUMN Observacoes TEXT NOT NULL DEFAULT '';");
        }

        if (fornecedorColumns.Contains("UltimaCompraData"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Fornecedores DROP COLUMN UltimaCompraData;");
        }

        if (fornecedorColumns.Contains("UltimaCompraValor"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Fornecedores DROP COLUMN UltimaCompraValor;");
        }

        var tableNames = new List<string>();
        using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                tableNames.Add(reader.GetString(0));
            }
        }

        // "Vendas"/"VendaItens" foram renomeadas para "Pedidos"/"PedidoItens" (terminologia do
        // negócio); o rename preserva todo o histórico já gravado nessas tabelas.
        if (tableNames.Contains("Vendas") && !tableNames.Contains("Pedidos"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Vendas RENAME TO Pedidos;");
        }

        if (tableNames.Contains("VendaItens") && !tableNames.Contains("PedidoItens"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE VendaItens RENAME TO PedidoItens;");
            db.Database.ExecuteSqlRaw("ALTER TABLE PedidoItens RENAME COLUMN VendaId TO PedidoId;");
        }

        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS Pedidos (
                Id INTEGER NOT NULL CONSTRAINT PK_Pedidos PRIMARY KEY AUTOINCREMENT,
                EmpresaId INTEGER NOT NULL,
                NumeroPedido INTEGER NOT NULL DEFAULT 0,
                ClienteId INTEGER NULL,
                DataPedido TEXT NOT NULL,
                NumeroNota TEXT NOT NULL,
                FormaPagamento TEXT NOT NULL,
                Status TEXT NOT NULL,
                PercentualDesconto TEXT NOT NULL DEFAULT '0',
                Observacoes TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                CONSTRAINT FK_Pedidos_Clientes_ClienteId FOREIGN KEY (ClienteId) REFERENCES Clientes (Id) ON DELETE SET NULL
            );
            """);

        var pedidoColumns = new List<string>();
        using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = "PRAGMA table_info(Pedidos);";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                pedidoColumns.Add(reader.GetString(reader.GetOrdinal("name")));
            }
        }

        if (!pedidoColumns.Contains("PercentualDesconto"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Pedidos ADD COLUMN PercentualDesconto TEXT NOT NULL DEFAULT '0';");
        }

        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS PedidoItens (
                Id INTEGER NOT NULL CONSTRAINT PK_PedidoItens PRIMARY KEY AUTOINCREMENT,
                EmpresaId INTEGER NOT NULL,
                PedidoId INTEGER NOT NULL,
                ProdutoId INTEGER NULL,
                Quantidade INTEGER NOT NULL,
                ValorUnitario TEXT NOT NULL,
                CONSTRAINT FK_PedidoItens_Pedidos_PedidoId FOREIGN KEY (PedidoId) REFERENCES Pedidos (Id) ON DELETE CASCADE,
                CONSTRAINT FK_PedidoItens_Produtos_ProdutoId FOREIGN KEY (ProdutoId) REFERENCES Produtos (Id) ON DELETE SET NULL
            );
            """);

        if (pedidoColumns.Contains("ProductId"))
        {
            // Os itens são copiados para uma tabela temporária (sem FK) antes de recriar Pedidos:
            // como o Microsoft.Data.Sqlite habilita PRAGMA foreign_keys, um DROP TABLE Pedidos
            // dispara a ação ON DELETE CASCADE de PedidoItens.PedidoId, apagando os itens já inseridos.
            db.Database.ExecuteSqlRaw("""
                CREATE TEMP TABLE PedidoItensStaging AS
                SELECT Id AS PedidoId, ProductId AS ProdutoId, Quantidade, ValorUnitario FROM Pedidos;
                """);

            // SQLite recusa DROP COLUMN em coluna usada numa FK da própria tabela (ProductId),
            // por isso a tabela precisa ser recriada em vez de alterada coluna a coluna.
            db.Database.ExecuteSqlRaw("""
                CREATE TABLE PedidosNovo (
                    Id INTEGER NOT NULL CONSTRAINT PK_Pedidos PRIMARY KEY AUTOINCREMENT,
                    ClienteId INTEGER NULL,
                    DataVenda TEXT NOT NULL,
                    NumeroNota TEXT NOT NULL,
                    FormaPagamento TEXT NOT NULL,
                    Status TEXT NOT NULL,
                    PercentualDesconto TEXT NOT NULL DEFAULT '0',
                    Observacoes TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    CONSTRAINT FK_Pedidos_Clientes_ClienteId FOREIGN KEY (ClienteId) REFERENCES Clientes (Id) ON DELETE SET NULL
                );
                INSERT INTO PedidosNovo (Id, ClienteId, DataVenda, NumeroNota, FormaPagamento, Status, PercentualDesconto, Observacoes, CreatedAt)
                SELECT Id, ClienteId, DataVenda, NumeroNota, FormaPagamento, Status, PercentualDesconto, Observacoes, CreatedAt FROM Pedidos;
                DROP TABLE Pedidos;
                ALTER TABLE PedidosNovo RENAME TO Pedidos;
                """);

            db.Database.ExecuteSqlRaw("""
                INSERT INTO PedidoItens (PedidoId, ProdutoId, Quantidade, ValorUnitario)
                SELECT PedidoId, ProdutoId, Quantidade, ValorUnitario FROM PedidoItensStaging;
                """);
            db.Database.ExecuteSqlRaw("DROP TABLE PedidoItensStaging;");

            pedidoColumns = new List<string>();
            using (var command = db.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = "PRAGMA table_info(Pedidos);";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    pedidoColumns.Add(reader.GetString(reader.GetOrdinal("name")));
                }
            }
        }

        if (pedidoColumns.Contains("DataVenda") && !pedidoColumns.Contains("DataPedido"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Pedidos RENAME COLUMN DataVenda TO DataPedido;");
        }

        if (!pedidoColumns.Contains("NumeroPedido"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Pedidos ADD COLUMN NumeroPedido INTEGER NOT NULL DEFAULT 0;");
            db.Database.ExecuteSqlRaw("""
                UPDATE Pedidos SET NumeroPedido = (
                    SELECT COUNT(*) FROM Pedidos AS p2 WHERE p2.Id <= Pedidos.Id
                );
                """);
        }

        if (!pedidoColumns.Contains("EmpresaId"))
        {
            db.Database.ExecuteSqlRaw($"ALTER TABLE Pedidos ADD COLUMN EmpresaId INTEGER NOT NULL DEFAULT {defaultEmpresaId};");
        }

        db.Database.ExecuteSqlRaw("DROP INDEX IF EXISTS IX_Pedidos_NumeroPedido;");
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_Pedidos_EmpresaId_NumeroPedido ON Pedidos (EmpresaId, NumeroPedido);");

        var pedidoItensColumns = GetColumns("PedidoItens");
        if (!pedidoItensColumns.Contains("EmpresaId"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE PedidoItens ADD COLUMN EmpresaId INTEGER NOT NULL DEFAULT 0;");
            db.Database.ExecuteSqlRaw("""
                UPDATE PedidoItens SET EmpresaId = (SELECT EmpresaId FROM Pedidos WHERE Pedidos.Id = PedidoItens.PedidoId)
                WHERE EmpresaId = 0;
                """);
        }

        var productColumnsForSaldo = new List<string>();
        using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = "PRAGMA table_info(Produtos);";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                productColumnsForSaldo.Add(reader.GetString(reader.GetOrdinal("name")));
            }
        }

        if (!productColumnsForSaldo.Contains("Saldo") && !productColumnsForSaldo.Contains("Quantidade"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Produtos ADD COLUMN Saldo INTEGER NOT NULL DEFAULT 0;");
            db.Database.ExecuteSqlRaw("""
                UPDATE Produtos SET Saldo = -COALESCE((SELECT SUM(Quantidade) FROM PedidoItens WHERE PedidoItens.ProdutoId = Produtos.Id), 0);
                """);
        }

        // "Saldo" (contador incrementado/decrementado a cada pedido) foi substituído por
        // "Quantidade" (quantidade cadastrada do produto); o saldo disponível passa a ser
        // calculado sob demanda como Quantidade - soma de itens de pedidos.
        var productColumnsForQuantidade = new List<string>();
        using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = "PRAGMA table_info(Produtos);";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                productColumnsForQuantidade.Add(reader.GetString(reader.GetOrdinal("name")));
            }
        }

        if (!productColumnsForQuantidade.Contains("Quantidade"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Produtos ADD COLUMN Quantidade INTEGER NOT NULL DEFAULT 0;");
            db.Database.ExecuteSqlRaw("""
                UPDATE Produtos SET Quantidade = Saldo + COALESCE((SELECT SUM(Quantidade) FROM PedidoItens WHERE PedidoItens.ProdutoId = Produtos.Id), 0);
                """);
        }

        if (productColumnsForQuantidade.Contains("Saldo"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Produtos DROP COLUMN Saldo;");
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

        if (!loginColumns.Contains("Role"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Logins ADD COLUMN Role TEXT NOT NULL DEFAULT 'Colaborador';");
            db.Database.ExecuteSqlRaw("UPDATE Logins SET Role = 'Admin';");
        }

        if (!loginColumns.Contains("EmpresaId"))
        {
            db.Database.ExecuteSqlRaw($"ALTER TABLE Logins ADD COLUMN EmpresaId INTEGER NOT NULL DEFAULT {defaultEmpresaId};");
        }

        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_Logins_Email ON Logins (Email);");
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_Logins_EmpresaId_Username ON Logins (EmpresaId, Username);");

        if (!loginColumns.Contains("IsSuperAdmin"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Logins ADD COLUMN IsSuperAdmin INTEGER NOT NULL DEFAULT 0;");
        }

        var superAdminOptions = scope.ServiceProvider.GetRequiredService<IOptions<SuperAdminOptions>>().Value;
        var superAdminEmail = superAdminOptions.Email.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(superAdminEmail))
        {
            var superAdminLogin = db.Logins.IgnoreQueryFilters().FirstOrDefault(login => login.Email == superAdminEmail);
            if (superAdminLogin is not null && !superAdminLogin.IsSuperAdmin)
            {
                superAdminLogin.IsSuperAdmin = true;
                db.SaveChanges();
            }
        }

        var workspaceInviteColumns = GetColumns("WorkspaceInvites");
        if (!workspaceInviteColumns.Contains("EmpresaId"))
        {
            db.Database.ExecuteSqlRaw($"ALTER TABLE WorkspaceInvites ADD COLUMN EmpresaId INTEGER NOT NULL DEFAULT {defaultEmpresaId};");
        }

        var invoiceColumns = GetColumns("Invoices");
        if (!invoiceColumns.Contains("EmpresaId"))
        {
            db.Database.ExecuteSqlRaw($"ALTER TABLE Invoices ADD COLUMN EmpresaId INTEGER NOT NULL DEFAULT {defaultEmpresaId};");
        }

        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS AuditLogEntries (
                Id INTEGER NOT NULL CONSTRAINT PK_AuditLogEntries PRIMARY KEY AUTOINCREMENT,
                EmpresaId INTEGER NOT NULL,
                EntityName TEXT NOT NULL,
                EntityId INTEGER NOT NULL,
                Action TEXT NOT NULL,
                UserName TEXT NOT NULL,
                Details TEXT NOT NULL,
                Timestamp TEXT NOT NULL
            );
            """);

        var auditLogColumns = GetColumns("AuditLogEntries");
        if (!auditLogColumns.Contains("EmpresaId"))
        {
            db.Database.ExecuteSqlRaw($"ALTER TABLE AuditLogEntries ADD COLUMN EmpresaId INTEGER NOT NULL DEFAULT {defaultEmpresaId};");
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
