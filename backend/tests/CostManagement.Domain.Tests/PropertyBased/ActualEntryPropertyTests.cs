using CostManagement.Domain.Actuals;
using CostManagement.Domain.Budgeting;
using CostManagement.Domain.Shared;
using CsCheck;
using static CostManagement.Domain.Tests.PropertyBased.ドメイン生成器;

namespace CostManagement.Domain.Tests.PropertyBased;

/// <summary>実績(ActualEntry)の計上規則の性質。</summary>
public class 実績計上の性質
{
    [Fact]
    public void 計上できるのは区分に合った案件か費目を持ち月と金額と明細名が妥当な実績だけ()
    {
        Gen.Select(区分, 案件.Nullable(0.4), 費目.Nullable(0.4), Gen.Int[-1, 8].Nullable(0.3),
                金額_負を含む, 明細名)
            .Sample((category, project, element, month, amount, detail) =>
            {
                var keyIsValid = category.IsProjectBased()
                    ? project is not null && element is null
                    : element is not null && project is null;
                var monthIsValid = month is null or (>= 1 and <= HalfMonths.Count);
                var detailIsValid = string.IsNullOrWhiteSpace(detail) || category == BudgetCategory.PeriodCost;
                var shouldSucceed = keyIsValid && monthIsValid && amount >= 0m && detailIsValid;

                var error = Record.Exception(() => ActualEntry.Record(対象課, 対象半期, category, project,
                    element, month, new Money(amount), null, 現在時刻, detail));

                if (shouldSucceed)
                    Assert.Null(error);
                else
                    Assert.IsType<DomainException>(error);
            });
    }

    [Fact]
    public void 明細名と備考は前後の空白を除いて保持され空白だけなら未指定になる()
    {
        Gen.Select(費目, 明細名, Gen.OneOfConst<string?>(null, "", " ", "月末払い", "  月末払い  "))
            .Sample((element, detail, note) =>
            {
                var entry = ActualEntry.Record(対象課, 対象半期, BudgetCategory.PeriodCost, null, element,
                    null, new Money(1m), note, 現在時刻, detail);

                Assert.Equal(string.IsNullOrWhiteSpace(detail) ? null : detail.Trim(), entry.PeriodDetail);
                Assert.Equal(string.IsNullOrWhiteSpace(note) ? null : note.Trim(), entry.Note);
            });
    }

    [Fact]
    public void 永続化形式から復元した実績は元の実績と同じ値を持つ()
    {
        実績.Sample(entry =>
        {
            var restored = ActualEntry.Restore(entry.Id.Value, entry.DepartmentId.Value,
                entry.FiscalHalf.ToString(), entry.Category.ToString(), entry.ProjectId?.Value,
                entry.ElementCode?.Value, entry.PeriodDetail, entry.Month, entry.Amount.Value,
                entry.Note, entry.RecordedAt);

            Assert.Equal(
                (entry.Id, entry.DepartmentId, entry.FiscalHalf, entry.Category, entry.ProjectId,
                    entry.ElementCode, entry.PeriodDetail, entry.Month, entry.Amount, entry.Note),
                (restored.Id, restored.DepartmentId, restored.FiscalHalf, restored.Category,
                    restored.ProjectId, restored.ElementCode, restored.PeriodDetail, restored.Month,
                    restored.Amount, restored.Note));
        });
    }
}
