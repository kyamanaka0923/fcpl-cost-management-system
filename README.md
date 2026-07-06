# 総合原価管理システム(予実管理)

総合原価計算における**原価・売上高の予算策定・実績計上・差異分析**を行うシステムです。

- 予算(予定)はプロジェクトごとに**バージョン管理**され、四半期などの節目で何度でも改定できます
- **売上予算**は原価予算とは独立にバージョン管理・改定できます(品目 × 年月 × 販売数量 × 販売単価)
- 予実の**差異分析**では、総差異を**価格差異**と**数量差異**に分解します(原価・売上とも)
- **損益(粗利)分析**: 売上 − 原価 = 粗利の予実比較と粗利率、月別内訳
- 予算バージョン間の**変動比較**(例: 当初予算 vs 第2四半期改定)ができます

## 技術スタック

| レイヤ | 技術 |
|---|---|
| フロントエンド | React 19 + TypeScript + Vite |
| バックエンド | .NET 10(ASP.NET Core Minimal API) |
| データベース | SQLite + Dapper |
| アーキテクチャ | ヘキサゴナルアーキテクチャ + ドメイン駆動設計(戦術パターン) |

## アーキテクチャ

ヘキサゴナル(ポート&アダプタ)構成。依存はすべて内側(Domain)に向かいます。

```
backend/
├── src/
│   ├── CostManagement.Domain/          # 中心: エンティティ・値オブジェクト・集約・
│   │   │                               #       ドメインサービス・リポジトリポート
│   │   ├── Shared/                     #   Money, AccountingPeriod(値オブジェクト)
│   │   ├── Projects/                   #   Project 集約
│   │   ├── CostElements/               #   CostElement(費目マスタ)集約
│   │   ├── Planning/                   #   CostPlan 集約(バージョン管理・承認ワークフロー)
│   │   ├── Actuals/                    #   ActualCost 集約
│   │   ├── Revenue/                    #   RevenuePlan / ActualRevenue 集約(売上)
│   │   └── Analysis/                   #   差異分析・バージョン比較のドメインサービス群
│   ├── CostManagement.Application/     # ユースケース(入力ポート)・DTO
│   ├── CostManagement.Infrastructure/  # 出力アダプタ: Dapper + SQLite リポジトリ実装
│   └── CostManagement.WebApi/          # 入力アダプタ: HTTP API(Minimal API)
└── tests/
    └── CostManagement.Domain.Tests/    # ドメイン単体テスト(xUnit)

frontend/                               # React SPA(/api を dev proxy 経由でバックエンドへ)
```

### ドメインモデル(DDD 戦術パターン)

| 要素 | 実装 |
|---|---|
| 集約ルート | `Project`, `CostPlan`, `ActualCost`, `CostElement`, `RevenuePlan`, `ActualRevenue` |
| エンティティ | `PlanLine`(CostPlan 集約内), `RevenuePlanLine`(RevenuePlan 集約内) |
| 値オブジェクト | `Money`, `AccountingPeriod`(yyyy-MM), `ProjectId` 等の型付き ID, `CostElementCode` |
| ドメインサービス | `VarianceAnalysisService` / `RevenueVarianceAnalysisService`(予実差異分析), `PlanComparisonService` / `RevenuePlanComparisonService`(バージョン間比較) |
| リポジトリ(ポート) | `IProjectRepository`, `ICostPlanRepository`, `IActualCostRepository`, `ICostElementRepository`, `IRevenuePlanRepository`, `IActualRevenueRepository` |
| ドメイン例外 | `DomainException`(不変条件違反 → HTTP 400 に変換) |

### 予算のライフサイクル

```
当初予算 v1 (Draft) ── 承認 ──> v1 (Approved)
                                    │ 四半期改定(明細を引き継いでドラフト起票)
                                    v
                         v2 (Draft) ── 承認 ──> v2 (Approved)
                                                 v1 は Superseded(履歴として保持)
```

主な不変条件:

- 承認済み・失効済みの予算は編集不可(改定版の作成が必要)
- 明細のない予算は承認不可
- 策定中のドラフトは同時に 1 つまで
- 同一プロジェクト内でバージョン番号は単調増加
- 新バージョンの承認により、旧承認版は自動的に失効(Superseded)

