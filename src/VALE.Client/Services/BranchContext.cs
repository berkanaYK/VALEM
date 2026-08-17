using VALE.Contracts;

namespace VALE.Client.Services;

public sealed class BranchContext
{
    public Guid? ActiveBranchId { get; private set; }
    public BranchDto? ActiveBranch { get; private set; }

    public void Initialize(UserDto user)
    {
        ActiveBranchId = user.BranchId;
        ActiveBranch = null;
    }

    public void Select(BranchDto branch)
    {
        ActiveBranchId = branch.Id;
        ActiveBranch = branch;
    }

    public void Clear()
    {
        ActiveBranchId = null;
        ActiveBranch = null;
    }
}
