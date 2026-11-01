# 実装・テストの規約の知識

出どころはリポジトリ(main)の `feature-dev`・`testing`・`domain-design`・`ci-and-env`・`docs-update` スキル。[B] 画面・[C] 仕様・[D] 実装・[E] 検証で使う。

## 実装の順番(この順で層を通す)

1. Domain(`backend/src/CostManagement.Domain`): 集約・値オブジェクト・ドメインサービス + ドメインテスト
2. Application(`.../CostManagement.Application`): ユースケースサービス + DTO(`Dtos.cs` に集約)
3. Infrastructure(`.../CostManagement.Infrastructure`): Dapper リポジトリ、スキーマ(`DatabaseInitializer`)、DI 登録(`DependencyInjection.cs`)
4. WebApi(`.../CostManagement.WebApi/Program.cs`): Minimal API のエンドポイント
5. Frontend(`frontend/src`): `api.ts` に型とクライアント → ページ・コンポーネント → `App.tsx` のルート
6. テスト → ドキュメント(README・`docs/MANUAL.md`・`docs/DESIGN.md`)

## 各層の規約

- API の型は OpenAPI から生成する。DTO・エンドポイントを変えたら `cd backend && UPDATE_OPENAPI=1 dotnet test --filter OpenApi`(`docs/api/openapi.json` を更新)→ `cd frontend && npm run gen:api`(`src/api-schema.d.ts` を再生成)。手書きの interface を増やさない。201・204 のエンドポイントは `.Produces<T>()` で応答型を明示する
- [C] の仕様で手で書いた `docs/api/openapi.json` の差分は、[D] で実装から再生成したものと一致させる
- DTO は camelCase JSON。数値は JSON の数値のみ受け付ける(`NumberHandling.Strict`)
- Dapper: decimal・Guid・DateTime は TEXT 往復。金額カラムは TEXT。明細の `project_id` / `element_code` は区分により排他で、未使用側は DB では `''`、読み出しで null に読み替える
- 半期は DB では `fiscal_half TEXT`("2026-H1" 形式)
- 集約の更新は「ヘッダ UPDATE + 明細 DELETE→INSERT」をトランザクションで行う
- エラー: 不変条件違反は `DomainException`(→400)、未検出は `NotFoundException`(→404)。メッセージは日本語でユーザーに見せられる文にする
- フロント: 依存は React と Router のみ(UI・チャートのライブラリは使わない)。金額表示は `formatYen`/`formatSignedYen`、区分名は `categoryLabel`、半期は `halfLabel`。差異の色はコスト=正が赤、売上高・損益=正が緑。ダークモード対応(CSS 変数)。エラーは `<Toast message={error} onClose={() => setError(null)} />` で出す
- バックエンドは警告をエラー扱い(`AnalysisLevel=latest-recommended`)。ルールを外すときはリポジトリ直下の `.editorconfig` に理由のコメントつきで書く
- コメント: C# は XML コメントの `<summary>`(ドメイン層は日本語、他層は英語)。フロントは JSDoc
- DB スキーマを変えるとき(ci-and-env): 旧世代のスキーマからのデータ移行はしない。`DatabaseInitializer` の CREATE TABLE を直し、構造が変わるなら `DropLegacyTables` に冪等な判定を足し、`SchemaResetTests` にテスト(旧テーブルの破棄・作り直し、2回実行しても壊れない、新スキーマのデータを保つ)を足し、README の「スキーマの作り直し」節を直す。単純なカラム追加は `ALTER TABLE … ADD COLUMN`

## ドメインを変えるとき(domain-design)