### 差異分析(変動分析)

差異は「実績 − 予算」で符号付き(**正 = 不利差異(予算超過)、負 = 有利差異**)。
総差異は次のとおり分解され、`価格差異 + 数量差異 = 総差異` が常に成立します。

```
価格差異 = (実際単価 − 予定単価) × 実際数量
数量差異 = (実際数量 − 予定数量) × 予定単価
```

同一(費目, 年月)に複数の実績がある場合は合算し、実際単価は加重平均で求めます。
予算にない実績は「予定外」として報告されます。

売上も同じ式で**販売価格差異**と**販売数量差異**に分解します(品目 × 年月単位)。
符号の解釈は原価と逆で、売上は「実績 > 予算」が有利差異です。
損益タブでは最新の承認済み売上予算・原価予算を突き合わせ、粗利(売上 − 原価)の
予実差異・粗利率・月別内訳を表示します。

## 実行方法

### バックエンド

```bash
cd backend
dotnet run --project src/CostManagement.WebApi
# => http://localhost:5100 (初回起動時に costmanagement.db を自動作成・費目マスタをシード)
```

### フロントエンド

```bash
cd frontend
npm install
npm run dev
# => http://localhost:5173 (/api は localhost:5100 にプロキシ)
```

### テスト

```bash
cd backend
dotnet test
```

## API 概要

| メソッド/パス | 説明 |
|---|---|
| `GET/POST /api/projects` | プロジェクト一覧・作成 |
| `POST /api/projects/{id}/complete` | プロジェクト完了 |
| `GET/POST /api/cost-elements` | 費目マスタ一覧・追加 |
| `GET/POST /api/projects/{id}/plans` | 予算バージョン一覧・ドラフト起票(初回は当初予算、以降は改定版) |
| `GET /api/plans/{planId}` | 予算詳細(明細含む) |
| `PUT/DELETE /api/plans/{planId}/lines` | 予算明細の登録(upsert)・削除 |
| `POST /api/plans/{planId}/approve` | 予算承認(旧承認版は自動失効) |
| `GET/POST /api/projects/{id}/actuals` | 実績一覧・計上 |
| `DELETE /api/actuals/{actualId}` | 実績取消 |
| `GET/POST /api/projects/{id}/revenue-plans` | 売上予算バージョン一覧・ドラフト起票 |
| `GET /api/revenue-plans/{planId}` | 売上予算詳細(明細含む) |
| `PUT/DELETE /api/revenue-plans/{planId}/lines` | 売上予算明細の登録(upsert)・削除 |
| `POST /api/revenue-plans/{planId}/approve` | 売上予算承認(旧承認版は自動失効) |
| `GET/POST /api/projects/{id}/actual-revenues` | 売上実績一覧・計上 |
| `DELETE /api/actual-revenues/{actualId}` | 売上実績取消 |
| `GET /api/projects/{id}/variance?planId=&from=&to=` | 原価の予実差異分析(バージョン・期間指定可) |
| `GET /api/projects/{id}/revenue-variance?planId=&from=&to=` | 売上の予実差異分析 |
| `GET /api/projects/{id}/plan-comparison?baseVersion=&targetVersion=` | 原価予算バージョン間比較 |
| `GET /api/projects/{id}/revenue-plan-comparison?baseVersion=&targetVersion=` | 売上予算バージョン間比較 |
| `GET /api/projects/{id}/profit?from=&to=` | 損益(粗利)予実サマリ |

## 画面

- **プロジェクト一覧 / 詳細** — プロジェクト登録、原価・売上それぞれの予算バージョンの一覧・改定・承認
- **予算編集** — 原価予算(費目 × 年月 × 数量 × 単価)/ 売上予算(品目 × 年月 × 販売数量 × 販売単価)のドラフト明細編集
- **実績入力** — 原価実績・売上実績の計上(同一キーへの複数計上に対応)
- **予実差異分析・損益** — タブ構成: 原価差異 / 売上差異 / 損益(粗利)。月別チャート、費目・品目別差異チャート、価格・数量差異の分解テーブル、粗利の予実と月別内訳
- **予算バージョン比較** — 原価予算・売上予算それぞれで任意の 2 バージョン間の増減を明細単位で比較
