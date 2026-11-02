---
name: docs-update
description: ドキュメント(README / docs/MANUAL.md / docs/DESIGN.md)の更新規約。機能・スキーマ・画面・API・費目マスタを変更したらどの文書のどこを同期するか、Mermaid図の検証方法。ドキュメントに触れる作業で必ず読む。
model: claude-haiku-4-5
---

# ドキュメント更新

**正本は `.takt/facets/knowledge/factory-dev.md`。この作業を始める前に必ず読み、それに従う。**
このスキルは、Claude Code がその正本を見つけるための案内。中身はここに書かない(工場の workflow と同じ正本を、Claude・Codex・Copilot のどれでも読むため。#94)。

読む節は「ドキュメントの同期」(利用者向けの文書と、工場の文書の同期表、Mermaid の検証)。

## Claude Code だけの補足

- 既存の文書の同期・追記は `claude-haiku-4-5` で十分。新しい設計文書の書き下ろしや図の再設計は `claude-sonnet-5` 以上
