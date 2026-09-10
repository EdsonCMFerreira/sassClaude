using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Saas.Data;
using Saas.Models;

namespace Saas.Controllers;

[ApiController]
[Authorize]
[Route("api/projetos")]
public class ApiProjetosController : ControllerBase
{
    private readonly SassDbContext _context;

    public ApiProjetosController(SassDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProjetoResponse>>> GetProjetos()
    {
        var projetos = await _context.Projetos.OrderByDescending(x => x.CreatedAt).ToListAsync();
        return projetos.Select(ToResponse).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProjetoResponse>> GetProjeto(int id)
    {
        var projeto = await _context.Projetos.FindAsync(id);
        if (projeto is null)
        {
            return NotFound();
        }

        return ToResponse(projeto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<ProjetoResponse>> PostProjeto(ProjetoRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nome))
        {
            return BadRequest("Informe o nome do projeto.");
        }

        var projeto = new Projeto
        {
            Nome = request.Nome.Trim(),
            Descricao = request.Descricao.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.Projetos.Add(projeto);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetProjeto), new { id = projeto.Id }, ToResponse(projeto));
    }

    [HttpPut("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PutProjeto(int id, ProjetoRequest request)
    {
        var existing = await _context.Projetos.FindAsync(id);
        if (existing is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Nome))
        {
            return BadRequest("Informe o nome do projeto.");
        }

        existing.Nome = request.Nome.Trim();
        existing.Descricao = request.Descricao.Trim();

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteProjeto(int id)
    {
        var projeto = await _context.Projetos.FindAsync(id);
        if (projeto is null)
        {
            return NotFound();
        }

        _context.Projetos.Remove(projeto);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private static ProjetoResponse ToResponse(Projeto projeto)
    {
        return new ProjetoResponse(projeto.Id, projeto.Nome, projeto.Descricao, projeto.CreatedAt);
    }
}

public sealed record ProjetoRequest(string Nome, string Descricao);

public sealed record ProjetoResponse(int Id, string Nome, string Descricao, DateTime CreatedAt);

[ApiController]
[Authorize]
[Route("api/tarefas")]
public class ApiTarefasController : ControllerBase
{
    private readonly SassDbContext _context;

    public ApiTarefasController(SassDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TarefaResponse>>> GetTarefas([FromQuery] int projetoId)
    {
        var tarefas = await _context.Tarefas
            .Where(x => x.ProjetoId == projetoId)
            .OrderBy(x => x.Concluida)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync();
        return tarefas.Select(ToResponse).ToList();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<TarefaResponse>> PostTarefa(TarefaRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Titulo))
        {
            return BadRequest("Informe o título da tarefa.");
        }

        var projetoExiste = await _context.Projetos.AnyAsync(x => x.Id == request.ProjetoId);
        if (!projetoExiste)
        {
            return BadRequest("Projeto inválido.");
        }

        var tarefa = new Tarefa
        {
            ProjetoId = request.ProjetoId,
            Titulo = request.Titulo.Trim(),
            Concluida = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Tarefas.Add(tarefa);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetTarefas), new { projetoId = tarefa.ProjetoId }, ToResponse(tarefa));
    }

    [HttpPut("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PutTarefa(int id, TarefaRequest request)
    {
        var existing = await _context.Tarefas.FindAsync(id);
        if (existing is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Titulo))
        {
            return BadRequest("Informe o título da tarefa.");
        }

        existing.Titulo = request.Titulo.Trim();
        existing.Concluida = request.Concluida;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTarefa(int id)
    {
        var tarefa = await _context.Tarefas.FindAsync(id);
        if (tarefa is null)
        {
            return NotFound();
        }

        _context.Tarefas.Remove(tarefa);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private static TarefaResponse ToResponse(Tarefa tarefa)
    {
        return new TarefaResponse(tarefa.Id, tarefa.ProjetoId, tarefa.Titulo, tarefa.Concluida, tarefa.CreatedAt);
    }
}

public sealed record TarefaRequest(int ProjetoId, string Titulo, bool Concluida);

public sealed record TarefaResponse(int Id, int ProjetoId, string Titulo, bool Concluida, DateTime CreatedAt);
