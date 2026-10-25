using CostManagement.Domain.Divisions;
using CostManagement.Domain.Shared;

namespace CostManagement.Domain.Tests;

public class DivisionBudgetApprovalTests
{
    private static readonly DateTime Now = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly FiscalHalf Half = new(2026, HalfTerm.H1);

    [Fact]
    public void 部承認は部と半期と承認日時を持って作成される()
    {
        var divisionId = DivisionId.New();

        var approval = DivisionBudgetApproval.Approve(divisionId, Half, Now);

        Assert.Equal(divisionId, approval.DivisionId);
        Assert.Equal(Half, approval.FiscalHalf);
        Assert.Equal(Now, approval.ApprovedAt);
    }

    [Fact]
    public void 復元は保存時の状態を再構築する()
    {
        var id = Guid.NewGuid();
        var divisionId = Guid.NewGuid();

        var restored = DivisionBudgetApproval.Restore(id, divisionId, "2026-H1", Now);

        Assert.Equal(id, restored.Id.Value);
        Assert.Equal(divisionId, restored.DivisionId.Value);
        Assert.Equal(Half, restored.FiscalHalf);
        Assert.Equal(Now, restored.ApprovedAt);
    }
}
