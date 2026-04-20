using AFTRS.Models;

namespace AFTRS.ViewModels;

public class ResolutionViewModel
{
    public List<Transaction> UnmatchedLedger { get; set; } = new();
    public List<Transaction> UnmatchedBank { get; set; } = new();
}