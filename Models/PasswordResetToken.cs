namespace Saas.Models;

public class PasswordResetToken
{
    public int Id { get; set; }
    public int LoginId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public Login Login { get; set; } = null!;
}