---
name: feature-dev
description: 機能追加・変更の標準ワークフロー。バックエンド(ヘキサゴナル各層)とフロントエンド(React)の実装手順、レイヤごとの置き場所、コーディング規約、コミット前の検証手順。新機能・画面追加・API追加のタスクで必ず読む。
model: claude-sonnet-5
---

# 機能開発

**正本は `.takt/facets/knowledge/factory-dev.md`。この作業を始める前に必ず読み、それに従う。**
このスキルは、Claude Code がその正本を見つけるための案内。中身はここに書かない(工場の workflow と同じ正本を、Claude・Codex・Copilot のどれでも読むため。#94)。

あわせて読むもの: テストは `.takt/facets/knowledge/factory-testing.md`、ドメインを変えるなら `.takt/facets/knowledge/factory-domain.md`、DB スキーマを変えるなら `.takt/facets/knowledge/factory-schema.md`、CI とプッシュは `.takt/facets/knowledge/factory-ci-env.md`。

## Claude Code だけの補足

- 通常の機能追加は `claude-sonnet-5`。ドメインモデル自体を変えるときは先に domain-design(`claude-opus-4-8`)
- チャートを足す・変えるときは、dataviz スキル(バンドル)も先に読む
- 工場のラインの作業項目([D] 実装)として進めるなら、手で書かずに `tools/factory/takt.sh <Issue番号>` を使う(issue-response スキル)
