using sassClaude.Data;

namespace sassClaude.Models;

public class AuditLogEntry : ITenantScoped
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
