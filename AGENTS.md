# AGENTS.md

総合原価管理システム(予実管理)の**工場(ソフトウェアファクトリー)**で働くエージェントへの共通の指示。
ここは目次。細部はリンク先を読む。工場の考え方は [ADR-0015](docs/adr/0015-software-factory.md)、
元の文書は `software-factory/`(PROC-DEV-001 と導入計画)。

## 工場の流れ

```
上流:   [A] ナレッジ ─▶ ゲート0 ─▶ [B] 要件・画面 ─▶ ゲート1(投入ゲート)
本ライン: 着手待ち ─▶ [C] 仕様 ─▶ ゲート2 ─▶ [D] 実装 ─▶ [E] 検証(CI・レビューAI) ─▶ ゲート3(マージ)
```

- **すべてのゲートで、承認されるまで次の工程に進まない。** 承認 = 人が PR を main にマージすること
- ゲート0〜2 を通ったことは、Issue の**ゲートラベル**(`gate0-approved` / `gate1-approved` / `gate2-approved`)で表す。
  **ゲートラベルを付けるのは人だけ。エージェントは付けない・外さない**
- 作業項目(Issue)の列は `stage:準備中` → `stage:着手待ち` → `stage:仕様確定` → `stage:実装中` → `stage:判断待ち` → 完了。
  **列を飛ばして作業しない**(着手待ちでない Issue の仕様を書かない、仕様確定でない Issue を実装しない)

## 工程ごとの置き場とブランチ

| 工程 | 書く場所 | ブランチ |
|---|---|---|
| [A] ナレッジ | [`knowledge/`](knowledge/README.md)(用語・業務ルール・未確定事項・出どころ) | `knowledge/<Issue番号>-<名前>` |
| [B] 要件・画面 | [`requirements/`](requirements/README.md)(EARS)、`.feature` の `ルール:` と `@FR-x.y`、`requirements/screens/` | `req/<n>-…` |
| [C] 仕様 | [`specs/`](specs/README.md)、`docs/api/openapi.json`、テスト計画 | `spec/<n>-…` |
| [D] 実装 | `backend/`、`frontend/`、`tests/` | `impl/<n>-…` |
| 下ごしらえ | レビューView、テスト強化、ナレッジの穴の洗い出し | `prep/<n>-…` |
| 工場の変更 | `.claude/`、`AGENTS.md`、`.github/`、`tools/factory/`、`.factory/` | `factory/<名前>` |

- 1 作業項目 → 1 ブランチ → 1 PR。main に直接 push しない
- ブランチの種類ごとに触ってよい場所が決まっている(CI の `role-guard` が検査する)

## 守ること

- **推測で埋めない。** 上流([A][B])では、不明点を `knowledge/open-questions.md` に問いとして書き、下書きに仮決めを置いてよい。
  ただし金額・税の計算、権限・認可、個人情報は仮決めしない。**本ライン([C][D])で要件の穴を見つけたら、作業を止めて Issue に
  `needs-answer` を付け、問いを書く**
- **手で直さない。** 成果物の誤りは、ナレッジ・要件・仕様・工場のどこが悪かったかを探してそこを直す
- **テストは要件・仕様から導く。** コードからテストを作らない
- **生成と検証を分ける。** 自分が書いたものを自分でレビューしない(レビューは別のエージェント)
- 作業を始めたら、前提にした要件・仕様・ナレッジのファイルと版を `.factory/premises/ISSUE-<n>.yml` に書く([書き方](.factory/premises/README.md)。`tools/factory/premises.sh` で作る)
- PR は `.github/pull_request_template.md` の欄(工場の版・前提・レビューView・介入の記録)を埋める
- **マージ・PR の Ready 化・承認・ゲートラベルの付与はしない**(人の仕事)

## プロダクトの決まり

- 業務の決まり: `knowledge/`(用語集・業務ルール)。設計判断: [docs/adr/](docs/adr/README.md)
- アーキテクチャ: .NET 10 ヘキサゴナル + DDD(依存ルールは `tests/architecture/` が検査)/ React 19 / SQLite + Dapper
- テスト名・エラーメッセージ・コミットメッセージは日本語
- テストの構成と実行方法: [README.md](README.md) の「テスト」
