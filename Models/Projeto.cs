using System.ComponentModel.DataAnnotations;
using Saas.Data;

namespace Saas.Models;

public class Projeto : ITenantScoped
{
    public int Id { get; set; }

    public int EmpresaId { get; set; }

    [Required]
    [StringLength(150)]
    public string Nome { get; set; } = string.Empty;

    [StringLength(1000)]
    public string Descricao { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Tarefa> Tarefas { get; set; } = new List<Tarefa>();
}
