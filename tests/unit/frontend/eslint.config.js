// ESLint(flat config)。TypeScript の推奨ルールを検査する。
// 書式(インデント・引用符など)は Prettier に任せるため、衝突するルールは eslint-config-prettier で無効化する。
import js from '@eslint/js'
import tseslint from 'typescript-eslint'
import globals from 'globals'
import prettier from 'eslint-config-prettier'

export default tseslint.config(
  { ignores: ['node_modules', 'coverage'] },
  {
    files: ['**/*.{ts,tsx,js}'],
    extends: [js.configs.recommended, ...tseslint.configs.recommended],
    // テストは jsdom(ブラウザ相当)と Node.js の両方の API を使う
    languageOptions: { ecmaVersion: 2022, globals: { ...globals.browser, ...globals.node } },
  },
  prettier,
)
