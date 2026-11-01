---
name: docs-update
description: ドキュメント(README / docs/MANUAL.md / docs/DESIGN.md)の更新規約。機能・スキーマ・画面・API・費目マスタを変更したらどの文書のどこを同期するか、Mermaid図の検証方法。ドキュメントに触れる作業で必ず読む。
model: claude-haiku-4-5
---

# ドキュメント更新スキル

**利用モデル**: 既存文書の同期・追記は `claude-haiku-4-5` で十分。
新しい設計文書の書き下ろしや図の再設計は `claude-sonnet-5` 以上。

## 文書の役割と同期ルール

| 文書 | 役割 | 更新トリガ |
|---|---|---|
| `README.md` | 概要・技術スタック・アーキテクチャ・費目マスタ・実行方法・API一覧・テスト説明 | API追加、費目シード変更、テスト種類の追加、実行手順の変更 |
| `docs/MANUAL.md` | 操作マニュアル(計画策定→実績入力→計画変更の順) | 画面・操作フローの変更、FAQに新しいつまずきが出たとき |
| `docs/design/e2e-test-guide.html` | E2Eテスト(API・ブラウザ)の各ケースの解説 | E2E テストケースの追加・変更・数値の変更 |
| `docs/DESIGN.md` | コンテキストマップ/C4/クラス図(Mermaid) | 集約・ドメインサービス・層構成・不変条件の変更 |

工場(ソフトウェアファクトリー)の文書は、利用者向けの文書とは別に同期する。

| 文書 | 役割 | 更新トリガ |
|---|---|---|
| `AGENTS.md` | 工場の流れ・置き場・守ること(目次) | 工程・置き場・ブランチの命名の変更 |
| `knowledge/`・`requirements/`・`specs/` の README | 各工程の成果物の形 | 成果物の形の変更(工場の変更の種類 C) |
| `.factory/changes/<Issue番号>.md` | 工場の変更の記録(1 件 1 ファイル。`.factory/CHANGELOG.md` は版と変更の種類の説明) | 工場の設定を変えたとき毎回 |
| `docs/adr/0015-software-factory.md` | 工場の決定と、定義書との違い | 違いが増えたとき |
| `docs/factory/README.md` | 工場を回す人のための案内(決まりは写さず、正へのリンクだけ) | 人の作業・頼み方・ゲート後の手順・ハーネス・検査の数・よくあるつまずきが変わったとき。リンク先のファイルを動かしたとき |

- 費目マスタの一覧は `DatabaseInitializer` のシードと**突き合わせて**記載する(推測で書かない)
- 差異の符号規則(原価=正で利益が減る、売上=正で利益が増える。「有利/不利」という語は使わない)は README・MANUAL・DESIGN の3箇所に
  登場する。変更時は全部直す

## Mermaid 図の規約と検証

- 図はすべて Mermaid(GitHub でレンダリングされる)。クラス図のステレオタイプは
  `<<Aggregate Root>>` `<<Value Object>>` `<<Domain Service>>` `<<record>>` を使う
- **コミット前に mermaid-cli でレンダリング検証する**(構文エラーはGitHub上で初めて発覚しがち):

```bash
# 図をブロックごとに .mmd へ抽出して検証(過去の手順)
npm install --prefix frontend @mermaid-js/mermaid-cli --no-save
echo '{"executablePath": "/opt/pw-browsers/chromium", "args": ["--no-sandbox"]}' > /tmp/pp.json
frontend/node_modules/.bin/mmdc -p /tmp/pp.json -i diagram.mmd -o out.svg
```

- classDiagram の落とし穴: メンバ行の半角カッコ+等号(`(null=共通費)`)や演算子表記は
  パースを壊しうる。注記は `※〜` 形式にする。型は `List~T~ Name` 形式
- 図を変えたら PNG に出して目視確認(ラベル衝突・重なり)。
  分岐棒グラフ等の説明はスクリーンショットではなくテキストで書く(陳腐化防止)
