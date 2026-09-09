using System.ComponentModel.DataAnnotations;

namespace Saas.Models;

public sealed class ProfileViewModel
{
    [Required(ErrorMessage = "Informe seu nome de usuário.")]
    [StringLength(100)]
    [Display(Name = "Usuário")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe seu e-mail.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;
}

public sealed class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Informe sua senha atual.")]
    [DataType(DataType.Password)]
    [Display(Name = "Senha atual")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a nova senha.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "A senha deve ter pelo menos 6 caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Nova senha")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirme sua nova senha.")]
    [Compare(nameof(NewPassword), ErrorMessage = "As senhas não conferem.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirmar nova senha")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

public sealed class SettingsPageViewModel
{
    public ProfileViewModel Profile { get; set; } = new();
    public ChangePasswordViewModel Password { get; set; } = new();
    public bool EmailConfirmed { get; set; }
}