- このシステムのドメインの決定事項は再検討しない。要点は知識「工場のライン」の「プロダクトの絶対のルール」と `knowledge/` の業務ルール。差異の符号は文書・画面・テストで「利益が増える/減る」と書き、「有利/不利」という語を使わない
- 戦術パターン(アーキテクチャテストで強制される): 集約ルート・エンティティ・ドメインサービスは `sealed`、値オブジェクトは `readonly record struct`、集約に公開セッターを置かない、リポジトリのポートはドメイン層・実装はインフラ層、永続化からの復元は `Restore(...)`
- 変えるときの手順: ドメインのテスト(日本語のテスト名)を先に直す → `Restore`・リポジトリ・DTO・分析サービスへの波及を追う → スキーマが変わるなら下の「スキーマ」→ `docs/DESIGN.md` のクラス図・不変条件表を同期 → `ArchitectureTests.cs` の固定値(ポート数・値オブジェクトの列挙・集約の列挙)を確かめる

## ドキュメントの同期(docs-update)

| 文書 | 直すとき |
|---|---|
| `README.md` | API の追加、費目のシードの変更、テストの種類の追加、実行手順の変更 |
| `docs/MANUAL.md` | 画面・操作の流れの変更 |
| `docs/design/e2e-test-guide.html` | E2E のテストケースの追加・変更・数値の変更 |
| `docs/DESIGN.md` | 集約・ドメインサービス・層の構成・不変条件の変更(図は Mermaid) |

- 費目マスタの一覧は `DatabaseInitializer` のシードと突き合わせて書く(推測で書かない)

## テストの出どころ

テストは要件・仕様から導く。コードを読んでテストを作らない。

| テスト | 出どころ |
|---|---|
| Gherkin のシナリオ | 要件(`requirements/`)。`.feature` の `ルール:` に要件文、`@FR-x.y` タグ |
| 例示テスト | 処理ルール(`specs/rules/SPEC-xxx.md`)の「具体例」の各行 |
| プロパティベーステスト | 業務ルール(`knowledge/business-rules/`)の不変条件 |

- まだ実装のないシナリオには `@未実装` を付ける([B] が付け、[D] が外す)。`@未実装` を外すのはシナリオの文を変えることではない
- 実装の定数をそのままテストの期待値にしない
- テスト名は日本語の文にする(例: `改定版を承認すると旧バージョンは自動的に失効する()`)

## テストの置き場所

| 置き場所 | 対象 |
|---|---|
| `tests/unit/backend/Domain` | 不変条件・計算規則(純粋単体)。`PropertyBased/` に CsCheck |
| `tests/unit/frontend` | 画面のロジックとコンポーネント(Vitest + Testing Library + MSW) |
| `tests/integration/backend/Application` | ユースケース(一時 SQLite)。業務ルールの Gherkin(Reqnroll) |
| `tests/integration/backend/Infrastructure` | 永続化の往復・スキーマの作り直し |
| `tests/e2e/api` | HTTP 経由の業務フロー(Gherkin) |
| `tests/e2e/browser` | 画面→API→DB のつながり(playwright-bdd) |
| `tests/architecture/backend` | 層の依存ルール |

- 迷ったら下のスコープ(速いテスト)から検討する
- Gherkin は各 `.feature` 先頭に `# language: ja`。新しいステップを足す前に既存のステップを探して再利用する。ステップ文に `(` `)` `/` を書かない
- 未定義ステップのあるシナリオは Skipped になる。全件 Passed で Skipped 0 を確かめる
- E2E のテストケースを足したら `docs/design/e2e-test-guide.html` も直す

## コミット前のチェックリスト([D])

```bash
cd backend && dotnet test
cd frontend && npm run build
cd frontend && npm run lint && npm run format:check
cd tests/unit/frontend && npm test      # 画面を触った場合
cd tests/e2e/browser && npm test        # 画面を触った場合
```

## 画面要件([B] の画面)

- 画面要件は `requirements/screens/screens.yaml`(画面 ID・ルート・項目・表示の条件・操作・`requirements: [FR-…]`)と `requirements/screens/fixtures/<SCR-xxx>/<状態>.json`(通常・空・エラー・境界値。API の応答の形のまま)
- `requirements/screens/approved/` には書かない。承認した画面の画像を撮るのは人
- 画面の差分の検査は `bash tools/factory/screens-docker.sh`(CI と同じコンテナ)。新しい状態の画像がないと検査は落ちる。それは人が撮る
