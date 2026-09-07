using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sassClaude.Data;
using sassClaude.Models;

namespace sassClaude.Controllers;

[Authorize]
public class FornecedoresController : Controller
{
    public IActionResult Index() => View();
}

[ApiController]
[Authorize]
[Route("api/fornecedores")]
public class ApiFornecedoresController : ControllerBase
{
    private readonly SassDbContext _context;

    public ApiFornecedoresController(SassDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<FornecedorResponse>>> GetFornecedores()
    {
        var fornecedores = await _context.Fornecedores.OrderBy(x => x.Nome).ToListAsync();
        return fornecedores.Select(ToResponse).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<FornecedorResponse>> GetFornecedor(int id)
    {
        var fornecedor = await _context.Fornecedores.FindAsync(id);
        if (fornecedor is null)
        {
            return NotFound();
        }

        return ToResponse(fornecedor);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<FornecedorResponse>> PostFornecedor(FornecedorRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nome) || string.IsNullOrWhiteSpace(request.CpfCnpj))
        {
            return BadRequest("Nome e CPF/CNPJ são obrigatórios.");
        }

        var cpfCnpj = request.CpfCnpj.Trim();
        var alreadyExists = await _context.Fornecedores.AnyAsync(item => item.CpfCnpj == cpfCnpj);
        if (alreadyExists)
        {
            return BadRequest("Já existe um fornecedor com esse CPF/CNPJ.");
        }

        var fornecedor = FromRequest(new Fornecedor { CreatedAt = DateTime.UtcNow }, request);
        _context.Fornecedores.Add(fornecedor);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetFornecedor), new { id = fornecedor.Id }, ToResponse(fornecedor));
    }

    [HttpPut("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PutFornecedor(int id, FornecedorRequest request)
    {
        var existing = await _context.Fornecedores.FindAsync(id);
        if (existing is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Nome) || string.IsNullOrWhiteSpace(request.CpfCnpj))
        {
            return BadRequest("Nome e CPF/CNPJ são obrigatórios.");
        }

        var cpfCnpj = request.CpfCnpj.Trim();
        var cpfCnpjTaken = await _context.Fornecedores.AnyAsync(item => item.Id != id && item.CpfCnpj == cpfCnpj);
        if (cpfCnpjTaken)
        {
            return BadRequest("Já existe um fornecedor com esse CPF/CNPJ.");
        }

        FromRequest(existing, request);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteFornecedor(int id)
    {
        var fornecedor = await _context.Fornecedores.FindAsync(id);
        if (fornecedor is null)
        {
            return NotFound();
        }

        _context.Fornecedores.Remove(fornecedor);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private static Fornecedor FromRequest(Fornecedor fornecedor, FornecedorRequest request)
    {
        fornecedor.Nome = request.Nome.Trim();
        fornecedor.TipoPessoa = request.TipoPessoa.Trim();
        fornecedor.CpfCnpj = request.CpfCnpj.Trim();
        fornecedor.ContatoResponsavel = request.ContatoResponsavel.Trim();
        fornecedor.Email = request.Email.Trim();
        fornecedor.Site = request.Site.Trim();
        fornecedor.Telefone = request.Telefone.Trim();
        fornecedor.Cep = request.Cep.Trim();
        fornecedor.Endereco = request.Endereco.Trim();
        fornecedor.Numero = request.Numero.Trim();
        fornecedor.Complemento = request.Complemento.Trim();
        fornecedor.Bairro = request.Bairro.Trim();
        fornecedor.Cidade = request.Cidade.Trim();
        fornecedor.Uf = request.Uf.Trim();
        fornecedor.Observacoes = request.Observacoes.Trim();
        return fornecedor;
    }

    private static FornecedorResponse ToResponse(Fornecedor fornecedor)
    {
        return new FornecedorResponse(
            fornecedor.Id, fornecedor.Nome, fornecedor.TipoPessoa, fornecedor.CpfCnpj, fornecedor.ContatoResponsavel,
            fornecedor.Email, fornecedor.Site, fornecedor.Telefone, fornecedor.Cep, fornecedor.Endereco, fornecedor.Numero,
            fornecedor.Complemento, fornecedor.Bairro, fornecedor.Cidade, fornecedor.Uf,
            fornecedor.Observacoes,
            fornecedor.CreatedAt);
    }
}

public sealed record FornecedorRequest(
    string Nome, string TipoPessoa, string CpfCnpj, string ContatoResponsavel, string Email, string Site, string Telefone,
    string Cep, string Endereco, string Numero, string Complemento, string Bairro, string Cidade, string Uf,
    string Observacoes);

public sealed record FornecedorResponse(
    int Id, string Nome, string TipoPessoa, string CpfCnpj, string ContatoResponsavel, string Email, string Site, string Telefone,
    string Cep, string Endereco, string Numero, string Complemento, string Bairro, string Cidade, string Uf,
    string Observacoes, DateTime CreatedAt);
