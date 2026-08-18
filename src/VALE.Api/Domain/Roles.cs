namespace VALE.Api.Domain;

public static class Roles
{
    public const string Owner = "Owner";
    public const string Admin = "Admin";
    public const string OperationsManager = "OperationsManager";
    public const string Manager = "Manager"; // legacy compatibility
    public const string BranchManager = "BranchManager";
    public const string Supervisor = "Supervisor";
    public const string Cashier = "Cashier";
    public const string Valet = "Valet";
    public const string Auditor = "Auditor";

    public static readonly string[] All =
    [
        Owner, Admin, OperationsManager, Manager, BranchManager, Supervisor, Cashier, Valet, Auditor
    ];

    public const string StaffPolicy = "Staff";
    public const string ManageUsersPolicy = "ManageUsers";
    public const string ManageBranchesPolicy = "ManageBranches";
    public const string OperationWritePolicy = "OperationWrite";
    public const string FinancePolicy = "Finance";
    public const string ReportsPolicy = "Reports";
    public const string AuditPolicy = "Audit";
    public const string RecordsEditPolicy = "RecordsEdit";
    public const string DeleteRecordsPolicy = "DeleteRecords";

    public static readonly string[] ManageUsersRoles = [Owner, Admin, OperationsManager, Manager, BranchManager];
    public static readonly string[] ManageBranchesRoles = [Owner, Admin, OperationsManager, Manager];
    public static readonly string[] OperationWriteRoles = [Owner, Admin, OperationsManager, Manager, BranchManager, Supervisor, Valet];
    public static readonly string[] FinanceRoles = [Owner, Admin, OperationsManager, Manager, BranchManager, Cashier];
    public static readonly string[] ReportRoles = [Owner, Admin, OperationsManager, Manager, BranchManager, Auditor];
    public static readonly string[] AuditRoles = [Owner, Admin, OperationsManager, Manager, Auditor];
    public static readonly string[] RecordsEditRoles = [Owner, Admin, OperationsManager, Manager, BranchManager, Supervisor];
    public static readonly string[] DeleteRecordsRoles = [Owner, Admin, OperationsManager, Manager, BranchManager];
    public static readonly string[] CrossBranchRoles = [Owner, Admin, OperationsManager, Manager];

    private static readonly IReadOnlyDictionary<string, int> Rank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        [Owner] = 100,
        [Admin] = 90,
        [OperationsManager] = 80,
        [Manager] = 80,
        [BranchManager] = 70,
        [Supervisor] = 60,
        [Auditor] = 50,
        [Cashier] = 40,
        [Valet] = 30
    };

    public static int HighestRank(IEnumerable<string> roles) => roles
        .Select(role => Rank.TryGetValue(role, out var rank) ? rank : 0)
        .DefaultIfEmpty(0)
        .Max();

    public static bool Contains(IEnumerable<string> roles, string role) =>
        roles.Contains(role, StringComparer.OrdinalIgnoreCase);

    public static bool CanAssignRoles(IEnumerable<string> actorRoles, IEnumerable<string> requestedRoles)
    {
        var actor = actorRoles.ToArray();
        var target = requestedRoles.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (target.Length == 0 || target.Any(role => !All.Contains(role, StringComparer.OrdinalIgnoreCase))) return false;
        if (Contains(actor, Owner)) return true;
        return HighestRank(actor) > HighestRank(target);
    }

    public static bool CanManageTarget(IEnumerable<string> actorRoles, IEnumerable<string> targetRoles)
    {
        var actor = actorRoles.ToArray();
        if (Contains(actor, Owner)) return true;
        return HighestRank(actor) > HighestRank(targetRoles);
    }
}
