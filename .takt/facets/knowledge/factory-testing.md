# テストの規約の知識

総合原価管理システム(予実管理)のテストを書く・直す・動かすときの規約。[B] 要件(シナリオ)・[C] 仕様(テストの観点)・[D] 実装・[E] 検証で使う。
この知識が正本(スキル `testing` はここを読む案内)。工場の外のふだんの開発でも、この規約に従う。CI の構成は知識「CI・開発環境・プッシュの運用」にある。

## テストの出どころ

テストは要件・仕様から導く。コードを読んでテストを作らない。

| テスト | 出どころ |
|---|---|
| Gherkin のシナリオ | 要件(`requirements/`)。`.feature` の `ルール:` に要件文を書き、`@FR-x.y` タグを付ける |
| 例示テスト | 処理ルール(`specs/rules/SPEC-xxx.md`)の「具体例」の各行 |
| プロパティベーステスト | 業務ルール(`knowledge/business-rules/`)の不変条件 |

- シナリオを書くのは [B] 要件、ステップ定義と例示テストを書くのは [D] 実装。レベルごとの母集団と完了基準は `specs/test-plan.md`
- まだ実装のないシナリオには `@未実装` を付ける([B] が付け、[D] が外す)。Reqnroll は `Support/未実装のシナリオ.cs` のフックで Skipped にし、playwright-bdd は `tags: 'not @未実装'` で生成しない。`@未実装` を外すのは、シナリオの文を変えることではない
- 実装の定数をそのままテストの期待値にしない
- テスト名は日本語の文にする(例: `改定版を承認すると旧バージョンは自動的に失効する()`)。クラス名も業務の概念(`原価予算の策定と改定` など)。ヘルパの名前も日本語でよい

## テストの置き場所

テストはすべてリポジトリ直下の `tests/` に置き、テストのスコープで区切る(`backend/` と `frontend/` には本体のコードだけを置く)。
迷ったら下のスコープ(速いテスト)から検討する。速いテストで確かめられることを遅いテストに書かない。

| スコープ | 何を通すか | 速さ |
|---|---|---|
| `tests/unit/` | 1 つの部品だけ。DB・HTTP・ブラウザを使わない(フロントは jsdom + MSW) | 速い |
| `tests/integration/` | 本物の SQLite を通す(ユースケース・リポジトリ) | 中 |
| `tests/e2e/` | システムを外側から操作する(API は HTTP、ブラウザは画面→API→DB) | 遅い |
| `tests/architecture/` | コードの構造(層の依存ルール) | 速い |

| 置き場所 | 対象 | 方式 |
|---|---|---|
| `tests/unit/backend/Domain` | 不変条件・計算規則 | 純粋単体。`PropertyBased/` に CsCheck |
| `tests/unit/frontend` | 画面のロジックとコンポーネント | Vitest + Testing Library + MSW |
| `tests/integration/backend/Application` | ユースケース。業務ルールの Gherkin(Reqnroll) | `UseCaseFixture`(一時 SQLite + 本物のリポジトリ + `FixedClock`。日本語のヘルパ `課を作成`・`案件を作成`・`承認済み予算を作成`) |
| `tests/integration/backend/Infrastructure` | 永続化の往復・スキーマの作り直し | `RepositoryFixture`(一時 SQLite)+ `SchemaResetTests` |
| `tests/e2e/api` | HTTP 経由の業務フロー(Gherkin) | `WebApplicationFactory<Program>`(`ApiFixture`)+ Reqnroll |
| `tests/e2e/browser` | 画面→API→DB のつながり | playwright-bdd + @playwright/test(webServer で両サーバを自動で起動) |
| `tests/architecture/backend` | 層の依存ルール | NetArchTest |

