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
| frontend | npm ci + ESLint(`npm run lint`)+ Prettier(`npm run format:check`)+ tsc + vite build |
| frontend-unit | `tests/unit/frontend` で実行。npm ci(frontend 側は不要)→ ESLint・tsc・Prettier → Vitest(API は MSW でモック) |
| browser-e2e | `tests/e2e/browser` で実行。frontend と tests/e2e/browser の npm ci → E2E のステップ定義の ESLint・tsc・Prettier → Chromium(ヘッドレスシェルのみ。`~/.cache/ms-playwright` を Playwright の版をキーにキャッシュし、ないときだけ `npx playwright install --only-shell chromium`。OS の依存はランナーに揃っているので `--with-deps` は付けない。Issue #63)→ 日本語のフォント(`fonts-ipafont-gothic` のファイルを `~/.local/share/fonts/ja` にキャッシュし、ないときだけ apt で入れる。`fc-list :lang=ja` が空なら落とし、日本語を描いたときに Chromium が使ったフォントを CDP の `CSS.getPlatformFontsForNode` で聞いて IPA ゴシックでなければ落とす。中国語のフォントがあるとそちらが選ばれるので、IPA ゴシックだけを入れる。失敗時のトレースで日本語を □ にしないため。Issue #65)→ バックエンド+Vite自動起動 → E2E(`npm test` = bddgen + playwright test)。失敗時レポート保存 |
| metrics | コードメトリクス。cloc(言語別行数)+ scc(行数+複雑度)を backend/src・frontend/src・tests/unit・tests/integration・tests/e2e・tests/architecture の区分で実行サマリへ + `code-metrics` アーティファクト。scc は `go install github.com/boyter/scc/v3@latest`(ubuntu ランナーの Go を利用)。**Microsoft.CodeAnalysis.Metrics は Linux 不可 & 旧 Roslyn で現代 C# 非対応のため使わない** |
| mutation | ミューテーションテスト(Stryker.NET)。`tests` で `dotnet tool restore` → `unit/backend/Domain`・`integration/backend/Application` で `dotnet tool run dotnet-stryker`。markdown レポートを実行サマリへ + html を `mutation-report` アーティファクトへ。**非ブロッキング**(閾値 break:0 でスコアでは落とさない。Stryker 自体の失敗のみ赤)。設定は各テストプロジェクト直下 `stryker-config.json`、ツールは `tests/.config/dotnet-tools.json`。詳細は testing スキル(Issue #6) |

- **PR では、変えた場所に応じてジョブを飛ばす(#73)**。最初のジョブ「変えた場所の判定」が `node tools/factory/ci-scope-cli.mjs` で決め、各ジョブの `if:` で飛ばす。
  対応の正は `tools/factory/lib/ci-scope.mjs`(テストは `tools/factory/test/ci-scope.test.mjs`)。main への push ではすべて動かす。
  ワークフロー全体を `on.pull_request.paths` で絞らない(必須チェックが「待ち」のまま残ってマージできなくなる)。判定が失敗したら、すべて動かす側に倒す。
  判定のジョブ「変えた場所の判定(CI)」「変えた場所の判定(工場のゲート)」も必須チェックにする(`setup-github.sh` の `wanted`。名前が同じだと区別できないので分けている)

  | 変えた場所 | 動かすジョブ |
  |---|---|
  | `knowledge/`・`requirements/`(screens 以外)・`specs/`・`docs/`・`.factory/`・`*.md` だけ | なし(工場のゲートの検査だけ) |
  | Reqnroll の `.feature`(`tests/integration/`・`tests/e2e/api/`) | バックエンド |
  | `backend/`・`tests/unit/backend/`・`tests/integration/`・`tests/e2e/api/`・`tests/architecture/`・`.editorconfig` | バックエンド・ブラウザE2E・ミューテーション・メトリクス |
  | `frontend/` | フロントエンド・Vitest・ブラウザE2E・画面の差分・メトリクス |
  | `tests/unit/frontend/` | Vitest・メトリクス |
  | `tests/e2e/browser/` | ブラウザE2E |
  | `requirements/screens/`・`tests/e2e/screens/` | 画面の差分 |
  | `docs/api/openapi.json` | バックエンド・フロントエンド・画面の差分 |
  | それ以外(`.github/workflows/`・`tools/factory/`・`.claude/`・`docker/`・`infra/` など) | すべて |

  ジョブを足したら、`ci-scope.mjs` の `SCOPES` と対応、ジョブの `needs: changes` と `if:` も足す
- **依存の自動更新**: Dependabot(`.github/dependabot.yml`)が毎週月曜 09:00 JST に NuGet・npm・Actions・Docker を確認して PR を作る。
  マイナー・パッチはエコシステムごとに1件へまとめ、メジャーは個別 PR。PR は CI を通してからマージする
  (メジャー更新は破壊的変更の確認が必要。例: React・Vite・xUnit のメジャー)
- トリガ: push(main)と pull_request。README 冒頭に CI バッジあり
- **工場のゲート(`.github/workflows/factory.yml`)**: フックの確認、検査スクリプトのテスト、ゲートの検査 8 つ(matrix。中身は `tools/factory/cli.mjs`、一覧は `tools/factory/README.md`)。ゲートラベルを付けたあとは「ゲートラベルの確認」を再実行する(Issue のラベルでは PR の検査が動かないため)。
  「画面の差分(承認した画面との比較)」は `mcr.microsoft.com/playwright` のコンテナで動く(版は `tests/e2e/screens/package.json` の `@playwright/test` と同じにする。Playwright を上げるときは、このコンテナの版・`tools/factory/screens-docker.sh` もそろえ、人が承認した画面を撮り直す)。
  ジョブ名(`name:`)が main のルールセットの必須チェックになる。ジョブを足したら、その PR をマージしたあとで、
  人が `bash tools/factory/setup-github.sh` を実行して必須チェックに加える(main で動いたジョブだけが加わる)
- **プッシュしたら必ず `gh pr checks <番号> --watch` で結果を確認する**。実行は約1〜5分(ミューテーションが最も遅い)。
  失敗したら `gh run view <run-id> --log-failed`
- カバレッジは backend ジョブの Summary ページに表示。目標 85% 以上
- playwright パッケージはブラウザを postinstall でダウンロードするため、
  ブラウザ不要のジョブでは `PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD=1` を維持する

## 開発環境

- **devcontainer の守り(ADR-0015 フェーズ7)**: 起動時に `docker/init-firewall.sh` が外への通信を許可リストに絞る(`ALLOWED_DOMAINS`。GitHub は api.github.com/meta の IP の範囲)。sudo は `SUDO_POLICY=firewall-only` で、ファイアウォールと `devcontainer-fix-owner.sh` だけ。宛先や道具を足すのは工場の変更(`factory/` の PR)。docker compose のイメージは `SUDO_POLICY=full`(既定)のまま。許可リストにない宛先は即座に拒否される(`curl: (7) ... Connection refused` に似た失敗になる)ので、通信の失敗はまずこれを疑う
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
- 期間費用の明細名 `period_detail` 列の追加(`MigrateAddPeriodDetail`。Issue #7)。
  `department_budget_lines` は UNIQUE 制約に `period_detail` を含めるため**テーブルの作り直しが必要**。
  子テーブル `department_budget_line_months` が FK 参照するので、作り直しの間だけ `PRAGMA foreign_keys=OFF`
  (トランザクション外で設定)にし、**create-new → copy → drop-old → rename** で既存 id を保持する
  (id を保つので月別金額の子行はそのまま有効)。`actual_entries` は一意制約に関与しないため ALTER ADD COLUMN。
  既存行は `period_detail=''`(費目一括)として復元。判定は `period_detail` 列の有無で冪等に
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

- ブランチ: 工程別の命名(`knowledge/` `req/` `spec/` `impl/` `prep/` `factory/`。`AGENTS.md`)で main から切る。main へ直接プッシュしない(ルールセットとフックが止める)
- `git push -u origin <branch>`。ネットワークエラー時のみ指数バックオフで最大4回リトライ
- 工程の成果物は PR で出す(`.github/pull_request_template.md`)。**マージは人がする**
- プッシュと `gh pr create --base main` は別のコマンドにする。フックはコマンドの文字列で判定するので、
  コミットメッセージやヒアドキュメントに「git push」と「main」が両方あると止まることがある(本文はファイルに書いて渡す)
- 工場の設定(`.claude/`・`AGENTS.md`・`CLAUDE.md`・`.github/workflows/`・`tools/factory/`)を変えたら、変更の記録 `.factory/changes/<Issue番号>.md` を 1 ファイル足す(書き方は `.factory/changes/README.md`)。`.factory/CHANGELOG.md` の表には足さない(工場の PR が同時に開くと衝突するため。#66)
