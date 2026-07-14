---
name: ci-and-env
description: CI(GitHub Actions)・開発環境(Docker/Dev Container/リモート環境)・スキーマ移行・リリース検証の手順。CIの修正、環境構築、プッシュ後の確認、DBスキーマ変更を伴う作業で必ず読む。
model: claude-sonnet-5
---

# CI・環境スキル

**利用モデル**: `claude-sonnet-5`。CI設定の微修正やジョブ結果確認だけなら `claude-haiku-4-5` で可。

## CI(.github/workflows/ci.yml)の構成

| ジョブ | 内容 |
|---|---|
| backend | dotnet test + dorny/test-reporter(Checks)+ ReportGenerator でカバレッジを実行サマリへ + HTML アーティファクト |
| frontend | npm ci + tsc + vite build |
| browser-e2e | Playwright(Chromium導入→バックエンド+Vite自動起動→E2E)。失敗時レポート保存 |
| metrics | コードメトリクス。cloc(言語別行数)+ scc(行数+複雑度)を backend/src・backend/tests・frontend/src の3区分で実行サマリへ + `code-metrics` アーティファクト。scc は `go install github.com/boyter/scc/v3@latest`(ubuntu ランナーの Go を利用)。**Microsoft.CodeAnalysis.Metrics は Linux 不可 & 旧 Roslyn で現代 C# 非対応のため使わない** |

- トリガ: push(main, claude/**)と pull_request。README 冒頭に CI バッジあり
- **プッシュしたら必ず GitHub MCP(`actions_list`/`actions_get`)で結果を確認する**。
  実行は約1〜2分。認証なし curl は private リポジトリに使えない(404になる)
- カバレッジは backend ジョブの Summary ページに表示。目標 85% 以上
- playwright パッケージはブラウザを postinstall でダウンロードするため、
  ブラウザ不要のジョブでは `PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD=1` を維持する

## 開発環境

- **ローカル**: `docker compose up backend frontend` / `docker compose run --rm backend-test`。
  VS Code は Dev Containers(`.devcontainer/`、イメージは `docker/dev.Dockerfile` を共用、
  非rootユーザー vscode、NuGet/node_modules は named volume)
- **Claude リモート環境の注意**:
  - .NET 10 SDK は apt(`dotnet-sdk-10.0`)で入れる。builds.dotnet.microsoft.com はプロキシで遮断される
  - localhost への curl は `--noproxy localhost` を付ける
  - Chromium は `/opt/pw-browsers/chromium`(Playwright は `PW_CHROMIUM` で executablePath 上書き)
  - Docker CLI はあるがデーモンは動かない(compose の実検証はローカルPCでのみ可能)
- SQLitePCLRaw の脆弱性警告が出たら `SQLitePCLRaw.bundle_e_sqlite3` を明示参照で更新する

## DBスキーマ変更の手順(重要)

方針: 旧世代スキーマからの**データ移行は行わない**(2026-07-11 の課×半期再構築で決定)。
旧世代のテーブルは起動時に破棄して新スキーマで作り直す(`DropLegacyTables`)。

例外: **現世代スキーマ内での変更**(制約緩和や不要カラム削除など、既存データが衝突しない変更)は、
データを保持したままテーブルを作り直すマイグレーションを行ってよい。RENAME→CREATE→INSERT SELECT→DROP を
トランザクションで、必ず冪等に。例(いずれも `projects`、順に適用):
- 案件コードの一意制約をグローバル→課ごとへ変更(`MigrateProjectCodeUniqueness`。Issue #1)。
  検出は `sqlite_master.sql` に `UNIQUE (department_id, code)` が含まれるかで判定
- 案件の終了ステータス `status` 列の廃止(`MigrateDropProjectStatus`。Issue #2)。
  検出は `pragma_table_info` に `status` 列が残っているかで判定
- 実績への月次計上用 `month` 列の追加(`MigrateAddActualMonth`。Issue #5)。
  `pragma_table_info` に `month` 列が無ければ `ALTER TABLE actual_entries ADD COLUMN month INTEGER NULL`。
  **単純なカラム追加は RENAME→CREATE→INSERT の作り直しではなく ALTER ADD COLUMN でよい**
- 新規テーブル(`department_budget_line_months` = 明細の月別金額)は移行不要。
  `CREATE TABLE IF NOT EXISTS` で足りる(既存DBには空テーブルが増えるだけ)
複数の移行が連なる場合、後段は前段の結果に対して冪等(該当列/制約がなければ何もしない)であること。

1. `DatabaseInitializer` の CREATE TABLE を新スキーマに更新
2. 同名テーブルの構造が変わる場合は `DropLegacyTables` に判定を追加:
   pragma_table_info で**旧世代にしかないカラム**を検出したら DROP(旧世代専用テーブルは
   無条件 `DROP TABLE IF EXISTS`、子 → 親の順)
3. 破棄処理は**冪等**にする(2回実行しても壊れない。新スキーマの既存データには触れない)
4. `Infrastructure.Tests` の SchemaResetTests にテストを追加(旧スキーマを生SQLで再現 →
   Initialize → 旧テーブル消滅・新スキーマ・シード確認、冪等性、新スキーマデータの保持)
5. README の「スキーマの作り直し」節を更新

## プッシュ運用

- ブランチ: `claude/cost-management-system-qvc368`(main へ直接プッシュしない)
- `git push -u origin <branch>`。ネットワークエラー時のみ指数バックオフで最大4回リトライ
- PR は明示的に依頼されたときだけ作成する
