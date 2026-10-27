using Reqnroll;
using Reqnroll.UnitTestProvider;

namespace CostManagement.E2E.Tests.Support;

/// <summary>
/// `@未実装` のシナリオを実行時に飛ばす(Skipped にする)。要件の工程([B])で、実装より先に受け入れシナリオを書くための印。
/// 実装の工程([D])の PR でタグを外す(外し忘れは CI「要件とシナリオの網羅」が落とす。requirements/README.md)。
/// </summary>
[Binding]
public sealed class 未実装のシナリオ(IUnitTestRuntimeProvider runtime)
{
    /// <summary>ほかのフックより先に飛ばす(前提のデータを作らない)。</summary>
    [BeforeScenario("未実装", Order = int.MinValue)]
    public void 飛ばす() => runtime.TestIgnore("@未実装: 実装の工程([D])でステップと実装を入れ、タグを外す");
}
