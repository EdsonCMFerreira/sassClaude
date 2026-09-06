using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sassClaude.Data;
using sassClaude.Models;

namespace sassClaude.Controllers;

[Authorize]
public class ClientesController : Controller
{
    public IActionResult Index() => View();
}

[ApiController]
[Authorize]
[Route("api/clientes")]
public class ApiClientesController : ControllerBase
{
    private readonly SassDbContext _context;

    public ApiClientesController(SassDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ClienteResponse>>> GetClientes()
    {
        var clientes = await _context.Clientes.OrderBy(x => x.Nome).ToListAsync();
        return clientes.Select(ToResponse).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ClienteResponse>> GetCliente(int id)
    {
        var cliente = await _context.Clientes.FindAsync(id);
        if (cliente is null)
        {
            return NotFound();
        }

        return ToResponse(cliente);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<ClienteResponse>> PostCliente(ClienteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nome) || string.IsNullOrWhiteSpace(request.CpfCnpj))
        {
            return BadRequest("Nome e CPF/CNPJ são obrigatórios.");
        }

        var cpfCnpj = request.CpfCnpj.Trim();
        var alreadyExists = await _context.Clientes.AnyAsync(item => item.CpfCnpj == cpfCnpj);
        if (alreadyExists)
        {
            return BadRequest("Já existe um cliente com esse CPF/CNPJ.");
        }

        var cliente = FromRequest(new Cliente { CreatedAt = DateTime.UtcNow }, request);
        _context.Clientes.Add(cliente);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetCliente), new { id = cliente.Id }, ToResponse(cliente));
    }

    [HttpPut("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PutCliente(int id, ClienteRequest request)
    {
        var existing = await _context.Clientes.FindAsync(id);
        if (existing is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Nome) || string.IsNullOrWhiteSpace(request.CpfCnpj))
        {
            return BadRequest("Nome e CPF/CNPJ são obrigatórios.");
        }

        var cpfCnpj = request.CpfCnpj.Trim();
        var cpfCnpjTaken = await _context.Clientes.AnyAsync(item => item.Id != id && item.CpfCnpj == cpfCnpj);
        if (cpfCnpjTaken)
        {
            return BadRequest("Já existe um cliente com esse CPF/CNPJ.");
        }

        FromRequest(existing, request);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCliente(int id)
    {
        var cliente = await _context.Clientes.FindAsync(id);
        if (cliente is null)
        {
            return NotFound();
        }

        _context.Clientes.Remove(cliente);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private static Cliente FromRequest(Cliente cliente, ClienteRequest request)
    {
        cliente.Nome = request.Nome.Trim();
        cliente.TipoPessoa = request.TipoPessoa.Trim();
        cliente.CpfCnpj = request.CpfCnpj.Trim();
        cliente.Email = request.Email.Trim();
        cliente.Site = request.Site.Trim();
        cliente.Telefone = request.Telefone.Trim();
        cliente.Cep = request.Cep.Trim();
        cliente.Endereco = request.Endereco.Trim();
        cliente.Numero = request.Numero.Trim();
        cliente.Complemento = request.Complemento.Trim();
        cliente.Bairro = request.Bairro.Trim();
        cliente.Cidade = request.Cidade.Trim();
        cliente.Uf = request.Uf.Trim();
        return cliente;
    }

    private static ClienteResponse ToResponse(Cliente cliente)
    {
        return new ClienteResponse(
            cliente.Id, cliente.Nome, cliente.TipoPessoa, cliente.CpfCnpj, cliente.Email, cliente.Site, cliente.Telefone,
            cliente.Cep, cliente.Endereco, cliente.Numero, cliente.Complemento, cliente.Bairro, cliente.Cidade, cliente.Uf,
            cliente.UltimaCompraData, cliente.UltimaCompraValor, cliente.CreatedAt);
    }
}

public sealed record ClienteRequest(
    string Nome, string TipoPessoa, string CpfCnpj, string Email, string Site, string Telefone,
    string Cep, string Endereco, string Numero, string Complemento, string Bairro, string Cidade, string Uf);

public sealed record ClienteResponse(
    int Id, string Nome, string TipoPessoa, string CpfCnpj, string Email, string Site, string Telefone,
    string Cep, string Endereco, string Numero, string Complemento, string Bairro, string Cidade, string Uf,
    DateTime? UltimaCompraData, decimal? UltimaCompraValor, DateTime CreatedAt);
