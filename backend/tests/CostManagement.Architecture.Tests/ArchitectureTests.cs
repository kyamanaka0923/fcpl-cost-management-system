using System.Reflection;
using CostManagement.Application;
using CostManagement.Domain.Shared;
using CostManagement.Infrastructure;
using NetArchTest.Rules;

namespace CostManagement.Architecture.Tests;

/// <summary>
/// ヘキサゴナルアーキテクチャの依存ルールを検証するテスト。
/// 依存は常に内側(Domain)に向かい、Domain は他のどの層・技術詳細にも依存しない。
/// docs/DESIGN.md のコンポーネント図に対応する。
/// </summary>
public class LayerDependencyTests
{
    private static readonly Assembly DomainAssembly = typeof(DomainException).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(ProjectService).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(DependencyInjection).Assembly;

    private const string ApplicationNamespace = "CostManagement.Application";
    private const string InfrastructureNamespace = "CostManagement.Infrastructure";
    private const string WebApiNamespace = "CostManagement.WebApi";

    private static void AssertSuccessful(TestResult result)
    {
        var offenders = result.FailingTypes?.Select(t => t.FullName ?? t.Name) ?? [];
        Assert.True(result.IsSuccessful,
            $"アーキテクチャルール違反: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void ドメイン層は外側の層に依存しない()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNamespace, InfrastructureNamespace, WebApiNamespace)
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void ドメイン層は永続化やWebの技術詳細に依存しない()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Dapper",
                "Microsoft.Data.Sqlite",
                "Microsoft.AspNetCore",
                "Microsoft.Extensions",
                "System.Data")
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void アプリケーション層はインフラ層とWebApiに依存しない()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNamespace, WebApiNamespace)
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void アプリケーション層は永続化やWebの技術詳細に依存しない()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Dapper", "Microsoft.Data.Sqlite", "Microsoft.AspNetCore")
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void インフラ層はWebApiに依存しない()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOn(WebApiNamespace)
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void 検出器の健全性確認_インフラ層のDapper依存を検出できる()
    {
        // 依存検出が機能していることのポジティブコントロール。
        // これが失敗する場合、上記の「依存しない」系テストは空振りしている可能性がある。
        var result = Types.InAssembly(InfrastructureAssembly)
            .That().ResideInNamespace("CostManagement.Infrastructure.Repositories")
            .And().HaveNameEndingWith("Repository")
            .Should()
            .HaveDependencyOn("Dapper")
            .GetResult();

        AssertSuccessful(result);
    }
}

/// <summary>ポート&アダプタの配置ルール(リポジトリはポート=Domain、実装=Infrastructure)。</summary>
public class PortAndAdapterTests
{
    private static readonly Assembly DomainAssembly =
        typeof(Domain.Shared.DomainException).Assembly;

    private static readonly Assembly ApplicationAssembly = typeof(ProjectService).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(DependencyInjection).Assembly;

    private static IEnumerable<Type> RepositoryPorts() =>
        DomainAssembly.GetTypes()
            .Where(t => t.IsInterface && t.Name.EndsWith("Repository"));

    [Fact]
    public void リポジトリポートはドメイン層に定義されている()
    {
        // 7つの集約リポジトリ(Division/DivisionBudgetApproval/Department/Project/
        // CostElement/DepartmentBudget/ActualEntry)
        Assert.Equal(7, RepositoryPorts().Count());
    }

    [Fact]
    public void すべてのリポジトリポートにインフラ層の実装が存在する()
    {
        var infraTypes = InfrastructureAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .ToList();

        foreach (var port in RepositoryPorts())
        {
            var implementations = infraTypes.Where(port.IsAssignableFrom).ToList();
            Assert.True(implementations.Count == 1,
                $"{port.Name} の実装がインフラ層にちょうど1つ存在すべきですが {implementations.Count} 件でした。");
        }
    }

    [Fact]
    public void アプリケーション層にリポジトリ実装を置かない()
    {
        var offenders = ApplicationAssembly.GetTypes()
            .Where(t => t.IsClass && RepositoryPorts().Any(p => p.IsAssignableFrom(t)))
            .Select(t => t.FullName)
            .ToList();

        Assert.True(offenders.Count == 0,
            $"アプリケーション層にリポジトリ実装があります: {string.Join(", ", offenders)}");
    }
}

/// <summary>ドメインモデルの戦術パターンに関する規約。</summary>
public class DomainModelConventionTests
{
    private static readonly Assembly DomainAssembly =
        typeof(Domain.Shared.DomainException).Assembly;

    [Fact]
    public void ドメインの公開クラスはsealedである()
    {
        // 集約・エンティティ・ドメインサービス・レポートはすべて継承を前提としない。
        var offenders = DomainAssembly.GetTypes()
            .Where(t => t.IsClass && t.IsPublic && !t.IsAbstract && !t.IsSealed)
            .Select(t => t.FullName)
            .ToList();

        Assert.True(offenders.Count == 0,
            $"sealed でない公開クラスがあります: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void 値オブジェクトは不変な構造体である()
    {
        var valueObjects = new[]
        {
            typeof(Money),
            typeof(FiscalHalf),
            typeof(Domain.Divisions.DivisionId),
            typeof(Domain.Divisions.DivisionBudgetApprovalId),
            typeof(Domain.Departments.DepartmentId),
            typeof(Domain.Projects.ProjectId),
            typeof(Domain.Budgeting.DepartmentBudgetId),
            typeof(Domain.Actuals.ActualEntryId),
            typeof(Domain.CostElements.CostElementCode),
        };

        foreach (var vo in valueObjects)
        {
            Assert.True(vo.IsValueType, $"{vo.Name} は struct であるべきです。");
            Assert.True(
                vo.GetCustomAttributes().Any(a =>
                    a.GetType().Name == "IsReadOnlyAttribute"),
                $"{vo.Name} は readonly struct であるべきです。");
        }
    }

    [Fact]
    public void 集約の状態はドメインメソッド経由でのみ変更できる()
    {
        // すべての公開プロパティは set 不可(private set / init / get-only)であること。
        var aggregates = new[]
        {
            typeof(Domain.Divisions.Division),
            typeof(Domain.Divisions.DivisionBudgetApproval),
            typeof(Domain.Departments.Department),
            typeof(Domain.Projects.Project),
            typeof(Domain.Budgeting.DepartmentBudget),
            typeof(Domain.Budgeting.BudgetLine),
            typeof(Domain.Actuals.ActualEntry),
            typeof(Domain.CostElements.CostElement),
        };

        var offenders = aggregates
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(p => p.SetMethod is { IsPublic: true })
            .Select(p => $"{p.DeclaringType?.Name}.{p.Name}")
            .ToList();

        Assert.True(offenders.Count == 0,
            $"公開セッターを持つプロパティがあります: {string.Join(", ", offenders)}");
    }
}
