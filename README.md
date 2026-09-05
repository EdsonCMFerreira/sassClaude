# sassClaude

Aplicação ASP.NET Core MVC com API CRUD para cadastro de usuários de login.

## Recursos

- Interface MVC para cadastro, edição e exclusão de usuários
- API REST em `/api/login`
- SQLite local para persistência
- Senhas protegidas com hash
- Migração automática de registros antigos em texto puro

## Executar

```powershell
dotnet run --urls http://localhost:5002
```

Acesse `http://localhost:5002/Login`.

## Tecnologias

- .NET 9
- ASP.NET Core MVC
- Entity Framework Core
- SQLite
