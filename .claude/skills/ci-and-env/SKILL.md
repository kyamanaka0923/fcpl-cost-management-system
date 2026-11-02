---
name: ci-and-env
description: CI(GitHub Actions)・開発環境(Docker/Dev Container/リモート環境)・スキーマ移行・リリース検証の手順。CIの修正、環境構築、プッシュ後の確認、DBスキーマ変更を伴う作業で必ず読む。
model: claude-sonnet-5
---

# CI・環境

**正本は `.takt/facets/knowledge/factory-ci-env.md`。この作業を始める前に必ず読み、それに従う。**
このスキルは、Claude Code がその正本を見つけるための案内。中身はここに書かない(工場の workflow と同じ正本を、Claude・Codex・Copilot のどれでも読むため。#94)。

DB スキーマを変えるときは `.takt/facets/knowledge/factory-schema.md` も読む。

## Claude Code だけの補足

- `claude-sonnet-5`。CI 設定の微修正やジョブの結果の確認だけなら `claude-haiku-4-5` でよい
