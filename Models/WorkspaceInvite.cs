using Saas.Data;

namespace Saas.Models;

public class WorkspaceInvite : ITenantScoped
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public int InvitedByLoginId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public Login InvitedBy { get; set; } = null!;
}
