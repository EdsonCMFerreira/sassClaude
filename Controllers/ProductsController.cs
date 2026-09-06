using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sassClaude.Data;
using sassClaude.Models;

namespace sassClaude.Controllers;

[Authorize]
public class ProductsController : Controller
{
    public IActionResult Index() => View();
}

[ApiController]
[Authorize]
[Route("api/products")]
public class ApiProductsController : ControllerBase
{
    private readonly SassDbContext _context;

    public ApiProductsController(SassDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductResponse>>> GetProducts()
    {
        return await _context.Products
            .OrderBy(x => x.Codigo)
            .Select(product => ToResponse(product))
            .ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductResponse>> GetProduct(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product is null)
        {
            return NotFound();
        }

        return ToResponse(product);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<ProductResponse>> PostProduct(ProductRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Codigo) ||
            string.IsNullOrWhiteSpace(request.Descricao) ||
            string.IsNullOrWhiteSpace(request.Fornecedor))
        {
            return BadRequest("Código, descrição e fornecedor são obrigatórios.");
        }

        var codigo = request.Codigo.Trim();
        var alreadyExists = await _context.Products.AnyAsync(item => item.Codigo == codigo);
        if (alreadyExists)
        {
            return BadRequest("Já existe um produto com esse código.");
        }

        var product = new Product
        {
            Codigo = codigo,
            Descricao = request.Descricao.Trim(),
            Validade = request.Validade,
            Valor = request.Valor,
            Fornecedor = request.Fornecedor.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, ToResponse(product));
    }

    [HttpPut("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PutProduct(int id, ProductRequest request)
    {
        var existing = await _context.Products.FindAsync(id);
        if (existing is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Codigo) ||
            string.IsNullOrWhiteSpace(request.Descricao) ||
            string.IsNullOrWhiteSpace(request.Fornecedor))
        {
            return BadRequest("Código, descrição e fornecedor são obrigatórios.");
        }

        var codigo = request.Codigo.Trim();
        var codeTaken = await _context.Products.AnyAsync(item => item.Id != id && item.Codigo == codigo);
        if (codeTaken)
        {
            return BadRequest("Já existe um produto com esse código.");
        }

        existing.Codigo = codigo;
        existing.Descricao = request.Descricao.Trim();
        existing.Validade = request.Validade;
        existing.Valor = request.Valor;
        existing.Fornecedor = request.Fornecedor.Trim();

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product is null)
        {
            return NotFound();
        }

        _context.Products.Remove(product);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private static ProductResponse ToResponse(Product product)
    {
        return new ProductResponse(product.Id, product.Codigo, product.Descricao, product.Validade, product.Valor, product.Fornecedor, product.CreatedAt);
    }
}

public sealed record ProductRequest(string Codigo, string Descricao, DateTime Validade, decimal Valor, string Fornecedor);

public sealed record ProductResponse(int Id, string Codigo, string Descricao, DateTime Validade, decimal Valor, string Fornecedor, DateTime CreatedAt);
