namespace sassClaude.Models;

public sealed class BillingViewModel
{
    public string Plano { get; set; } = "Starter";
    public List<Invoice> Invoices { get; set; } = new();
}
