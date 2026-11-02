---
name: testing
description: テストの追加・修正・実行の規約。tests/ のスコープ別構成(unit/integration/e2e/architecture)、バックエンドテスト(ドメイン/アプリケーション/インフラ/API E2E/アーキテクチャ)、フロントエンド単独テスト(Vitest + MSW)、ブラウザE2E(Playwright + playwright-bdd)・日本語 Gherkin シナリオの書き方、日本語テスト名の方針、既知の落とし穴。テストを書く・直す作業で必ず読む。
model: claude-sonnet-5
---

# テスト

**正本は `.takt/facets/knowledge/factory-testing.md`。この作業を始める前に必ず読み、それに従う。**
このスキルは、Claude Code がその正本を見つけるための案内。中身はここに書かない(工場の workflow と同じ正本を、Claude・Codex・Copilot のどれでも読むため。#94)。

## Claude Code だけの補足

- `claude-sonnet-5`。テスト戦略自体の再設計は `claude-opus-4-8`
