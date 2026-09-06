namespace sassClaude.Models;

public class Invoice
{
    public int Id { get; set; }
    public int LoginId { get; set; }
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public decimal Amount { get; set; }
    public string Status { get; set; } = "Pago";
    public string Description { get; set; } = string.Empty;
    public Login Login { get; set; } = null!;
}
