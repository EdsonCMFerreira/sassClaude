# sassClaude

Aplicação ASP.NET Core MVC com API CRUD para cadastro de usuários de login.

## Recursos

- Interface MVC para cadastro, edição e exclusão de usuários
- API REST em `/api/login`
- SQLite local para persistência
- Senhas protegidas com hash
- Migração automática de registros antigos em texto puro
- Autenticação por cookie e áreas privadas
- Proteção CSRF nas operações de alteração
- Testes de integração com xUnit

## Configurar envio de e-mail

Preencha a seção `Email` em `appsettings.json` ou use um arquivo de configuração
por ambiente com `Host`, `Port`, `Username`, `Password` e `From`. Em desenvolvimento,
sem SMTP configurado, o link é registrado no log da aplicação para facilitar o teste.
A tela de confirmação também mostra esse link somente no ambiente `Development`.
Para produção, defina também `BaseUrl` com o endereço público da aplicação. Variáveis
de ambiente usam o formato `Email__Host`, `Email__Port`, `Email__Username`, `Email__Password`,
`Email__From` e `Email__BaseUrl`.

## Executar

```powershell
dotnet run --urls http://localhost:5002
```

Acesse `http://localhost:5002/Login`.

## Testes

```powershell
$env:DOTNET_ROOT = "C:\Program Files\dotnet"
dotnet test .\sassClaude.Tests\sassClaude.Tests.csproj
```

## Tecnologias

- .NET 9
- ASP.NET Core MVC
- Entity Framework Core
- SQLite
