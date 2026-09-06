using System.ComponentModel.DataAnnotations;

namespace sassClaude.Models;

public sealed class WorkspaceInviteViewModel
{
    [Required(ErrorMessage = "Informe o e-mail da pessoa a convidar.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [StringLength(150)]
    [Display(Name = "E-mail do convidado")]
    public string Email { get; set; } = string.Empty;
}

public sealed record WorkspaceInviteRow(string Email, DateTime CreatedAt, DateTime ExpiresAt, bool Accepted);

public sealed class WorkspaceInvitePageViewModel
{
    public WorkspaceInviteViewModel Form { get; set; } = new();
    public List<WorkspaceInviteRow> Invites { get; set; } = new();
}
