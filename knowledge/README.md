# ナレッジ(knowledge/)

要件の上流に置く、この業務の知識の正(SSoT)。エージェントは要件・仕様を書くとき、ここを入力にする
(PROC-DEV-001 3.0、[ADR-0015](../docs/adr/0015-software-factory.md))。

| ファイル | 中身 |
|---|---|
| [glossary.md](glossary.md) | 用語集(ユビキタス言語) |
| [business-rules/](business-rules/) | 業務ルール。1 ルール 1 ファイル(`BR-<領域>-<連番>.md`) |
| [decisions/](decisions/README.md) | 判断の記録。正は [docs/adr/](../docs/adr/README.md)。ここは索引だけ |
| [open-questions.md](open-questions.md) | 未確定事項(問い・仮決め・回答) |
| [review-viewpoints.md](review-viewpoints.md) | レビューの観点 |
| [sources.md](sources.md) | 出どころの索引 |

## 書き方の決まり

1. **出どころを必ず書く。** 業務ルールの frontmatter の `sources` に、[sources.md](sources.md) の ID(`SRC-…`)を 1 つ以上書く。出どころのないルールは置かない
2. **確定と未確定を分ける。** `status: 確定` は、人がゲート0で出どころと照らして確かめたものだけ。AI が推測したものは `status: 未確定` にし、
   問いを [open-questions.md](open-questions.md) に書く。未確定のルールを要件から参照しない
3. **生データを置かない。** ヒアリングの録画・文字起こしは元の場所に残し、ここには人が確かめた整理版と出どころへの参照だけを置く
4. **小さく分ける。** 1 ルール 1 ファイル。用語集は追記を中心にし、意味を変えるときは判断の記録(ADR)に理由を残す
5. **作業項目に要る分だけ作る。** 全部を先に作らない。マージのたびに育てる

## 業務ルールの形

```markdown
---
id: BR-BUD-002
title: 予算の状態遷移
status: 確定            # 確定 | 未確定
sources: [SRC-REQ, SRC-ADR-0002]
requirements: [FR-5.1, FR-5.2]   # このルールを使う要件(移行済みのものは requirements/ に、未移行は docs/REQUIREMENTS.md にある)
---

## ルール
デシジョンテーブル・計算式・状態遷移表のどれかで書く。

## 例
境界を数値で示す(Gherkin のシナリオの素になる)。
```

ゲート0 の承認は、このディレクトリを変える PR(`knowledge/<n>-…` ブランチ)を人がマージし、Issue に `gate0-approved` を付けること。
