using System.ComponentModel.DataAnnotations;

namespace Saas.Models;

public sealed class ContactMessageViewModel
{
    [Required(ErrorMessage = "Informe seu nome.")]
    [StringLength(100)]
    [Display(Name = "Nome")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe seu e-mail.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [StringLength(150)]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escreva sua mensagem.")]
    [StringLength(2000)]
    [Display(Name = "Mensagem")]
    public string Message { get; set; } = string.Empty;
}
