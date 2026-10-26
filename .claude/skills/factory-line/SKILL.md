---
name: factory-line
description: 工場のラインで作業項目(Issue)を進める手順。Issue の列(stage:…)とゲートラベルを見て、いま進めてよい工程だけを、どのエージェントに渡すかを決める。「Issue を進めて」「次の工程へ」「ラインを回して」などの依頼、作業項目に着手するときに必ず読む。
model: claude-opus-5
---

# 工場のライン

工場の流れは `AGENTS.md`、考え方は ADR-0015。ここは「いま何をしてよいか」を決める手順。

## 1. 列とゲートラベルを見る

```bash
gh issue view <n> --json number,title,labels,state --jq '{number,title,state,labels:[.labels[].name]}'
```

| Issue の状態 | 進めてよい工程 | 使うエージェント | ブランチ |
|---|---|---|---|
| `stage:準備中`、`gate0-approved` なし | [A] ナレッジ | `knowledge-extractor` | `knowledge/<n>-…` |
| `stage:準備中`、`gate0-approved` あり | [B] 要件・画面 | `requirements-writer` → `requirements-reviewer`(画面はフェーズ5 以降 `screen-writer`) | `req/<n>-…` |
| `stage:着手待ち`(`gate1-approved`) | [C] 仕様 | `spec-writer` | `spec/<n>-…` |
| `stage:仕様確定`(`gate2-approved`) | [D] 実装 → [E] 検証 | `implementer` → `code-reviewer` | `impl/<n>-…` |
| `stage:実装中` | [D] の続き(CI の失敗・レビューの指摘への対応) | `implementer` | 同じ PR |
| `stage:判断待ち`、`needs-answer` | **何もしない**(人の回答を待つ) | — | — |

- **列を飛ばさない。** 表にない組み合わせ(例: `stage:準備中` で `gate1-approved` あり)は、自分で直さずに人に報告する
- ナレッジに足すものがない作業項目は、[A] を「変更なし」と PR なしで済ませてよい。そのときも `gate0-approved` は人が付ける
- 列のラベル(`stage:…`)は、エージェントが付け替えてよい。**ゲートラベル(`gate…-approved`)は付けない・外さない**
  - 着手したら `stage:実装中` など次の列にする。問いで止めたら `stage:判断待ち` と `needs-answer`
  - ゲートを通ったときの列の移動(`stage:準備中` → `stage:着手待ち` など)は、人がゲートラベルを付けるときにする

## 2. エージェントに渡す

Agent ツールで、表のエージェントを `subagent_type` に指定して渡す。プロンプトには次を書く。

- Issue 番号と、その工程で作るもの
- ブランチ名(main から切る)
- 前の工程の PR(マージ済み)の番号

**生成と検証を分ける。** 書いたエージェントと同じ会話でレビューしない。レビューは `requirements-reviewer`・`code-reviewer` を別に起動する。

## 3. 止めるとき

- 上流([A][B])で分からないこと → `knowledge/open-questions.md` に問いを書き、PR の「問い」に出す。仮決めを置いてよい(金額・税・権限・個人情報は除く)
- 本ライン([C][D])で要件・仕様の穴を見つけた → **作業を止める**
  ```bash
  gh issue edit <n> --add-label needs-answer --add-label stage:判断待ち
  gh issue comment <n> --body-file <問いを書いたファイル>
  ```
  問いは Yes/No か、選択肢で答えられる形にする
- 上流の前提が変わった(`.factory/premises/ISSUE-<n>.yml` の版と main が違う)→ 同じく止める

## 4. 前提を書く([C][D])

```bash
bash tools/factory/premises.sh <n> <spec|impl> <読んだ要件・仕様・業務ルールのファイル>...
```

書き方は `.factory/premises/README.md`。

## 5. PR を出す

- `.github/pull_request_template.md` の欄をすべて埋める(当てはまらない欄は「なし」)。工場の版は `.factory/VERSION`
- レビューView の「問い」は Yes/No で答えられる形にする。作り方は `gate-review` スキル
- プッシュしたら `gh pr checks <番号> --watch` で CI を見届ける
- **マージ・Ready 化・承認はしない。** フックに止められたら、回避せずに人に頼む

## 6. 判断待ちが溜まったら(WIP の上限)

| 指標 | 上限(導入計画 §17.5 の初期値) | 超えたら |
|---|---|---|
| `stage:判断待ち` | 5 件 | 新しい工程に着手せず、`prep` の作業(判定を楽にする下ごしらえ)をする |
| 未回答の問い(open-questions.md) | 10 件 | 同上 |
| `stage:着手待ち` | 2 件以上を保つ | 上流([A][B])の作業を優先する |

`prep` の作業: 判断待ちの PR のレビューView を見直す、問いを選択肢の形に直す、テストを足す(`prep/<n>-…` ブランチ、`prep` ラベル)。
