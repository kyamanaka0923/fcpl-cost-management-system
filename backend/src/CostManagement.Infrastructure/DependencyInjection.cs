using CostManagement.Application;
using CostManagement.Application.Common;
using CostManagement.Domain.Actuals;
using CostManagement.Domain.Analysis;
using CostManagement.Domain.CostElements;
using CostManagement.Domain.Planning;
using CostManagement.Domain.Projects;
using CostManagement.Domain.Revenue;
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
        services.AddSingleton<IProjectRepository, ProjectRepository>();
        services.AddSingleton<ICostElementRepository, CostElementRepository>();
        services.AddSingleton<ICostPlanRepository, CostPlanRepository>();
        services.AddSingleton<IActualCostRepository, ActualCostRepository>();
        services.AddSingleton<IRevenuePlanRepository, RevenuePlanRepository>();
        services.AddSingleton<IActualRevenueRepository, ActualRevenueRepository>();
        services.AddSingleton<ISystemClock, SystemClock>();

        // ドメインサービス
        services.AddSingleton<VarianceAnalysisService>();
        services.AddSingleton<PlanComparisonService>();
        services.AddSingleton<RevenueVarianceAnalysisService>();
        services.AddSingleton<RevenuePlanComparisonService>();
        services.AddSingleton<ProfitAnalysisService>();

        // ユースケース(アプリケーションサービス)
        services.AddSingleton<ProjectService>();
        services.AddSingleton<CostElementService>();
        services.AddSingleton<CostPlanService>();
        services.AddSingleton<ActualCostService>();
        services.AddSingleton<RevenuePlanService>();
        services.AddSingleton<ActualRevenueService>();
        services.AddSingleton<AnalysisService>();

        return services;
    }
}
