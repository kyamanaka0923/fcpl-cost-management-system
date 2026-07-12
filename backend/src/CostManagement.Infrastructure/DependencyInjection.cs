using CostManagement.Application;
using CostManagement.Application.Common;
using CostManagement.Domain.Actuals;
using CostManagement.Domain.Analysis;
using CostManagement.Domain.Budgeting;
using CostManagement.Domain.CostElements;
using CostManagement.Domain.Departments;
using CostManagement.Domain.Divisions;
using CostManagement.Domain.Projects;
using CostManagement.Infrastructure.Persistence;
using CostManagement.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace CostManagement.Infrastructure;

public sealed class SystemClock : ISystemClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

public static class DependencyInjection
{
    /// <summary>ヘキサゴナルアーキテクチャの出力アダプタ(永続化)と入力ポート(ユースケース)を登録する。</summary>
    public static IServiceCollection AddCostManagement(this IServiceCollection services,
        string connectionString)
    {
        // 永続化アダプタ
        services.AddSingleton(new SqliteConnectionFactory(connectionString));
        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<IDivisionRepository, DivisionRepository>();
        services.AddSingleton<IDivisionBudgetApprovalRepository, DivisionBudgetApprovalRepository>();
        services.AddSingleton<IDepartmentRepository, DepartmentRepository>();
        services.AddSingleton<IProjectRepository, ProjectRepository>();
        services.AddSingleton<ICostElementRepository, CostElementRepository>();
        services.AddSingleton<IDepartmentBudgetRepository, DepartmentBudgetRepository>();
        services.AddSingleton<IActualEntryRepository, ActualEntryRepository>();
        services.AddSingleton<ISystemClock, SystemClock>();

        // ドメインサービス
        services.AddSingleton<BudgetVarianceAnalysisService>();
        services.AddSingleton<BudgetComparisonService>();
        services.AddSingleton<ProfitAnalysisService>();
        services.AddSingleton<DivisionBudgetSummaryService>();

        // ユースケース(アプリケーションサービス)
        services.AddSingleton<DivisionService>();
        services.AddSingleton<DivisionBudgetApprovalService>();
        services.AddSingleton<DepartmentService>();
        services.AddSingleton<ProjectService>();
        services.AddSingleton<CostElementService>();
        services.AddSingleton<DepartmentBudgetService>();
        services.AddSingleton<ActualEntryService>();
        services.AddSingleton<AnalysisService>();

        return services;
    }
}
