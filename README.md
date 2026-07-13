# 総合原価管理システム(予実管理)

[![CI](https://github.com/kyamanaka0923/fcpl-cost-management-system/actions/workflows/ci.yml/badge.svg)](https://github.com/kyamanaka0923/fcpl-cost-management-system/actions/workflows/ci.yml)

**課(部門)単位の半期予算の策定・実績計上・差異分析**を行うシステムです。
明細は数量×単価ではなく**金額**で直接管理します。

- 組織は **部 > 課** の2階層です。課は必ず1つの部に属し、**部では配下課の予算・予実を合計**して把握できます
  (部自体は予算を策定しない集計ビュー)。部詳細は「予算(計画)」タブ(区分別の予算・計画損益・粗利率と、
  課ごとの予算比較グリッド)と「予実サマリ」タブ(予実の差異)に分かれ、どの課の損益が弱いかを比較できます
- 課の承認に加えて**部単位の予算承認**ができます。部承認は配下課がすべて承認済みになると可能で、
  課の承認とは独立(部承認後に課が改定しても部承認は残る)。取り消しもできます
- 予算は**課 × 半期(年度の上期/下期)**ごとに策定し、**バージョン管理**され、半期の途中でも何度でも改定できます
- 予算は **売上高・加工費・外注費・期間費用の4区分**を1つの予算としてまとめて承認します
- **売上高・加工費・外注費は案件別の詳細計画**として立案します。課の区分合計 = 案件明細の合計
  (課レベルの直接入力はできません)
- **期間費用**は費目別(人件費・ライセンス費など。費目マスタで拡張可能)に計画します
- 明細は**半期一括の金額**です(年月の粒度はありません)
- 予実の**差異分析**: 差異 = 実績金額 − 予算金額(コストは超過が不利、売上は超過が有利)
- **損益分析**: 課全体(売上高 − 総コスト)と案件別(売上高 − 加工費 − 外注費。期間費用は課共通)。
  計画損益・損益実績には**粗利率(損益 ÷ 売上高)**を併記します
- 予算バージョン間の**変動比較**(例: 当初予算 vs 上期見直し)ができます

操作手順は **[操作マニュアル](docs/MANUAL.md)**(計画策定 → 実績入力 → 計画変更)、
設計の詳細は **[設計ドキュメント](docs/DESIGN.md)**(コンテキストマップ / C4 モデル / クラス図)を参照してください。

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
│   │   ├── Shared/                     #   Money, FiscalHalf(値オブジェクト)
│   │   ├── Divisions/                  #   Division(部)集約
│   │   ├── Departments/                #   Department(課。部に属する)集約
│   │   ├── Projects/                   #   Project(案件。課に属するマスタ)集約
│   │   ├── CostElements/               #   CostElement(期間費用の費目マスタ)集約
│   │   ├── Budgeting/                  #   DepartmentBudget 集約(4区分・バージョン管理・承認)
│   │   ├── Actuals/                    #   ActualEntry 集約(実績)
│   │   └── Analysis/                   #   差異分析・バージョン比較・損益・部集計のドメインサービス群
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
| 集約ルート | `Division`(部), `DivisionBudgetApproval`(部承認), `Department`(課), `Project`(案件), `DepartmentBudget`(課予算), `ActualEntry`(実績), `CostElement`(費目マスタ) |
| エンティティ | `BudgetLine`(区分 × 案件 or 費目 × 金額。案件系区分と期間費用でキーが排他) |
| 値オブジェクト | `Money`, `FiscalHalf`(yyyy-H1 / yyyy-H2), `DivisionId` / `DepartmentId` 等の型付き ID, `CostElementCode` |
| ドメインサービス | `BudgetVarianceAnalysisService`(予実差異分析), `BudgetComparisonService`(バージョン間比較), `ProfitAnalysisService`(課全体・案件別の損益), `DivisionBudgetSummaryService`(部の予実集計) |
| リポジトリ(ポート) | `IDivisionRepository`, `IDivisionBudgetApprovalRepository`, `IDepartmentRepository`, `IProjectRepository`, `ICostElementRepository`, `IDepartmentBudgetRepository`, `IActualEntryRepository` |
| ドメイン例外 | `DomainException`(不変条件違反 → HTTP 400 に変換) |

### 予算の構成と区分

課の半期予算は次の4区分で構成され、1つの予算としてまとめて承認します。

| 区分 | 明細のキー | 意味 |
|---|---|---|
| 売上高(Revenue) | 案件 | 案件ごとの売上計画 |
| 加工費(Processing) | 案件 | 案件ごとの加工費計画 |
| 外注費(Outsourcing) | 案件 | 案件ごとの外注費計画 |
| 期間費用(PeriodCost) | 費目 | 課共通の費用(人件費・ライセンス費など) |

課の区分合計は常に明細の合計として導出されます(ヘッダに金額を持たないため、
案件明細と課合計が食い違うことは構造的に起こりません)。

### 予算のライフサイクル

```
当初予算 v1 (Draft) ── 承認 ──> v1 (Approved)
                                    │ 見直し(明細を引き継いでドラフト起票)
                                    v
                         v2 (Draft) ── 承認 ──> v2 (Approved)
                                                 v1 は Superseded(履歴として保持)
```

主な不変条件:

- 承認済み・失効済みの予算は編集不可(改定版の作成が必要)
- 明細のない予算は承認不可
- 策定中のドラフトは同一(課, 半期)に 1 つまで
- 同一(課, 半期)内でバージョン番号は単調増加
- 新バージョンの承認により、旧承認版は自動的に失効(Superseded)
- 売上高・加工費・外注費の明細は案件必須(その課に属する案件のみ)、期間費用の明細は費目必須

### 差異分析(変動分析)

明細は金額で管理されるため、差異は「実績金額 − 予算金額」で符号付きに算出します。

- **コスト(加工費・外注費・期間費用)**: (区分, 案件 or 費目) の粒度で突き合わせ。正 = 予算超過 = 不利差異
- **売上高**: (案件) の粒度で突き合わせ。正 = 売上超過 = 有利差異(コストと逆)

同一キーに複数の実績がある場合は合算されます。予算にない実績は「予定外」として報告されます。

### 損益分析

`ProfitAnalysisService` が課全体と案件別の損益予実を算出します。

- **課全体の損益** = 売上高 −(加工費 + 外注費 + 期間費用)。利益率(損益 ÷ 売上高)も算出
- **案件別の損益** = 売上高 − 加工費 − 外注費。期間費用は課共通のため案件には配賦しません
- 案件別損益の合計 − 期間費用 = 課全体の損益(整合性はテストで担保)

### 管理できる費目(費目マスタ)

費目マスタは**期間費用**の明細に使う、**システム全体で共通のマスタ**です(課ごとの設定ではありません)。
専用の「**費目マスタ**」画面(画面右上のナビ / `/cost-elements`)で追加・一覧でき、
追加した費目はすべての部・課の予算編集・実績入力で共通して使えます。初回起動時に以下の標準費目がシードされます。

| コード | 費目名 |
|---|---|
| `PERSONNEL` | 人件費 |
| `LICENSE` | ライセンス費 |

費目は `POST /api/cost-elements` で追加できます(コード・名称を指定。コードは重複不可)。
シードは `INSERT OR IGNORE` のため、追加・既存データに影響しません。

### 案件(Project)コードの一意性と編集

案件は課に属するマスタで、**案件コードは課ごとに一意**です。別の課であれば同じ案件コードを使えます。

案件のコード・名称は**登録後も編集**できます(予算編集画面の案件行の「編集」)。案件の同一性は
**生成時に採番される GUID(`ProjectId`)**で保たれ、予算明細・実績はこの GUID で案件を参照するため、
コードや名称を変更しても既存の予算・実績の紐づけは壊れません。編集時も課ごとのコード一意性は検証されます。

### スキーマの作り直し

旧世代(プロジェクト単位予算)のテーブルが残っているデータベースは、起動時に旧テーブルを
破棄して新スキーマで作り直します(**データ移行は行いません**)。部を持たない旧世代の
`departments`(`division_id` 列がない)も、配下テーブル(projects / department_budgets /
department_budget_lines / actual_entries)ごと破棄して作り直します。
この処理は冪等で、新スキーマの既存データには影響しません。

一方、現世代スキーマ内での変更(既存データが衝突しないもの)は、既存の案件データを**保持したまま**
`projects` テーブルを作り直すマイグレーションを起動時に実行します(冪等で、移行済みなら何もしません)。
現在は次の2つを実行します:

- **案件コードの一意制約を「グローバル一意」→「課ごとに一意」へ変更**(制約の緩和のみ)
- **案件の終了ステータス(`status` 列)を廃止**(案件は終了の概念を持たなくなったため。Issue #2)

## 実行方法

### Docker / VS Code Dev Containers(推奨)

ローカルに .NET SDK / Node.js を入れなくても、Docker だけでビルド・テスト・起動できます
(開発用イメージ `docker/dev.Dockerfile` = .NET 10 SDK + Node.js 22 を共用)。

```bash
# アプリ起動(バックエンド http://localhost:5100 / フロントエンド http://localhost:5173)
docker compose up backend frontend

# バックエンドのテスト
docker compose run --rm backend-test

# フロントエンドの型チェック + ビルド
docker compose run --rm frontend-build
```

VS Code でのリモート開発は、拡張機能「Dev Containers」を入れてリポジトリを開き、
「Reopen in Container」を選ぶだけです(`.devcontainer/devcontainer.json` が使われます)。
コンテナ内で `dotnet restore` / `npm install` が自動実行され、ポート 5100/5173 が
フォワードされます。ビルド・テストは `.vscode/tasks.json` のタスク
(`backend: test`、`full: backend + frontend` など)から実行できます。
デバッグ実行は `.vscode/launch.json` の構成(`full: backend デバッグ + frontend` など)を使います。

### バックエンド(ローカルに SDK がある場合)

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

テストは5種類あります(テスト名はすべて日本語で、テスト一覧が仕様書として読めるようにしています)。

- **ドメイン単体テスト**(`tests/CostManagement.Domain.Tests`) — 集約の不変条件、差異・損益の計算規則
- **アプリケーション層テスト**(`tests/CostManagement.Application.Tests`) — ユースケース単位の検証。
  本物のリポジトリ実装+一時 SQLite を使い、予算の策定→改定→承認、実績計上、分析の業務ルールを確認
- **インフラ層テスト**(`tests/CostManagement.Infrastructure.Tests`) — リポジトリの永続化往復
  (保存した集約が同じ状態で復元されること)と、旧スキーマの破棄・作り直しの冪等性の検証
- **API E2E テスト**(`tests/CostManagement.E2E.Tests`) — WebApplicationFactory で WebApi を
  まるごと起動し、計画策定→実績入力→分析→計画変更の一連の業務フローを HTTP 経由で検証
- **アーキテクチャテスト**(`tests/CostManagement.Architecture.Tests`) — NetArchTest による
  ヘキサゴナルアーキテクチャの依存ルール検証(層依存・ポート&アダプタ配置・ドメイン規約)

さらに**ブラウザ E2E テスト**(Playwright)が `frontend/e2e/` にあります。
バックエンドとフロントエンドを自動起動し、実際のブラウザで計画策定→実績入力→分析→計画変更を操作します。

```bash
cd frontend
npx playwright install chromium   # 初回のみ
npm run test:e2e
```

**カバレッジ**: CI(GitHub Actions)がテスト実行時にカバレッジを計測し、
各実行の **Summary ページにアセンブリ別のカバレッジ表**を表示します。
詳細な HTML レポートは実行のアーティファクト `coverage-report` からダウンロードできます。
ローカルでは `dotnet test --collect:"XPlat Code Coverage"` で計測できます。

## AWS へのデプロイ(サーバーレス構成)

EC2・コンテナを使わず費用を抑えたサーバーレス構成の IaC(AWS SAM)を `infra/` に用意しています。

- **バックエンド**: .NET 10 を **Lambda**(`provided.al2023` カスタムランタイム, arm64)で実行(コンテナ不使用)
- **DB**: SQLite ファイルを **EFS** に永続化(予約同時実行=1 で書き込み直列化)
- **フロント**: React 静的ビルドを **S3 + CloudFront**。CloudFront が `/api/*` を Lambda に振り分け同一ドメイン化

```bash
cd infra && ./deploy.sh     # 詳細・コスト目安・注意点は infra/README.md
```

## API 概要

半期は `fiscalHalf=2026-H1`(上期)/ `fiscalHalf=2026-H2`(下期)の形式で指定します。

| メソッド/パス | 説明 |
|---|---|
| `GET/POST /api/divisions` | 部一覧・登録 |
| `GET /api/divisions/{id}` | 部の取得 |
| `GET /api/divisions/{id}/budget-summary?fiscalHalf=` | 部の予算・予実サマリ(配下課の予算/実績/差異・損益の合計 + 課別内訳(課ごとの区分別内訳を含む) + 部承認状態) |
| `POST/DELETE /api/divisions/{id}/budget-approval?fiscalHalf=` | 部予算の承認・取り消し(配下課が全承認済みで承認可能) |
| `GET/POST /api/divisions/{id}/departments` | 配下課一覧・課の登録(課は部に属する) |
| `GET /api/departments/{id}` | 課の取得 |
| `GET/POST /api/departments/{id}/projects` | 案件一覧・登録(案件は課に属する) |
| `GET /api/projects/{id}` | 案件の取得 |
| `POST /api/projects/{id}/complete` | 案件終了 |
| `GET/POST /api/cost-elements` | 費目マスタ一覧・追加(期間費用用) |
| `GET/POST /api/departments/{id}/budgets?fiscalHalf=` | 予算バージョン一覧・ドラフト起票(初回は当初予算、以降は改定版) |
| `GET /api/budgets/{budgetId}` | 予算詳細(明細・4区分合計含む) |
| `PUT/DELETE /api/budgets/{budgetId}/lines` | 予算明細の登録(upsert)・削除(案件別 or 費目別) |
| `POST /api/budgets/{budgetId}/approve` | 予算承認(同一課・半期の旧承認版は自動失効) |
| `GET/POST /api/departments/{id}/actuals?fiscalHalf=` | 実績一覧・計上 |
| `DELETE /api/actuals/{actualId}` | 実績取消 |
| `GET /api/departments/{id}/variance?fiscalHalf=&budgetId=` | 予実差異分析(区分別+案件/費目内訳。バージョン指定可) |
| `GET /api/departments/{id}/budget-comparison?fiscalHalf=&baseVersion=&targetVersion=` | 予算バージョン間比較(区分別) |
| `GET /api/departments/{id}/profit?fiscalHalf=&budgetId=` | 損益予実サマリ(課全体・案件別・期間費用) |

## 画面

- **部一覧 / 部詳細** — 部の登録、対象半期の選択、配下課の予実サマリ(区分別の予算/実績/差異 +
  損益)と課別内訳(未策定の課は明示)、課の登録、各課へのドリルダウン、
  **部予算の承認・取り消し**(配下課が全承認済みのとき承認可能)
- **課詳細** — 対象半期の選択、承認済み予算の4区分サマリ、予算バージョンの一覧・改定・承認
- **予算編集** — 案件×区分のグリッドで売上高・加工費・外注費を案件別に直接入力
  (セルを離れると自動保存)。期間費用も費目別の表に同様に直接入力する。
  案件の追加・終了もこの画面で行う(案件コードは課ごとに一意)。
  期間費用の費目は選択のみで、追加は「費目マスタ」画面で行う。
  区分合計・計画損益はサマリタイルに自動反映
- **費目マスタ** — システム共通の期間費用の費目マスタ(一覧・追加)。画面右上のナビからアクセス
- **実績入力** — 区分に応じて案件または費目を選んで計上(同一キーへの複数計上に対応)
- **予実差異分析・損益** — タブ構成: 予実差異(区分別サブトータル+案件/費目別明細、
  区分別チャート)/ 損益(課全体サマリ+案件別損益テーブル+期間費用(課共通)行+利益率)
- **予算バージョン比較** — 同一課・半期の任意の 2 バージョン間の増減を区分別・明細単位で比較
