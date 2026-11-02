---
name: issue-response
description: GitHub Issue を確認し、工場のラインで進めてよい工程があるものを tools/factory/takt.sh で進める手順。動かす Issue の選び方・起動スクリプトの呼び方・結果の伝え方。「Issueを確認して対応」「未対応のIssueをやって」等の依頼、および定期チェック(/loop)で必ず読む。
model: claude-opus-5
---

# Issue 対応

対象リポジトリ: **`kyamanaka0923/fcpl-cost-management-system`**(public)。
工場のラインは `tools/factory/takt.sh` で動かす。このスキルは、どの Issue を動かすかを選び、起動スクリプトを呼んで、結果を人に伝えるだけ(#94)。
決まりの正本は `.takt/facets/knowledge/factory-line.md`。**Issue を取って実装まで一気に進めることはしない**(1 回で 1 つの工程だけ。ゲートの手前で止まる)。

## 1. 動かす Issue を選ぶ

- 人が番号を指定した(「Issue #45 を進めて」): その番号を使う
- 指定がない(「Issue を確認して進めて」・定期実行): open の Issue を古い番号から見て、次をすべて満たす最初の 1 件
  - `node tools/factory/line-cli.mjs <n>` が成功する(ラベルから進めてよい工程が決まる)
  - その工程のブランチ(`<種類>/<n>-*`)がリモートにない。あれば人のゲート待ちか手直し待ちなので、定期実行では続きを勝手に動かさない(人が番号を指定したときだけ続きを動かす)

```bash
gh issue list --state open --json number --jq '.[].number' | sort -n
git ls-remote --heads origin '<種類>/<n>-*'
```

選べる Issue がなければ、何もしないで終える。

## 2. 起動スクリプトを呼ぶ

```bash
tools/factory/takt.sh <n>              # Claude(既定)
tools/factory/takt.sh <n> codex        # エージェントを指定するとき
```

- リポジトリの checkout で、作業ツリーがきれいな状態で呼ぶ(未コミットの変更があると起動スクリプトが止まる)
- 時間がかかる(書く ⇄ 検証 → publish)。終わるまで待ち、出力の最後の結果(COMPLETE / ABORT)を読む

## 3. 結果を伝える

- 起動スクリプトが止めた(工程が選べない・ブランチが重なる など): その理由をそのまま伝える
- COMPLETE: 出した Draft PR の番号と URL、人に決めてほしいこと(PR のレビューView の「問い」)を伝える。人の回答が要って止めたときは、Issue に出した問いを伝える
- ABORT: takt の報告(`.takt/runs/<最新>/reports/`)から、止まった step と理由を伝える
- **マージ・Ready 化・承認・ゲートラベルの付与はしない**(人の仕事。ガードが止める)

## 実行のトリガ

- 手動: 「Issue を確認して進めて」「Issue #45 を進めて」など
- 定期: `/loop 30min …`。発火時にこの手順を実行し、選べる Issue がなければ何もしないで終える
