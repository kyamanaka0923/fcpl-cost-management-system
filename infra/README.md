# AWS デプロイ(サーバーレス構成)

EC2・コンテナを使わず、費用を極力抑えたサーバーレス構成の IaC(AWS SAM)です。

## 構成図

```
                ┌──────────── CloudFront(1ドメイン / HTTPS / キャッシュ)────────────┐
  ブラウザ ────▶│  /            → S3(React 静的ビルド, 非公開 + OAC)                │
                │  /api/*       → Lambda Function URL(.NET 10 バックエンド)         │
                └───────────────────────────────────────────────────────────────────┘
                                          │
                                Lambda(.NET 10 / provided.al2023 / arm64)
                                   予約同時実行 = 1(SQLite 書き込み直列化)
                                          │  EFS マウント(/mnt/efs/costmanagement.db)
                                   Amazon EFS(SQLite 本体を永続化)
```

- **バックエンド**: ASP.NET Core Minimal API を `Amazon.Lambda.AspNetCoreServer.Hosting` で Lambda 実行。
  `provided.al2023` カスタムランタイムに **self-contained 発行した zip** をデプロイ(**コンテナ不使用**)。
- **DB**: SQLite ファイルを **EFS** に置き Lambda にマウント。コードはほぼ現状のまま
  (接続文字列を環境変数 `ConnectionStrings__Default` で EFS パスに向けるだけ)。
- **フロント**: `vite build` の成果物を **S3 + CloudFront**。`/api` は相対パスなので
  CloudFront が同一ドメインで振り分け、フロント側のコード変更は不要。
- **NAT/IGW なし**: Lambda は外部通信しないため NAT ゲートウェイ(月約$32〜)を置かない。
- **バックアップ**: **AWS Backup** が EFS(SQLite)を毎日 03:00 JST にバックアップし、
  保管庫 `<ProjectName>-vault` に既定 35 日保持する(`sam deploy --parameter-overrides BackupRetentionDays=90` などで変更)。
- **ログ**: Lambda のログは CloudWatch Logs(`/aws/lambda/<ProjectName>-api`)に **1行1 JSON** で出る。
  業務イベント(EventId 1001〜1006)には操作した利用者(`User`)が付く。
- **認証**: **Amazon Cognito** のユーザープール + Hosted UI。フロントは認可コード + PKCE でログインし、
  API(Lambda)は環境変数 `Auth__Authority` の発行者が出したアクセストークンだけを受け付ける(なければ 401)。

## 前提ツール

