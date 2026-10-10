/**
 * Amazon Cognito Hosted UI によるログイン(認可コードフロー + PKCE)。
 *
 * ビルド時の環境変数 `VITE_AUTH_DOMAIN`(Hosted UI の URL)と `VITE_AUTH_CLIENT_ID` が
 * 両方あるときだけ有効になる。未設定(ローカル開発・テスト)では認証なしで動く。
 * トークンはタブを閉じると消える sessionStorage に保持する。
 */

/** Hosted UI のベース URL(例: https://xxx.auth.ap-northeast-1.amazoncognito.com)。 */
const domain = (import.meta.env.VITE_AUTH_DOMAIN ?? '').replace(/\/$/, '')
/** Cognito アプリクライアント ID。 */
const clientId = import.meta.env.VITE_AUTH_CLIENT_ID ?? ''

/** 認証が有効か(ビルド時の環境変数で決まる)。 */
export const authEnabled = domain !== '' && clientId !== ''

/** ログイン後に戻るパス(Cognito のコールバック URL に登録する)。 */
export const callbackPath = '/auth/callback'

const TOKENS_KEY = 'cm.auth.tokens'
const VERIFIER_KEY = 'cm.auth.verifier'
const STATE_KEY = 'cm.auth.state'
const RETURN_TO_KEY = 'cm.auth.returnTo'

/** 保持するトークン一式。 */
interface Tokens {
  accessToken: string
  idToken: string
  refreshToken?: string
  /** アクセストークンの有効期限(エポックミリ秒)。 */
  expiresAt: number
}

/** トークンエンドポイントの応答。 */
interface TokenResponse {
  access_token: string
  id_token: string
  refresh_token?: string
  expires_in: number
}

/** ログイン中の利用者(ID トークンから取り出す表示用の情報)。 */
export interface CurrentUser {
  /** 表示名(メールアドレス、なければユーザー名)。 */
  name: string
}

const redirectUri = () => `${window.location.origin}${callbackPath}`

function loadTokens(): Tokens | null {
  try {
    const raw = sessionStorage.getItem(TOKENS_KEY)
    return raw ? (JSON.parse(raw) as Tokens) : null
  } catch {
    return null
  }
}

function saveTokens(res: TokenResponse, previousRefresh?: string) {
  const tokens: Tokens = {
    accessToken: res.access_token,
    idToken: res.id_token,
    // リフレッシュ時の応答には refresh_token が含まれないため、元のものを引き継ぐ
    refreshToken: res.refresh_token ?? previousRefresh,
    expiresAt: Date.now() + res.expires_in * 1000,
  }
  sessionStorage.setItem(TOKENS_KEY, JSON.stringify(tokens))
}

/** URL セーフな Base64 文字列にする。 */
function base64Url(bytes: Uint8Array): string {
  let s = ''
  bytes.forEach((b) => (s += String.fromCharCode(b)))
  return btoa(s).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

function randomString(): string {
  return base64Url(crypto.getRandomValues(new Uint8Array(32)))
}

async function tokenRequest(params: Record<string, string>): Promise<TokenResponse> {
  const res = await fetch(`${domain}/oauth2/token`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({ client_id: clientId, ...params }),
  })
  if (!res.ok) throw new Error('ログインの有効期限が切れました。もう一度ログインしてください。')
  return (await res.json()) as TokenResponse
}

/** Hosted UI のログイン画面へ移動する(戻り先として現在のパスを覚えておく)。 */
export async function login(): Promise<void> {
  const verifier = randomString()
  const state = randomString()
  const challenge = base64Url(
    new Uint8Array(await crypto.subtle.digest('SHA-256', new TextEncoder().encode(verifier))),
  )
  sessionStorage.setItem(VERIFIER_KEY, verifier)
  sessionStorage.setItem(STATE_KEY, state)
  sessionStorage.setItem(RETURN_TO_KEY, window.location.pathname + window.location.search)
  const query = new URLSearchParams({
    response_type: 'code',
    client_id: clientId,
    redirect_uri: redirectUri(),
    scope: 'openid email profile',
    state,
    code_challenge_method: 'S256',
    code_challenge: challenge,
  })
  window.location.assign(`${domain}/oauth2/authorize?${query}`)
}

/**
 * コールバック URL で呼ぶ。認可コードをトークンに交換して保存し、ログイン前に見ていたパスを返す。
 */
export async function completeLogin(search: string): Promise<string> {
  const params = new URLSearchParams(search)
  const code = params.get('code')
  const verifier = sessionStorage.getItem(VERIFIER_KEY)
  if (!code || !verifier || params.get('state') !== sessionStorage.getItem(STATE_KEY))
    throw new Error('ログインに失敗しました。もう一度やり直してください。')
  saveTokens(
    await tokenRequest({
      grant_type: 'authorization_code',
      code,
      redirect_uri: redirectUri(),
      code_verifier: verifier,
    }),
  )
  sessionStorage.removeItem(VERIFIER_KEY)
  sessionStorage.removeItem(STATE_KEY)
  const returnTo = sessionStorage.getItem(RETURN_TO_KEY) ?? '/'
  sessionStorage.removeItem(RETURN_TO_KEY)
  return returnTo
}

/**
 * API 呼び出しに付けるアクセストークン。期限が近ければリフレッシュトークンで更新する。
 * ログインしていなければ null。
 */
export async function getAccessToken(): Promise<string | null> {
  const tokens = loadTokens()
  if (!tokens) return null
  if (tokens.expiresAt - Date.now() > 60_000) return tokens.accessToken
  if (!tokens.refreshToken) return null
  try {
    saveTokens(
      await tokenRequest({ grant_type: 'refresh_token', refresh_token: tokens.refreshToken }),
      tokens.refreshToken,
    )
    return loadTokens()?.accessToken ?? null
  } catch {
    sessionStorage.removeItem(TOKENS_KEY)
    return null
  }
}

/** ログイン済みか(トークンを保持しているか)。 */
export function isLoggedIn(): boolean {
  return loadTokens() !== null
}

/** ID トークンから表示用の利用者情報を取り出す。 */
export function currentUser(): CurrentUser | null {
  const tokens = loadTokens()
  if (!tokens) return null
  try {
    const payload = JSON.parse(
      atob(tokens.idToken.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')),
    ) as { email?: string; 'cognito:username'?: string }
    return { name: payload.email ?? payload['cognito:username'] ?? '' }
  } catch {
    return null
  }
}

/** トークンを破棄し、Hosted UI のログアウトを経てトップへ戻る。 */
export function logout(): void {
  sessionStorage.removeItem(TOKENS_KEY)
  const query = new URLSearchParams({ client_id: clientId, logout_uri: `${window.location.origin}/` })
  window.location.assign(`${domain}/logout?${query}`)
}
