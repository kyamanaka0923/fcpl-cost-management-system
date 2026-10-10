/// <reference types="vite/client" />

/** ビルド時に埋め込む環境変数(未設定なら認証なしで動く)。 */
interface ImportMetaEnv {
  /** Cognito Hosted UI のベース URL。 */
  readonly VITE_AUTH_DOMAIN?: string
  /** Cognito アプリクライアント ID。 */
  readonly VITE_AUTH_CLIENT_ID?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