- 書き分け: 画面だけの振る舞い(折りたたみ・入力チェック・送るリクエストの中身・トースト)は `unit/frontend`、業務ルールは `integration/backend/Application`(Gherkin)、HTTP の入口〜出口(ステータス・JSON・認証)は `e2e/api`、画面→API→DB のつながりだけを `e2e/browser` に書く
- 新しいユースケース → Application のテストに業務フローとして足す。新しいリポジトリ・カラム → Infrastructure のテストに往復を足す
- C# のテストプロジェクトの中は同じ型で並べる: 直下に xUnit の例示テスト(`*Tests.cs`)、`PropertyBased/`(CsCheck)、`Features/`(`*.feature`)、`Steps/`(ステップ定義)、`Support/`(フィクスチャ・シナリオの状態。名前空間 `….Support` は csproj のグローバル using で全テストから見える)
- ブラウザ(`tests/e2e/browser`)は小文字で `features/`・`steps/`・`support/`。フロント単独(`tests/unit/frontend`)は `logic/`(純粋関数)・`components/`・`pages/<画面名>/<観点>.test.tsx`・`support/`
- 一時 DB は `Path.GetTempPath()` に GUID 名で作り、Dispose で消す。テスト間で共有しない
- `tests/Directory.Build.props` が `backend/Directory.Build.props`(アナライザ・警告のエラー扱い)を取り込む。`backend/CostManagement.slnx` が `../tests/...` を参照するので、`cd backend && dotnet test` で全テストが走る
- `tests/e2e/browser` と `tests/unit/frontend` は独立した npm パッケージ。ブラウザ E2E は `frontend` の Vite 開発サーバで画面を起動するので、`frontend` 側の `npm ci` も要る

## Gherkin(Reqnroll / playwright-bdd)

| 置き場所 | ライブラリ | 状態の共有 |
|---|---|---|
| `tests/integration/backend/Application/Features/` | Reqnroll.xUnit | `業務コンテキスト`(`Support/BusinessContext.cs`) |
| `tests/e2e/api/Features/` | Reqnroll.xUnit | `APIコンテキスト`(`Support/ApiContext.cs`) |
| `tests/e2e/browser/features/` | playwright-bdd | フィクスチャ `業務データ`(`support/fixtures.ts`) |

### 共通

- 各 `.feature` の先頭に `# language: ja`。キーワードは 機能 / ルール / 背景 / シナリオ / シナリオアウトライン / 例 / 前提 / もし / ならば / かつ
- `ルール:` でシナリオをまとめる。ルールの中に `背景:` も置ける(そのルールのシナリオだけに効く)
- 新しいステップを足す前に、既存のステップを探して再利用する(言い回しの揺れでステップを増やさない)
- ステップ文に `(` `)` `/` を書かない(Cucumber Expression の任意・択一の記法と解釈される)
- 表(DataTable)の列名は日本語。区分は 売上高 / 加工費 / 外注費 / 期間費用 で書く。空のセルは null(費目一括・案件なし)
- 未定義のステップがあるシナリオは Skipped(Inconclusive)になる。全件 Passed で Skipped 0 を確かめる

### Reqnroll

- 部・課・案件はフィーチャー上のコード(= 名称)で書き、`業務コンテキスト` で ID に解決する。区分は `業務コンテキスト.区分()` で内部名にする
- 状態は `業務コンテキスト` をコンストラクタ注入で共有する。シナリオの終わりに Dispose され、一時 DB が消える
- ステップは `Steps/`(組織と案件 / 予算 / 実績と分析)。Cucumber Expression で `{string}`(ダブルクォート)・`{int}`・`{long}` を使う。金額は `{long}`
- Reqnroll はキーワードの種類でステップを照合する。`[Given]` だけのステップを `もし`・`かつ`(When 扱い)で書くと未定義になる。前提でも操作でも使うステップは `[StepDefinition]` にする。`前提 … かつ …` の `かつ` は Given 扱い
- エラーを期待する操作は「…しようとする」と書き、`ctx.試行(...)` を通す(`DomainException`・`NotFoundException` を捕まえる)。確かめるのは `ならば "…" を含むエラーになる`
- 表の列を任意にしたいステップは `row.TryGetValue("列名", out var v)` で読む
- 生成されるコードビハインド `*.feature.cs` はビルドのときに作られる(gitignore 済み)

### playwright-bdd(ブラウザ E2E)

- `tests/e2e/browser` で `npm test`(= `bddgen && playwright test`)。`bddgen` が `.features-gen/` にテストを生成する。`npx playwright test` だけだと古い生成物で走る
- ステップ定義も `npm run lint`・`npm run typecheck`・`npm run format:check` で検査される(CI の browser-e2e ジョブ)
- 前提は API、もし・ならば は画面。部・課・案件・承認済み予算・実績は `業務データ` が `request` フィクスチャで API から作る。画面で登録すること自体を確かめるのは「計画策定」の最初のシナリオだけ
- DB は全シナリオで共有し、並列で動く。部・課の名前と費目コードは `業務データ.実名()`・`費目コード()` でシナリオ固有の接尾辞を付ける。案件は課の中で一意なのでそのまま。シナリオは互いに独立させ、serial を指定しない。新しい検証は既存のシナリオに継ぎ足さず、独立したシナリオとして足す
- API で作る予算・実績の半期は `今期()`(画面の `currentFiscalHalf` と同じ規則)。月次の入力欄は位置で指定する
- playwright-bdd はキーワードの種類を区別しない。同じ処理を「…する」「…している」の両方で書きたいときは、ハンドラを変数にして 2 回登録する。共有ハンドラの引数の型は `画面`(`support/fixtures.ts`)

