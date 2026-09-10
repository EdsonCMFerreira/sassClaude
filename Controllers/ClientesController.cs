using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Saas.Data;
using Saas.Models;

namespace Saas.Controllers;

[Authorize]
public class ClientesController : Controller
{
    public IActionResult Index() => View();

    public IActionResult PessoaFisica() => View("PorTipoPessoa", new ClienteTipoPessoaPageViewModel(
        "Física", "Clientes pessoa física", "Clientes cadastrados como pessoa física."));

    public IActionResult PessoaJuridica() => View("PorTipoPessoa", new ClienteTipoPessoaPageViewModel(
        "Jurídica", "Clientes pessoa jurídica", "Clientes cadastrados como pessoa jurídica."));
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
        cliente.CobrancaCep = request.CobrancaCep.Trim();
        cliente.CobrancaEndereco = request.CobrancaEndereco.Trim();
        cliente.CobrancaNumero = request.CobrancaNumero.Trim();
        cliente.CobrancaComplemento = request.CobrancaComplemento.Trim();
        cliente.CobrancaBairro = request.CobrancaBairro.Trim();
        cliente.CobrancaCidade = request.CobrancaCidade.Trim();
        cliente.CobrancaUf = request.CobrancaUf.Trim();
        cliente.EntregaCep = request.EntregaCep.Trim();
        cliente.EntregaEndereco = request.EntregaEndereco.Trim();
        cliente.EntregaNumero = request.EntregaNumero.Trim();
        cliente.EntregaComplemento = request.EntregaComplemento.Trim();
        cliente.EntregaBairro = request.EntregaBairro.Trim();
        cliente.EntregaCidade = request.EntregaCidade.Trim();
        cliente.EntregaUf = request.EntregaUf.Trim();
        cliente.Observacoes = request.Observacoes.Trim();
        return cliente;
    }

    private static ClienteResponse ToResponse(Cliente cliente)
    {
        return new ClienteResponse(
            cliente.Id, cliente.Nome, cliente.TipoPessoa, cliente.CpfCnpj, cliente.Email, cliente.Site, cliente.Telefone,
            cliente.CobrancaCep, cliente.CobrancaEndereco, cliente.CobrancaNumero, cliente.CobrancaComplemento,
            cliente.CobrancaBairro, cliente.CobrancaCidade, cliente.CobrancaUf,
            cliente.EntregaCep, cliente.EntregaEndereco, cliente.EntregaNumero, cliente.EntregaComplemento,
            cliente.EntregaBairro, cliente.EntregaCidade, cliente.EntregaUf,
            cliente.Observacoes,
            cliente.UltimaCompraData, cliente.UltimaCompraValor, cliente.CreatedAt);
    }
}

public sealed record ClienteRequest(
    string Nome, string TipoPessoa, string CpfCnpj, string Email, string Site, string Telefone,
    string CobrancaCep, string CobrancaEndereco, string CobrancaNumero, string CobrancaComplemento,
    string CobrancaBairro, string CobrancaCidade, string CobrancaUf,
    string EntregaCep, string EntregaEndereco, string EntregaNumero, string EntregaComplemento,
    string EntregaBairro, string EntregaCidade, string EntregaUf,
    string Observacoes);

public sealed record ClienteResponse(
    int Id, string Nome, string TipoPessoa, string CpfCnpj, string Email, string Site, string Telefone,
    string CobrancaCep, string CobrancaEndereco, string CobrancaNumero, string CobrancaComplemento,
    string CobrancaBairro, string CobrancaCidade, string CobrancaUf,
    string EntregaCep, string EntregaEndereco, string EntregaNumero, string EntregaComplemento,
    string EntregaBairro, string EntregaCidade, string EntregaUf,
    string Observacoes,
    DateTime? UltimaCompraData, decimal? UltimaCompraValor, DateTime CreatedAt);
