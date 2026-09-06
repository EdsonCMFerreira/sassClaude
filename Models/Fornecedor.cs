using System.ComponentModel.DataAnnotations;

namespace sassClaude.Models;

public class Fornecedor
{
    public int Id { get; set; }

    [Required]
    [StringLength(150)]
    public string Nome { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string TipoPessoa { get; set; } = "Jurídica";

    [Required]
    [StringLength(20)]
    public string CpfCnpj { get; set; } = string.Empty;

    [StringLength(150)]
    public string ContatoResponsavel { get; set; } = string.Empty;

    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [StringLength(20)]
    public string Telefone { get; set; } = string.Empty;

    [StringLength(10)]
    public string Cep { get; set; } = string.Empty;

    [StringLength(200)]
    public string Endereco { get; set; } = string.Empty;

    [StringLength(20)]
    public string Numero { get; set; } = string.Empty;

    [StringLength(100)]
    public string Complemento { get; set; } = string.Empty;

    [StringLength(100)]
    public string Bairro { get; set; } = string.Empty;

    [StringLength(100)]
    public string Cidade { get; set; } = string.Empty;

    [StringLength(2)]
    public string Uf { get; set; } = string.Empty;

    public DateTime? UltimaCompraData { get; set; }

    public decimal? UltimaCompraValor { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
