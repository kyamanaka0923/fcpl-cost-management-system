# 実装の規約の知識

総合原価管理システム(予実管理)のコードを書くときの規約。[B] 画面・[C] 仕様・[D] 実装・[E] 検証で使う。
出どころはリポジトリ(main)の `feature-dev`・`docs-update` スキル。正本はそれらのファイルで、この知識はその写し。正本と食い違いを見つけたら、食い違いをレポートに書く(正本は書き換えない)。
テストの書き方は知識「テストの規約」、ドメインの決定事項は知識「ドメインの規約」、DB スキーマの変更は知識「DB スキーマ変更の手順」にある。

## 実装の順番(この順で層を通す)

1. Domain(`backend/src/CostManagement.Domain`): 集約・値オブジェクト・ドメインサービス + ドメインテスト
2. Application(`.../CostManagement.Application`): ユースケースサービス + DTO(`Dtos.cs` に集約)
3. Infrastructure(`.../CostManagement.Infrastructure`): Dapper リポジトリ、スキーマ(`DatabaseInitializer`)、DI 登録(`DependencyInjection.cs`)
4. WebApi(`.../CostManagement.WebApi/Program.cs`): Minimal API のエンドポイント
5. Frontend(`frontend/src`): `api.ts` に型とクライアント → ページ・コンポーネント → `App.tsx` のルート
6. テスト → ドキュメント(下の「ドキュメントの同期」)

## 各層の規約

### バックエンド

- API の型は OpenAPI から生成する。DTO・エンドポイントを変えたら `cd backend && UPDATE_OPENAPI=1 dotnet test --filter OpenApi`(`docs/api/openapi.json` を更新)→ `cd frontend && npm run gen:api`(`src/api-schema.d.ts` を再生成)
- 作成系(201)・204 のエンドポイントは `.Produces<T>()` で応答型を明示する
- [C] の仕様で手で書いた `docs/api/openapi.json` の差分は、[D] で実装から再生成したものと一致させる
- DTO は camelCase JSON(ASP.NET Core の既定)。数値は JSON の数値のみ受け付ける(`NumberHandling.Strict`)
- Dapper: decimal・Guid・DateTime は `SqliteConnectionFactory` の型ハンドラで TEXT 往復。金額カラムは TEXT
- 明細の `project_id` / `element_code` は区分により排他。未使用側は DB では `''` として保存し、読み出しで null に読み替える(SQLite の UNIQUE 制約で NULL が重複できてしまう問題の回避を兼ねる)
- 半期は DB では `fiscal_half TEXT`("2026-H1" 形式 = `FiscalHalf.ToString()`)
- リポジトリの検索は、列エイリアス付きの SQL + 入れ子の record `Row` → `Restore(...)` で集約を復元する
- 集約の更新は「ヘッダ UPDATE + 明細 DELETE→INSERT(洗い替え)」をトランザクションで行う
- エラー: 不変条件違反は `DomainException`(→400)、未検出は `NotFoundException`(→404)。メッセージは日本語で、ユーザーに見せられる文にする
- WebApplicationFactory を使うため、`Program.cs` 末尾の `public partial class Program {}` を消さない

### フロントエンド

