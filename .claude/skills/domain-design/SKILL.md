---
name: domain-design
description: ドメインモデルの変更・追加・設計判断を行うときに必ず読む。集約の追加、不変条件の変更、課予算のバージョン管理、区分(売上高/加工費/外注費/期間費用)と案件・費目の対応付け、分析サービスの追加など、backend/src/CostManagement.Domain に触れる作業全般が対象。
model: claude-opus-4-8
---

# ドメイン設計

**正本は `.takt/facets/knowledge/factory-domain.md`。この作業を始める前に必ず読み、それに従う。**
このスキルは、Claude Code がその正本を見つけるための案内。中身はここに書かない(工場の workflow と同じ正本を、Claude・Codex・Copilot のどれでも読むため。#94)。

## Claude Code だけの補足

- 設計判断を伴うので `claude-opus-4-8` 以上を推奨(大規模なモデルの変更は Fable 5)。機械的なリネームや復元ファクトリの修正だけなら `claude-sonnet-5` でよい