## フロントエンド単独テスト(tests/unit/frontend)

- テストからは `@app/...` で `frontend/src` を参照する(vitest.config.ts の alias と tsconfig の paths)
- React の二重読み込みに注意する。vitest.config.ts の `resolve.dedupe`(react / react-dom / react-router-dom)と tsconfig の `paths` で 1 つの React にそろえている。react などの版は `frontend` と同じに保つ
- API は MSW でモックする。決めていない API が呼ばれるとテストが失敗する(`onUnhandledRequest: 'error'`)
- 予算編集画面は `support/fakeBudgetApi.ts` の `偽の予算API`(メモリ上の偽バックエンド)を使い、保存後の再描画と、送ったリクエストの中身・順序(`fake.writes`)を確かめる
- 画面のコードは相対 URL(`fetch('/api/...')`)で呼ぶため、`support/setup.ts` が jsdom のページ URL を基準に絶対 URL へ直す
- テストする計算は画面から純粋関数に切り出す(例: `frontend/src/budgetLines.ts` の `cellAction`・`monthlyTotal`)。境界値は `test.each` の表で並べる
- 実行日で変わる処理は `vi.setSystemTime` で日付を固定する(TZ は Asia/Tokyo)
- 純粋関数のテストは先頭に `// @vitest-environment node`。入力操作は `userEvent.setup({ delay: null })`
- Vitest は 3 系、MSW は 2 系(版の制約。上げない)

## プロパティベーステスト(CsCheck)

- ドメインとユースケースの不変条件は、例示テストに加えて CsCheck で任意の入力に対して確かめる。`[Fact]` の中で `gen.Sample(...)` / `gen.SampleAsync(...)` を呼ぶ
- 既存の性質テスト:

| 置き場所 | クラス | 確かめる性質 |
|---|---|---|
| `tests/unit/backend/Domain/PropertyBased/ValueObjectPropertyTests.cs` | `値オブジェクトの性質` | Money の加減算の法則、FiscalHalf の文字列往復と全順序、費目コードの正規化、月 1..6 |
| 〃 `DepartmentBudgetPropertyTests.cs` | `課予算の性質` | モデルベース: 任意の編集操作の列を集約と単純なモデルに適用し、成否・状態が一致する。拒否された操作は副作用がない。不変条件(区分合計 = 明細合計・月次合計・混在の禁止)。承認後の編集の拒否、改定の引き継ぎ、Restore の往復 |
| 〃 `ActualEntryPropertyTests.cs` | `実績計上の性質` | 計上できるかの規則、明細名・備考の正規化、Restore の往復 |
| 〃 `AnalysisServicePropertyTests.cs` | `分析サービスの性質` | 差異 = 実績 − 予算、区分合計 = 明細合計、予定外の判定、入力の順に依らない、比較の反対称性、「案件別損益 − 期間費用 = 全体の損益」、「部合計 = 課別内訳の合計」 |
| `tests/integration/backend/Application/PropertyBased/UseCasePropertyTests.cs` | `ユースケースの性質` | SQLite を往復しても応答どおり読み戻せる、改定を繰り返しても承認版は常に 1 件、実績合計の反映、部合計は承認済みの課だけ |
- 生成器は `PropertyBased/DomainGenerators.cs`(`ドメイン生成器`)。案件・費目・明細名は小さなプールから選び、上書き・衝突・混在が起きやすくする。金額は 1 円単位の整数(decimal の和を厳密に比べるため)
- 失敗すると `Set seed: "..."` が出る。`CsCheck_Seed=<seed> dotnet test --filter ...` で再現する。反復回数は `CsCheck_Iter=2000` などで一時的に増やせる(既定 100。Application は DB を作るため `iter: 25`)
- 反例を読めるように、Dictionary を持つ操作の record は `ToString` を上書きする
- SQLite を往復した decimal は、値が同じでも scale が変わる(`100` → `100.0`)。文字列で比べるときは正規化する
- PBT で `BudgetLine.SetMonthly` が検証の前に月別金額を書き換えていた不具合を見つけて直した(回帰テスト `不正な月次金額での上書きが拒否されても既存明細の金額と月次は変わらない`)

## 既知の落とし穴(実際に踏んだもの)