- 依存の追加は最小限。React と Router のみで、UI ライブラリ・チャートライブラリは使わない(チャートは手書きの SVG)
- `api.ts` の型は生成した型から導く(区分などの文字列は `Override` で 4 値に絞る)。手書きの interface を増やさない
- 金額の表示は `formatYen`/`formatSignedYen`、区分名は `categoryLabel`、半期は `halfLabel`
- 対象の半期は `?half=` クエリでページ間を引き回す(既定は `currentFiscalHalf()`)
- 差異の色は、コスト = 正が赤(利益が減る)、売上高・損益 = 正が緑(利益が増える)
- ダークモード対応は必須(CSS 変数)
- エラーは各ページの `error`/`setError` の state を `<Toast message={error} onClose={() => setError(null)} />`(`components/Toast.tsx`。画面右下に固定・自動で消える)で出す。最上部のバナー(`.error-banner`)は廃止済み(Issue #4)。新しいページも Toast を使う
- チャートを足す・変えるときは、検証済みのパレット(`frontend/src/styles.css` の CSS 変数)を使う(Claude Code なら dataviz スキルも先に読む)

## 静的解析

- バックエンドは `backend/Directory.Build.props` で `AnalysisLevel=latest-recommended` + 警告をエラー扱い。ロケール依存の `ToString`/`Parse`(CA1305)、比較方法を指定しない文字列比較(CA1310)などはビルドが通らない
- ルールを外すときは、リポジトリ直下の `.editorconfig` に理由のコメントつきで書く。抑制属性をコードに散らさない。テスト向けの緩和は `[tests/**.cs]`
- `dotnet format` は既存コードの意図的な桁揃えを崩すため、CI では強制しない
- フロントは ESLint + Prettier(CI で検査する。書式は `npm run format` で直す)

## コメント

- C# は XML コメントの `<summary>`。ドメイン層は日本語、他の層は英語
- クラスとメソッドには必ず `<summary>` を書く。パラメータ・戻り値のコメントは必要に応じて
- フロントは JSDoc(`/** ... */`)。型定義・関数・コンポーネントに必ず書く

## ドキュメントの同期

| 文書 | 直すとき |
|---|---|
| `README.md` | API の追加、費目のシードの変更、テストの種類の追加、実行手順の変更、スキーマの作り直し |
| `docs/MANUAL.md` | 画面・操作の流れの変更。FAQ に新しいつまずきが出たとき |
| `docs/design/e2e-test-guide.html` | E2E のテストケースの追加・変更・数値の変更 |
| `docs/DESIGN.md` | 集約・ドメインサービス・層の構成・不変条件の変更 |

- 費目マスタの一覧は `DatabaseInitializer` のシードと突き合わせて書く(推測で書かない)
- 差異の符号のルール(原価 = 正で利益が減る、売上 = 正で利益が増える。「有利/不利」という語は使わない)は README・MANUAL・DESIGN の 3 か所に出てくる。変えるときは 3 か所とも直す
- 図はすべて Mermaid。クラス図のステレオタイプは `<<Aggregate Root>>` `<<Value Object>>` `<<Domain Service>>` `<<record>>`
- 図を変えたら mermaid-cli でレンダリングを確かめる(構文エラーは GitHub で初めて分かることが多い)

```bash
npm install --prefix frontend @mermaid-js/mermaid-cli --no-save
echo '{"executablePath": "/opt/pw-browsers/chromium", "args": ["--no-sandbox"]}' > "$TMPDIR/pp.json"
frontend/node_modules/.bin/mmdc -p "$TMPDIR/pp.json" -i diagram.mmd -o "$TMPDIR/out.svg"
```

- classDiagram の落とし穴: メンバ行の半角カッコ + 等号(`(null=共通費)`)や演算子の表記はパースを壊すことがある。注記は `※〜` の形にする。型は `List~T~ Name` の形
- 分岐や棒グラフなどの説明は、スクリーンショットではなくテキストで書く(古くならないように)

## コミット前のチェックリスト([D])

```bash
cd backend && dotnet test                              # 全テスト(ドメイン・アプリ・インフラ・API E2E・アーキテクチャ)
cd frontend && npm run build                           # 型チェック + ビルド
cd frontend && npm run lint && npm run format:check    # ESLint + Prettier
cd tests/unit/frontend && npm test                     # 画面を触った場合
cd tests/e2e/browser && npm test                       # 画面を触った場合
```

- `.feature` のシナリオが、Skipped 0 で全件 Passed になること

## 画面要件([B] の画面)

- 画面要件は `requirements/screens/screens.yaml`(画面 ID・ルート・項目・表示の条件・操作・`requirements: [FR-…]`)と `requirements/screens/fixtures/<SCR-xxx>/<状態>.json`(通常・空・エラー・境界値。API の応答の形のまま)
- `requirements/screens/approved/` には書かない。承認した画面の画像を撮るのは人
- 画面の差分の検査は `bash tools/factory/screens-docker.sh`(CI と同じコンテナ)。新しい状態の画像がないと検査は落ちる。それは人が撮る
