---
name: pagina-filtrada
description: Cria uma página somente-leitura filtrada a partir de um cadastro existente (Produtos, Clientes, Fornecedores, etc.), seguindo o padrão já usado em Vencidos, Saldo 0, Vencendo em 30 dias e Pessoa Física/Jurídica.
---

# Skill: página filtrada de um cadastro existente

Use quando o usuário pedir uma nova "tela de ver X" que é apenas um
recorte filtrado de um cadastro que já existe (ex: "produtos sem
fornecedor", "clientes sem e-mail", "fornecedores de SP").

## Passos

1. **Controller**: adicione uma action simples no controller do
   cadastro (ex: `ProdutosController`) que retorna `View()`. Se houver
   mais de uma variação parecida (ex: várias por status), prefira uma
   view compartilhada com um `record` de ViewModel (veja
   `PedidoStatusPageViewModel` e `ClienteTipoPessoaPageViewModel` como
   referência) em vez de duplicar a view.

2. **View** (`Views/<Entidade>/<Nome>.cshtml`):
   - `page-heading` com título, descrição e um link "← Ver todos os X"
     de volta para o Index.
   - `surface-card users-card` com `table-wrap` > `data-table`.
   - No `@section Scripts`, busque os dados do endpoint que já existe
     (`/api/<entidade>`), filtre/ordene no cliente com JS puro, e
     renderize as linhas com template literals — sem inventar um
     endpoint novo se o existente já traz os campos necessários.
   - Cada linha tem um link "Editar" para `/<Entidade>?editar=<id>`.

3. **Deep-link de edição**: se a tela `Index.cshtml` do cadastro ainda
   não suporta `?editar=<id>`, adicione ao final do carregamento:
   ```js
   .then(() => {
       const editarId = new URLSearchParams(window.location.search).get('editar');
       if (editarId) editItem(Number(editarId));
   })
   ```

4. **Link de entrada**: adicione um botão no `hero-actions` do
   `Index.cshtml` (e do `Dashboard.cshtml`, se existir) apontando para
   a nova action, no mesmo estilo dos botões já existentes.

5. **Build e teste**:
   - `dotnet build` (pare o servidor de dev rodando antes, se o
     `bin/Debug/net9.0/Saas.exe` estiver travado por ele).
   - Rode contra um banco SQLite isolado em vez do `Saas.db` real:
     `ConnectionStrings__DefaultConnection=Data Source=<caminho temp>`.
   - Registre uma conta de teste, crie 2-3 registros que cubram o caso
     "aparece" e o caso "não aparece" no filtro, e confirme via `curl`
     + a mesma lógica JS rodada em Node (`node script.js dados.json`)
     que o filtro está correto — não dá pra ver o resultado renderizado
     só com `curl` porque a tabela é montada no cliente.
   - Depois, reinicie o servidor de dev real (porta original) com a
     build nova.

## Não fazer

- Não crie um endpoint de API novo se o `/api/<entidade>` existente já
  devolve os campos necessários para o filtro.
- Não misture o padrão de formulário MVC (Workspace) com o padrão
  JS/fetch (Produtos/Clientes) na mesma tela sem necessidade — siga o
  padrão que a tela de origem já usa.
