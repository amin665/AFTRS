using AFTRS.Models;

namespace AFTRS.ViewModels;

public class ResolutionViewModel
{
    public List<Transaction> LedgerDiscrepancies { get; set; } = new();
    public List<Transaction> BankDiscrepancies { get; set; } = new();
}