- アーキテクチャテストの空振り: 「依存しないこと」の検証は、検出器が壊れていても通る。ポジティブコントロール(リポジトリが Dapper に依存して「いる」ことの検出)を必ず残す。NetArchTest は入れ子の型(record `Row`)も対象に含むので、`HaveNameEndingWith("Repository")` などで絞る
- Playwright の strict mode: 金額は明細行と合計行、統計のタイルと表に重ねて出る。`.first()` か行のスコープで特定する。バッジは `.locator('.badge', { hasText: ... })`。説明文にも同じ語(「失効」など)が出るので、テキストの一致に注意する
- null キーの Dictionary: `GroupBy(x => x.NullableKey).ToDictionary(...)` は null キーで落ちる。null になりうるキーは Where で分けてから集計する(タプルのキーなら null を含んでも安全)
- Playwright の連続したフォーム送信のレース: 次の送信の前に完了を待つ。送信後に入力欄がクリアされる画面なら `await expect(欄).toHaveValue('')` で待つ。`reuseExistingServer` で残ったサーバを掴むと結果がぶれるので、E2E の前に 5173・5100 を空ける
- 予算明細の自動保存の重なり: 予算編集画面はセルを離れるたびに PUT /lines を送り、後の保存が先の保存を上書きすることがある。E2E で複数のセルを続けて入力するときは、`page.waitForResponse(PUT /lines)` で 1 セルずつ保存を待つ

## 実行方法

```bash
cd backend && dotnet test                     # バックエンド全部(slnx が tests/ のテストプロジェクトを含む)
cd tests/unit/frontend && npm test            # フロントエンド単独(初回は npm ci。frontend 側は不要)
cd tests/e2e/browser && npm test              # ブラウザ E2E(初回は frontend と tests/e2e/browser で npm ci)
bash tools/factory/screens-docker.sh          # 画面の差分の検査(CI と同じコンテナ。手元の Chromium では描画が違うので比べない)
```

- 環境によっては Chromium の場所を指定する: `CI= PW_CHROMIUM=/opt/pw-browsers/chromium npm test`
- localhost への curl は `--noproxy localhost` を付ける
- devcontainer は起動時に `docker/init-firewall.sh` が外への通信を許可リストに絞る。許可リストにない宛先は `Connection refused` に似た失敗になるので、通信の失敗はまずこれを疑う。宛先を足すのは工場の変更で、この workflow ではしない
- ブラウザの要らない場面では `PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD=1` を保つ
- カバレッジの目標は全体 85% 以上
- E2E(API・ブラウザ)のテストケースを足す・変えたら、`docs/design/e2e-test-guide.html` も直す

## 画面の差分の検査(tests/e2e/screens)

- 画面要件(`requirements/screens/screens.yaml`)の状態ごとに、fixtures で API を返して画面を開き、承認した画面(`requirements/screens/approved/`)と比べる。バックエンドは起動しない
- CI と同じコンテナで `bash tools/factory/screens-docker.sh` で動かす(手元の Chromium では描画が違うので比べない)
- 承認した画面の撮り直しは人の仕事(ガードが止める)

## ミューテーションテスト(Stryker.NET。Issue #6)

行カバレッジは「実行したか」しか見ないので、テストが仕様を実際に確かめているかを Stryker.NET のミューテーションテストで補う。ツールはローカルツールとして `tests/.config/dotnet-tools.json` に固定している(`dotnet-stryker` 5.0.0)。

- 対象は Domain・Application 層だけ(業務ロジックの中核。Infrastructure は SQL の文字列が中心で価値が低く遅い、WebApi は薄いホスティングなので除く)
- 設定は各テストプロジェクト直下の `stryker-config.json`(`tests/unit/backend/Domain` / `tests/integration/backend/Application`)。`project` で対象の src を明示する(テストプロジェクトが複数の src を参照するときの曖昧さを避けるのに要る)。レポータは html・json・markdown・cleartext。閾値は high:80 low:60 break:0(スコアで CI を落とさない)
- 実行(各テストプロジェクトのディレクトリで):

```bash
cd tests && dotnet tool restore
(cd unit/backend/Domain             && dotnet tool run dotnet-stryker)
(cd integration/backend/Application && dotnet tool run dotnet-stryker)
```

- 出力は `StrykerOutput/<日時>/reports/`(gitignore 済み)。CI は `mutation` ジョブで markdown を実行サマリへ、html を `mutation-report` アーティファクトへ出す(非ブロッキング)
- スコアの目安: Domain ≈ 54%、Application ≈ 54%。実行時間は各 1.5〜2 分ほど
