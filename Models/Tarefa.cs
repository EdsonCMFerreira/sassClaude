using System.ComponentModel.DataAnnotations;
using Saas.Data;

namespace Saas.Models;

public class Tarefa : ITenantScoped
{
    public int Id { get; set; }

    public int EmpresaId { get; set; }

    public int ProjetoId { get; set; }

    public Projeto? Projeto { get; set; }

    [Required]
    [StringLength(200)]
    public string Titulo { get; set; } = string.Empty;

    public bool Concluida { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
