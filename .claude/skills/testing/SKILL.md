---
name: testing
description: テストの追加・修正・実行の規約。tests/ のスコープ別構成(unit/integration/e2e/architecture)、バックエンドテスト(ドメイン/アプリケーション/インフラ/API E2E/アーキテクチャ)、フロントエンド単独テスト(Vitest + MSW)、ブラウザE2E(Playwright + playwright-bdd)・日本語 Gherkin シナリオの書き方、日本語テスト名の方針、既知の落とし穴。テストを書く・直す作業で必ず読む。
model: claude-sonnet-5
---

# テストスキル

**利用モデル**: `claude-sonnet-5`。テスト戦略自体の再設計は `claude-opus-4-8`。

## 大原則: テスト名は日本語の文にする

テスト一覧が仕様書として読めることを最優先する。
例: `改定版を承認すると旧バージョンは自動的に失効する()`。
クラス名も業務概念(`原価予算の策定と改定` 等)。ヘルパ名も日本語可(`承認済み原価予算を作成`)。

## テストの種類と置き場所

テストはすべてリポジトリ直下の `tests/` に集約し、**テストスコープで区切る**(`backend/` と `frontend/` には本体のコードだけを置く)。
スコープの下で技術(backend / frontend / api / browser)を分ける。

| スコープ | 何を通すか | 速さ |
|---|---|---|
| `tests/unit/` | 1つの部品だけ。DB・HTTP・ブラウザを使わない(フロントは jsdom + MSW) | 速い |
| `tests/integration/` | 本物の SQLite を通す(ユースケース・リポジトリ) | 中 |
| `tests/e2e/` | システムを外側から操作する(API は HTTP、ブラウザは画面→API→DB) | 遅い |
| `tests/architecture/` | コードの構造(層の依存ルール)。スコープの軸に乗らないので別枠 | 速い |

**どこに書くか迷ったら、下のスコープから検討する**(速いテストで確かめられることを遅いテストに書かない)。
画面だけの振る舞い(折りたたみ・入力チェック・送るリクエストの中身・トースト)は `unit/frontend`、
業務ルールは `integration/backend/Application`(Gherkin)、画面→API→DB のつながりだけを `e2e/browser` に書く。

| 置き場所 | プロジェクト | 対象 | 方式 |
|---|---|---|---|
| `tests/unit/backend/Domain` | CostManagement.Domain.Tests | 不変条件・計算規則 | 純粋単体 |
| `tests/unit/frontend` | (npm パッケージ) | 画面のロジックとコンポーネント | Vitest + Testing Library + MSW(API のモック) |
| `tests/integration/backend/Application` | CostManagement.Application.Tests | ユースケース | `UseCaseFixture`(一時SQLite+本物リポジトリ+`FixedClock`。日本語ヘルパ: `課を作成`/`案件を作成`/`承認済み予算を作成`) |
| `tests/integration/backend/Infrastructure` | CostManagement.Infrastructure.Tests | 永続化往復・スキーマ作り直し | `RepositoryFixture`(一時SQLite)+ `SchemaResetTests` |
| `tests/e2e/api` | CostManagement.E2E.Tests | HTTP経由の業務フロー | `WebApplicationFactory<Program>`(`ApiFixture`)+ Gherkin(Reqnroll) |
| `tests/architecture/backend` | CostManagement.Architecture.Tests | 依存ルール | NetArchTest |
| `tests/e2e/browser` | (npm パッケージ) | ブラウザ操作 | Gherkin(playwright-bdd)+ @playwright/test(webServerで両サーバ自動起動) |

**各プロジェクトの中は同じ型で並べる**(新しいファイルもこの型に従って置く):

