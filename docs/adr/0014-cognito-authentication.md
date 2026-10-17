# ADR-0014: 認証は Amazon Cognito(Hosted UI + PKCE)と API の JWT 検証で行う

- 状態: Accepted
- 日付: 2026-10
- 関連: [ADR-0010](0010-serverless-deployment.md)、`backend/src/CostManagement.WebApi/AuthSetup.cs`、
  `frontend/src/auth.ts`、[infra/template.yaml](../../infra/template.yaml)

## 背景

AWS 構成(ADR-0010)では Lambda Function URL が `AuthType: NONE` で、アプリにも認証がなかった。
デプロイすると予算・実績を誰でも閲覧・変更できる状態だった。

## 決定

- 利用者管理は **Amazon Cognito ユーザープール**で行う。自己サインアップは不可で、管理者が登録する。
- フロントは **Hosted UI の認可コードフロー + PKCE** でログインする(クライアントシークレットはブラウザに置けないため持たない)。
  トークンは sessionStorage に置き、API には**アクセストークン**を `Authorization: Bearer` で付ける。
- WebApi は `Auth:Authority`(ユーザープールの発行者 URL)が設定されていれば、すべての `/api` で
  **JWT(署名・発行者・有効期限・`token_use=access`)を検証**する。`Auth:ClientId` を設定すれば発行先クライアントも検証する。
- **Development 以外で `Auth:Authority` が未設定なら起動を失敗させる**(認証なしで本番に出る事故を防ぐ)。
  ローカル開発・テスト(Development)では未設定なら認証なしで動く。
- フロントの認証はビルド時の `VITE_AUTH_DOMAIN` / `VITE_AUTH_CLIENT_ID` で有効になる(`infra/deploy.sh` が埋め込む)。

## 結果

- デプロイ環境では、ログインした利用者だけが API を使える。Function URL を直接叩かれてもトークンがなければ 401 になる。
- Lambda には発行者 URL だけを渡し、アプリクライアント ID は渡さない。クライアントのコールバック URL が
  CloudFront のドメインに依存し、CloudFront が Lambda に依存するため、渡すと循環参照になるからである。
  ユーザープールのクライアントは1つだけなので、発行者の検証で足りる。
- 認可(役割による操作制限)と、承認者などの操作者の記録はまだない。必要になったら JWT の
  `sub` / `username` を使って追加する。
