# ADR-0010: クラウドはサーバーレス構成(Lambda + EFS + S3 + CloudFront)で提供する

- 状態: Accepted
- 日付: 2026-07(IaC 追加時)
- 関連: [infra/](../../infra/)(AWS SAM テンプレート)

## 背景

「EC2 やコンテナを使わず、費用を極力抑えてバックエンドを動かしたい」という要望があった。
本システムは SQLite ファイル1つで完結する軽量構成(ADR-0007)である。

## 決定

- クラウドは**サーバーレス構成**とし、IaC は **AWS SAM** で `infra/` に用意する。
  - **バックエンド**: .NET 10 を **Lambda**(`provided.al2023` カスタムランタイム, arm64, 自己完結 bootstrap)で実行(コンテナ不使用)。
  - **DB**: SQLite ファイルを **EFS** に永続化。**Lambda の予約同時実行 = 1** で書き込みを直列化する。
  - **フロント**: React 静的ビルドを **S3 + CloudFront**。CloudFront が `/api/*` を Lambda に振り分け同一ドメイン化。
  - NAT レス VPC(コスト削減)。
- WebApi は `AddAWSLambdaHosting(LambdaEventSource.HttpApi)` を持つ(ローカルでは no-op)。

## 結果

- アイドル時のコストをほぼゼロにでき、常時起動サーバを持たない。
- 予約同時実行 = 1 のため書き込みスループットは低い(単一課・単一組織の利用規模では許容)。
- コールドスタートと EFS レイテンシがあり、高頻度・大規模用途には向かない(現状の想定利用規模では許容)。
- Docker デーモンのないリモート環境では compose の実検証はできない(ローカル PC で行う)。