| 置き場所 | 中身 | 名前空間 |
|---|---|---|
| 直下 | xUnit の例示テスト(`*Tests.cs`) | `CostManagement.X.Tests` |
| `PropertyBased/` | CsCheck の性質テストと生成器 | `….PropertyBased` |
| `Features/` | Gherkin(`*.feature`) | — |
| `Steps/` | ステップ定義 | `….Steps` |
| `Support/` | フィクスチャ・シナリオの状態などの共通部品(`UseCaseFixture`・`業務コンテキスト`・`ApiFixture`・`認証ありのApi`・`APIコンテキスト`) | `….Support`(csproj のグローバル using で全テストから見える) |

ブラウザ(`tests/e2e/browser`)は Node の慣習どおり小文字で `features/`・`steps/`・`support/`。
フロント単独(`tests/unit/frontend`)は `logic/`(純粋関数)・`components/`・`pages/<画面名>/<観点>.test.tsx`・`support/`。

- テストプロジェクトは `backend/` の外にあるので、`tests/Directory.Build.props` が
  `backend/Directory.Build.props`(アナライザ・警告のエラー扱い)を取り込む。`.editorconfig` のテスト向け緩和は
  `[tests/**.cs]`。ソリューション `backend/CostManagement.slnx` が `../tests/...` の各テストプロジェクトを参照するので
  `cd backend && dotnet test` で全テストが走る
- `tests/e2e/browser` と `tests/unit/frontend` は独立した npm パッケージ(`frontend/node_modules` は `tests/` から解決できないため)。
  ブラウザ E2E は画面を `frontend` の Vite 開発サーバ(`webServer` の `cwd: '../../../frontend'`)で起動するので、`frontend` 側の `npm ci` も必要

### フロントエンド単独テスト(tests/unit/frontend)

- テストからは `@app/...` で `frontend/src` を参照する(vitest.config.ts の alias と tsconfig の paths)
- **React の二重読み込みに注意**: `frontend/src` の画面とテスト側の Testing Library が別々の React を読むと Hooks が壊れる。
  vitest.config.ts の `resolve.dedupe`(react / react-dom / react-router-dom)と tsconfig の `paths` で、
  このパッケージの1つの React にそろえている。そのため **react などの版は frontend と同じに保つ**(Dependabot は3パッケージを監視)
- API は MSW でモックする。決めていない API が呼ばれるとテストが失敗する(`onUnhandledRequest: 'error'`)。
  予算編集画面は `support/fakeBudgetApi.ts` の `偽の予算API`(明細の登録で合計を計算し直し、混在禁止・承認済みの編集禁止も拒否する
  メモリ上の偽バックエンド)を使い、保存後の再描画と「送ったリクエストの中身・順序」(`fake.writes`)を確かめる
- 画面のコードは `fetch('/api/...')` と相対 URL で呼ぶため、`support/setup.ts` が jsdom のページ URL を基準に絶対 URL へ直す
- テスト対象の計算は画面から純粋関数に切り出す(例: `frontend/src/budgetLines.ts` の `cellAction`・`monthlyTotal`)。
  境界値は `test.each` の表で並べる。実行日で変わる処理は `vi.setSystemTime` で日付を固定する(TZ は Asia/Tokyo)
- 純粋関数のテストは先頭に `// @vitest-environment node`(jsdom を用意しない分速い)。入力操作は `userEvent.setup({ delay: null })`
- Vitest は3系(frontend の vite 6・@vitejs/plugin-react 4 に合わせる)。MSW は2系(3系は TypeScript 5.9 以上が前提)

- 新しいユースケース → Application.Tests に業務フローとして追加
- 新しいリポジトリ/カラム → Infrastructure.Tests にラウンドトリップを追加
- スキーマ変更 → `SchemaResetTests` に「旧テーブルの破棄・作り直し+冪等性(2回実行)+
  新スキーマの既存データ保持」のテストを追加(レガシー移行は行わない方針。ci-and-env 参照)
- 一時DBは `Path.GetTempPath()` に GUID 名で作り Dispose で削除。テスト間で共有しない

## 既知の落とし穴(このセッションで実際に踏んだもの)

