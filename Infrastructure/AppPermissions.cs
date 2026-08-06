namespace AFTRS.Infrastructure;

public static class AppPermissions
{
    public const string Import = "Import";
    public const string Reconcile = "Reconcile";
    public const string ResolveDiscrepancies = "ResolveDiscrepancies";
    public const string StrategicIntelligence = "StrategicIntelligence";
    public const string Templates = "Templates";
    public const string Reports = "Reports";
    public const string Sessions = "Sessions";

    public static readonly IReadOnlyList<(string Key, string LabelKey)> All = new List<(string, string)>
    {
        (Import, "DataImport"),
        (Reconcile, "RunEngine"),
        (ResolveDiscrepancies, "Discrepancies"),
        (StrategicIntelligence, "Intelligence"),
        (Templates, "Templates"),
        (Reports, "Reports"),
        (Sessions, "Sessions")
    };

    public static bool IsValid(string permission) => All.Any(p => p.Key == permission);
}
