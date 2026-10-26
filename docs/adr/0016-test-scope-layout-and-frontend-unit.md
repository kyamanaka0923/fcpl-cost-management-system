# ADR-0016: テストをスコープ別の tests/ にまとめ、フロントエンド単独テストを加える

- 状態: Accepted
- 日付: 2026-10-25
- ADR-0011 の「テストの種類と置き場所」を置き換える(カバレッジ・ミューテーションの方針は ADR-0011 のまま)

## 背景

テストが `backend/tests` と `frontend/e2e` に分かれ、どこに何のテストがあるかが見えにくかった。
ブラウザ E2E はバックエンドと Vite を起動して 1 つの DB を共有するため、遅く、画面の細かい分岐まで E2E で確かめると壊れやすかった。

## 決定

- テストはリポジトリ直下の `tests/` に、スコープで分けて置く
  - `unit/`: Domain と、フロントエンド単独のテスト
  - `integration/`: Application と Infrastructure
  - `e2e/`: API E2E とブラウザ E2E
  - `architecture/`: NetArchTest による依存ルールの検証
- `tests/unit/frontend` を足す。使う道具は Vitest・jsdom・Testing Library で、API は MSW で置き換える。画面の分岐の確かめはここに移す
- ブラウザ E2E は、主要な業務の流れに絞る
- 業務フローは日本語 Gherkin でも書く。Application 層は Reqnroll、API E2E は Reqnroll、ブラウザ E2E は playwright-bdd を使う

## 結果

- フロントエンドの画面の確かめが数秒で終わる。ブラウザ E2E の本数が減り、CI が安定する
- MSW の偽 API は、サーバの業務ルールの一部(承認済みは編集不可など)を写しているので、サーバ側が変わったら合わせて直す必要がある。
  そのずれは、ブラウザ E2E と API E2E が拾う
