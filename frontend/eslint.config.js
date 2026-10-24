// ESLint(flat config)。TypeScript の推奨ルールと React Hooks の規則を検査する。
// 書式(インデント・引用符など)は Prettier に任せるため、衝突するルールは eslint-config-prettier で無効化する。
import js from '@eslint/js'
import tseslint from 'typescript-eslint'
import reactHooks from 'eslint-plugin-react-hooks'
import globals from 'globals'
import prettier from 'eslint-config-prettier'

export default tseslint.config(
  { ignores: ['dist', 'node_modules', 'playwright-report', 'test-results', '.features-gen'] },
  {
    files: ['**/*.{ts,tsx}'],
    extends: [js.configs.recommended, ...tseslint.configs.recommended],
    languageOptions: {
      ecmaVersion: 2022,
      globals: { ...globals.browser },
    },
    plugins: { 'react-hooks': reactHooks },
    rules: {
      'react-hooks/rules-of-hooks': 'error',
      'react-hooks/exhaustive-deps': 'warn',
    },
  },
  {
    // E2E テストと設定ファイルは Node.js で動く
    files: ['e2e/**/*.ts', '*.config.{ts,js}'],
    languageOptions: { globals: { ...globals.node } },
  },
  prettier,
)
