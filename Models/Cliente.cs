using System.ComponentModel.DataAnnotations;

namespace sassClaude.Models;

public class Cliente
{
    public int Id { get; set; }

    [Required]
    [StringLength(150)]
    public string Nome { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string TipoPessoa { get; set; } = "Física";

    [Required]
    [StringLength(20)]
    public string CpfCnpj { get; set; } = string.Empty;

    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [StringLength(200)]
    public string Site { get; set; } = string.Empty;

    [StringLength(20)]
    public string Telefone { get; set; } = string.Empty;

    [StringLength(10)]
    public string CobrancaCep { get; set; } = string.Empty;

    [StringLength(200)]
    public string CobrancaEndereco { get; set; } = string.Empty;

    [StringLength(20)]
    public string CobrancaNumero { get; set; } = string.Empty;

    [StringLength(100)]
    public string CobrancaComplemento { get; set; } = string.Empty;

    [StringLength(100)]
    public string CobrancaBairro { get; set; } = string.Empty;

    [StringLength(100)]
    public string CobrancaCidade { get; set; } = string.Empty;

    [StringLength(2)]
    public string CobrancaUf { get; set; } = string.Empty;

    [StringLength(10)]
    public string EntregaCep { get; set; } = string.Empty;

    [StringLength(200)]
    public string EntregaEndereco { get; set; } = string.Empty;

    [StringLength(20)]
    public string EntregaNumero { get; set; } = string.Empty;

    [StringLength(100)]
    public string EntregaComplemento { get; set; } = string.Empty;

    [StringLength(100)]
    public string EntregaBairro { get; set; } = string.Empty;

    [StringLength(100)]
    public string EntregaCidade { get; set; } = string.Empty;

    [StringLength(2)]
    public string EntregaUf { get; set; } = string.Empty;

    [StringLength(1000)]
    public string Observacoes { get; set; } = string.Empty;

    public DateTime? UltimaCompraData { get; set; }

    public decimal? UltimaCompraValor { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