- AWS アカウントと認証情報(`aws configure` 済み)
- [AWS SAM CLI](https://docs.aws.amazon.com/serverless-application-model/latest/developerguide/install-sam-cli.html)
- .NET 10 SDK / Node.js 20+ / `make` / Python 3

## デプロイ手順

```bash
cd infra
./deploy.sh                 # ビルド → デプロイ → フロント配信 → キャッシュ無効化
# 別スタック名/リージョンにする場合:
STACK=fcpl-cost REGION=ap-northeast-1 ./deploy.sh
```

`deploy.sh` は次を自動で行います:

1. `sam build`(`backend/src/CostManagement.WebApi/Makefile` が `dotnet publish -r linux-arm64 -p:LambdaPublish=true` で `bootstrap` を生成)
2. `sam deploy`(VPC / EFS / Lambda / S3 / CloudFront を作成)
3. フロントを `npm run build` して S3 へ `aws s3 sync`
4. CloudFront のキャッシュ無効化

完了後に表示される **CloudFront URL** がアプリの入口です。

### 利用者の登録(初回)

自己サインアップはできません。管理者が Cognito に利用者を登録します(`deploy.sh` の最後にコマンドが表示されます)。

```bash
aws cognito-idp admin-create-user --user-pool-id <UserPoolId> --username <メールアドレス> --region ap-northeast-1
```

招待メールの仮パスワードでログインすると、初回にパスワードの変更を求められます(12文字以上・大小英字と数字を含む)。

## コスト目安(小規模・社内利用)

| リソース | 目安 | 備考 |
|---|---|---|
| Lambda | ほぼ無料 | 月100万リクエストまで無料枠。arm64 で単価も安い |
| EFS | 月 数円〜 | 数十MBのSQLite想定。Bursting スループット |
| S3 | 月 数円 | 静的ファイルのみ |
| CloudFront | 月 数円〜 | 1TB/月まで無料枠あり |
| Cognito | ほぼ無料 | 小規模の月間アクティブユーザーは無料枠内 |
| AWS Backup | 月 数円〜 | EFS バックアップは保存容量に比例(数十MB × 保持日数の差分) |
| **合計** | **月 数百円以内** | 使用量が小さければ実質ほぼ無料〜 |

## 設計上の注意 / チューニング

- **SQLite の同時書き込み**: `ReservedConcurrentExecutions: 1`(template.yaml)で Lambda を
  常に1インスタンスに制限し、書き込みを直列化しています。単一利用者の内部ツール前提。
  同時アクセスが増えてボトルネックになったら、ヘキサゴナル設計を活かして
  リポジトリ実装を **DynamoDB** 等へ差し替える段階移行が可能です。
- **コールドスタート**: self-contained のみ(R2R/AOT なし)で概ね1〜2秒。短縮したい場合は
  `CostManagement.WebApi.csproj` の `LambdaPublish` 条件に `<PublishReadyToRun>true</PublishReadyToRun>`
  を追加(クロスアーキビルドが不安定な環境では CodeBuild/arm64 上でのビルドを推奨)。
- **セキュリティ**: API はアプリ側で Cognito のアクセストークンを検証するため、Function URL
  (`AuthType: NONE`)を直接呼ばれてもトークンがなければ 401 になります。ネットワークの段階でも
  直アクセスを塞ぎたい場合は `AuthType: AWS_IAM` に変更し、CloudFront の **OAC(Lambda 用)** で
  SigV4 署名する構成に切り替えてください。
- **メモリ/タイムアウト**: `MemorySize: 512` / `Timeout: 30`。負荷に応じて調整。

## ログの調べ方(CloudWatch Logs Insights)

ロググループ `/aws/lambda/<ProjectName>-api` を選び、例えば次のように検索します。

```
# 誰がいつ予算を承認したか
fields @timestamp, Scopes.3.User as user, State.BudgetId as budget, State.FiscalHalf as half, State.Version as version
| filter EventId = 1002
| sort @timestamp desc

# エラー応答(400/404)とその理由
fields @timestamp, Scopes.3.User as user, Message
| filter EventId in [2002, 2003]
```

(`Scopes` の位置は ASP.NET Core が付ける標準スコープの数で決まります。ずれる場合は `fields Scopes` で確認してください)
ロググループの保持期間は既定で無期限です。費用を抑えるなら CloudWatch のコンソールで保持期間(例: 90 日)を設定してください。

## バックアップからの復元

AWS Backup の保管庫 `<ProjectName>-vault` に、EFS の日次の復旧ポイントがたまります。

1. AWS コンソールの **AWS Backup → 保管庫 → `<ProjectName>-vault`** で、戻したい日時の復旧ポイントを選んで「復元」
2. 復元先は「既存のファイルシステム」を選び、**項目レベルの復元**で `costmanagement.db` を指定する
   (EFS 内の `aws-backup-restore_<日時>/` ディレクトリに復元される)
3. Lambda の予約同時実行数を一時的に 0 にして書き込みを止める
4. EFS をマウントできる環境(同じ VPC・EFS 用セキュリティグループに一時的に立てた EC2 など。
   このスタックには常設していない)で、復元されたファイルを EFS 直下の `costmanagement.db` に上書きする
5. 予約同時実行数を 1 に戻し、一時的な EC2 を削除する

## 片付け(削除)

```bash
# フロントのオブジェクトを空にしてからスタック削除(S3 バケットは中身があると消せない)
aws s3 rm "s3://$(aws cloudformation describe-stacks --stack-name fcpl-cost \
  --query "Stacks[0].Outputs[?OutputKey=='FrontendBucketName'].OutputValue" --output text)" --recursive
sam delete --stack-name fcpl-cost
```

バックアップの保管庫(`<ProjectName>-vault`)はデータ保護のため、スタックを削除しても残ります。
不要になったら、復旧ポイントを削除してから保管庫を削除してください。

> 注: この環境(サンドボックス)からは実 AWS へデプロイできません。AWS 認証情報のある
> 手元/CI 環境で `deploy.sh` を実行してください。デプロイ前に `sam validate --lint` で
> テンプレートを検証することを推奨します(本リポジトリの CI では SAM CLI 未導入のため未実行)。