- **アーキテクチャテストの空振り**: 「依存しないこと」の検証は検出器が壊れていても通る。
  ポジティブコントロール(リポジトリがDapperに依存して**いる**ことの検出)を必ず維持する。
  NetArchTest は入れ子型(record Row)も対象に含むため、`HaveNameEndingWith("Repository")` 等で絞る
- **Playwright strict mode**: 金額は明細行と合計行、統計タイルとテーブルに重複して表示される。
  `.first()` か行スコープで特定する。バッジは `.locator('.badge', { hasText: ... })`。
  説明文にも同じ語が出る(「失効」等)のでテキスト一致は要注意
- **null キーの Dictionary**: `GroupBy(x => x.NullableKey).ToDictionary(...)` は null キーで落ちる。
  null になりうるキーは Where で分離してから集計する(ProfitAnalysisService は案件系区分に
  絞ってから `ProjectId!.Value` で GroupBy している。タプルキーなら null を含んでも安全)
- WebApplicationFactory を使うため `Program.cs` 末尾の `public partial class Program {}` を消さない
- **Playwright の連続フォーム送信レース**: 送信ボタンを連打する前に完了を待つこと。
  ローカルの速いマシンでは通っても遅い CI で落ちる。実績計上のように送信後に入力欄が
  クリアされる画面なら `await expect(欄).toHaveValue('')` で完了を待ってから次へ進む
  (`reuseExistingServer` で残ったサーバを掴むと結果がぶれるので、E2E前に 5173/5100 を掃除する)
- **予算明細の自動保存の重なり**: 予算編集画面はセルを離れるたびに PUT /lines を送る。サーバは予算を
  読み込み→明細を足し→保存するため、PUT が重なると後の保存が先の保存を上書きする(同時更新の制御がない)。
  E2E で複数セルを続けて入力するときは `page.waitForResponse(PUT /lines)` で1セルずつ保存を待つ

E2E(API E2E・ブラウザE2E)のテストケースを追加・変更したら、解説 `docs/design/e2e-test-guide.html` も更新する。

## プロパティベーステスト(CsCheck)

