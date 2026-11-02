---
name: factory-line
description: 工場のラインで作業項目(Issue)を進める手順。Issue の列(stage:…)とゲートラベルを見て、いま進めてよい工程だけを、どのエージェントに渡すかを決める。「Issue を進めて」「次の工程へ」「ラインを回して」などの依頼、作業項目に着手するときに必ず読む。
model: claude-opus-5
---

# 工場のライン

**正本は `.takt/facets/knowledge/factory-line.md`(判定表のコードは `tools/factory/lib/gate-labels.mjs`)。必ず読み、それに従う。**

工場のラインの作業項目は、手で工程を進めない。`tools/factory/takt.sh` で動かす(#94)。

```bash
tools/factory/takt.sh <Issue番号> [knowledge|req|spec|impl] [claude|codex|copilot]
```

- 工程を省くと、ラベルから選ぶ。表に当たらなければ、理由を出して何もしない
- 同じコマンドをもう一度実行すると「続き」になる(CI の失敗・PR の指摘を直す)
- 担当(書く役・検証する役)は `.takt/facets/personas/`、手順は `.takt/workflows/` の4つ(Knowledge・Requirements・Spec・Implement)