例示テストに加えて、ドメインとユースケースの**不変条件**は CsCheck で任意の入力に対して検証する
(C# ネイティブ・縮小付き。Domain.Tests / Application.Tests が参照)。`[Fact]` の中で
`gen.Sample(...)` / `gen.SampleAsync(...)` を呼ぶだけでよい。

| 置き場所 | クラス | 検証する性質 |
|---|---|---|
| tests/unit/backend/Domain/PropertyBased/ValueObjectPropertyTests.cs | `値オブジェクトの性質` | Money の加減算の法則、FiscalHalf の文字列往復と全順序、費目コードの正規化、月 1..6 |
| 〃 DepartmentBudgetPropertyTests.cs | `課予算の性質` | **モデルベース**: 任意の編集操作列を集約と単純モデルに適用し成否・状態が一致、拒否された操作は無副作用、不変条件(区分合計=明細合計・月次合計・混在禁止)。承認後の編集拒否、改定の引き継ぎ、Restore 往復 |
| 〃 ActualEntryPropertyTests.cs | `実績計上の性質` | 計上可否の規則、明細名・備考の正規化、Restore 往復 |
| 〃 AnalysisServicePropertyTests.cs | `分析サービスの性質` | 差異=実績−予算、区分合計=明細合計、予定外判定、入力順非依存、比較の反対称性、「案件別損益−期間費用=全体損益」、「部合計=課別内訳の合計」 |
| tests/integration/backend/Application/PropertyBased/UseCasePropertyTests.cs | `ユースケースの性質` | SQLite 往復後も応答どおり読み戻せる、改定を繰り返しても承認版は常に1件、実績合計の反映、部合計は承認済み課のみ |

- 生成器は `PropertyBased/DomainGenerators.cs`(`ドメイン生成器`)。案件・費目・明細名は**小さなプール**から選び、
  上書き・衝突・混在が頻繁に起きるようにする。金額は1円単位の整数(decimal の和を厳密比較するため)
- 失敗時は `Set seed: "..."` が出る。`CsCheck_Seed=<seed> dotnet test --filter ...` で再現。
  反復回数は `CsCheck_Iter=2000` 等で一時的に増やせる(既定 100。Application は DB を作るため `iter: 25`)
- 反例表示のため、Dictionary を持つ操作 record は `ToString` を上書きする(既定では中身が出ない)
- **落とし穴**: SQLite 往復した decimal は値が同じでも scale が変わる(`100` → `100.0`)。文字列比較時は正規化する
- PBT で `BudgetLine.SetMonthly` が検証前に月別金額を書き換えていた不具合を検出・修正済み
  (回帰テスト `不正な月次金額での上書きが拒否されても既存明細の金額と月次は変わらない`)

## Gherkin シナリオ(Reqnroll / playwright-bdd)

業務フローは日本語 Gherkin で書く。3か所にある:

| 置き場所 | ライブラリ | 状態の共有 | xUnit/spec との関係 |
|---|---|---|---|
| `tests/integration/backend/Application/Features/` | Reqnroll.xUnit | `業務コンテキスト`(`Support/BusinessContext.cs`) | 既存 xUnit と併存(CI・ミューテーションにそのまま乗る) |
| `tests/e2e/api/Features/` | Reqnroll.xUnit | `APIコンテキスト`(`Support/ApiContext.cs`) | 既存 xUnit と併存 |
| `tests/e2e/browser/features/` | playwright-bdd | フィクスチャ `業務データ`(`support/fixtures.ts`) | **旧 journey.spec.ts は置き換え済み**(ブラウザは重いので二重に持たない) |

Reqnroll は `Reqnroll.xUnit`(SpecFlow の後継。xUnit 2.9 で動く)。業務ルールの組み合わせは Application 層、
HTTP の入口〜出口(ステータス・JSON・認証)は API E2E、画面の操作と表示はブラウザ E2E に書き分ける。

- **`ルール:`(Rule)でシナリオをまとめる**。ルールの中に `背景:` も置ける(ルール内のシナリオだけに効く)

- **キーワード**: 各 .feature 先頭に `# language: ja`(`reqnroll.json` でも ja 指定)。
  機能 / 背景 / シナリオ / シナリオアウトライン / 例 / 前提 / もし / ならば / かつ
- **状態**: `Support/BusinessContext.cs` の `業務コンテキスト` をコンストラクタ注入で共有。
  シナリオ終了時に Dispose され一時DBが消える。部・課・案件はフィーチャー上の**コード(=名称)**で書き、ここで ID に解決
- **ステップ**: `Steps/`(組織と案件 / 予算 / 実績と分析)。Cucumber Expression で
  `{string}`(ダブルクォート)・`{int}`・`{long}` を使う。金額は `{long}`。**`(` `)` `/` はステップ文に書かない**
  (Cucumber Expression の任意・択一記法と解釈される)
- 前提でも操作でも使うステップは `[StepDefinition]`、エラーを期待する操作は「…しようとする」で
  `ctx.試行(...)` を通し(DomainException/NotFoundException を捕捉)、`ならば "…" を含むエラーになる` で検証
- 表(DataTable)の列名は日本語。区分は 売上高/加工費/外注費/期間費用 で書き `業務コンテキスト.区分()` で内部名へ。
  空セルは null(費目一括・案件なし)
- **新しいステップを足す前に既存ステップを探して再利用する**(言い回しの揺れでステップを増やさない)
- 生成されるコードビハインド `*.feature.cs` はビルド時に作られるので gitignore 済み
- 未定義ステップがあるとそのシナリオは Skipped/Inconclusive になる。全件 Passed で Skipped 0 を確認する
- **Reqnroll はキーワード種別でステップを照合する**: `[Given]` だけのステップを `もし`/`かつ`(When 扱い)で
  書くと未定義になる。前提でも操作でも使うものは `[StepDefinition]` にする。`前提 … かつ …` の `かつ` は Given 扱い
- 表の列を任意にしたいステップは `row.TryGetValue("列名", out var v)` で読む(例: 実績の `備考`、課別内訳の列)

### ブラウザ E2E(playwright-bdd)

- `tests/e2e/browser` で `npm test` = `bddgen && playwright test`。`bddgen` が `.features-gen/`(gitignore・ESLint/Prettier 除外)に
  テストを生成する。CI も `npm test`。**`npx playwright test` だけだと古い生成物で走る**
- ステップ定義も `npm run lint`(ESLint)・`npm run typecheck`(tsc)・`npm run format:check`(Prettier)で検査する(CI の browser-e2e ジョブ)
- **前提は API、もし/ならば は画面**: 部・課・案件・承認済み予算・実績は `業務データ` が `request` フィクスチャで
  API から作る(Vite の /api プロキシ経由)。画面で登録すること自体を確かめるのは「計画策定」の最初のシナリオだけ
- 部・課の名前と費目コードは `業務データ.実名()` / `費目コード()` で**シナリオ固有の接尾辞**を付ける(DB を全シナリオで
  共有し、並列実行するため)。案件は課の中で一意なのでそのまま。シナリオは互いに独立で serial 指定はしない
- API で作る予算・実績の半期は `今期()`(画面の `currentFiscalHalf` と同じ規則)。月次入力欄は位置で指定する
- playwright-bdd はキーワード種別を区別しない。同じ処理を「…する」「…している」の両方で書きたいときは
  ハンドラを変数にして2回登録する。共有ハンドラの引数型は `画面`(support/fixtures.ts)

## 実行方法

```bash
cd backend && dotnet test                                  # バックエンド全部(slnx が tests/ のテストプロジェクトを含む)
cd tests/unit/frontend && npm test                         # フロントエンド単独(初回は npm ci。frontend 側は不要)
cd tests/e2e/browser && npm test                           # ブラウザE2E(初回は frontend と tests/e2e/browser で npm ci)
# このリモート環境では: CI= PW_CHROMIUM=/opt/pw-browsers/chromium npm test
```

カバレッジ目標: 全体85%以上を維持(現状 89.9%)。CI の実行サマリで確認できる。

## ミューテーションテスト(Stryker.NET。Issue #6)

行カバレッジは「実行したか」しか見ないため、テストが**仕様を実際に検証しているか**は
Stryker.NET のミューテーションテストで補完する。ツールはローカルツールとして
`tests/.config/dotnet-tools.json` に固定(`dotnet-stryker` 5.0.0)。

- 対象は **Domain・Application 層のみ**(業務ロジックの中核。Infrastructure は SQL 文字列中心で
  ミューテーションの価値が低く遅い、WebApi は薄いホスティングのため除外)
- 設定は各テストプロジェクト直下の `stryker-config.json`
  (`tests/unit/backend/Domain` / `tests/integration/backend/Application`)。
  `project` で対象 src を明示(テストプロジェクトが複数 src を参照する場合の曖昧さ回避に必須)。
  レポータは html/json/markdown/cleartext。閾値 high:80 low:60 **break:0(スコアで CI を落とさない)**
- 実行(各テストプロジェクトのディレクトリで):

  ```bash
  cd tests && dotnet tool restore
  (cd unit/backend/Domain             && dotnet tool run dotnet-stryker)
  (cd integration/backend/Application && dotnet tool run dotnet-stryker)
  ```

- 出力は `StrykerOutput/<日時>/reports/`(gitignore 済み)。CI は `mutation` ジョブで
  markdown を実行サマリへ、html を `mutation-report` アーティファクトへ出す(**非ブロッキング**)
- 現状スコアの目安: Domain ≈ 54%、Application ≈ 54%。実行時間は各 1.5〜2 分程度
