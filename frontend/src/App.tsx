import { useEffect } from 'react'
import { Link, Route, Routes, useLocation } from 'react-router-dom'
import { authEnabled, callbackPath, currentUser, isLoggedIn, login, logout } from './auth'
import AuthCallbackPage from './pages/AuthCallbackPage'
import DivisionListPage from './pages/DivisionListPage'
import DivisionDetailPage from './pages/DivisionDetailPage'
import DepartmentDetailPage from './pages/DepartmentDetailPage'
import BudgetEditPage from './pages/BudgetEditPage'
import ActualsPage from './pages/ActualsPage'
import VariancePage from './pages/VariancePage'
import ComparisonPage from './pages/ComparisonPage'
import CostElementsPage from './pages/CostElementsPage'

/** 未ログインのときに表示し、Cognito のログイン画面へ移動する。 */
function LoginRedirect() {
  useEffect(() => {
    void login()
  }, [])
  return (
    <div className="card">
      <p>ログイン画面へ移動しています…</p>
    </div>
  )
}

export default function App() {
  const location = useLocation()
  // 認証が有効なら、コールバック以外の画面はログインを必須にする
  const needsLogin = authEnabled && location.pathname !== callbackPath && !isLoggedIn()
  const user = authEnabled ? currentUser() : null

  return (
    <>
      <header className="app-header">
        <h1>
          <Link to="/" style={{ color: 'inherit' }}>
            総合原価管理システム
          </Link>
        </h1>
        <span className="subtitle">部・課別半期予算・実績管理・差異分析</span>
        <nav className="app-nav">
          <Link to="/">部一覧</Link>
          <Link to="/cost-elements">費目マスタ</Link>
          {user && (
            <>
              <span className="app-user">{user.name}</span>
              <button type="button" className="btn-link" onClick={logout}>
                ログアウト
              </button>
            </>
          )}
        </nav>
      </header>
      <main className="container">
        {needsLogin ? (
          <LoginRedirect />
        ) : (
          <Routes>
            <Route path={callbackPath} element={<AuthCallbackPage />} />
            <Route path="/" element={<DivisionListPage />} />
            <Route path="/cost-elements" element={<CostElementsPage />} />
            <Route path="/divisions/:divisionId" element={<DivisionDetailPage />} />
            <Route path="/departments/:departmentId" element={<DepartmentDetailPage />} />
            <Route
              path="/departments/:departmentId/budgets/:budgetId"
              element={<BudgetEditPage />}
            />
            <Route path="/departments/:departmentId/actuals" element={<ActualsPage />} />
            <Route path="/departments/:departmentId/variance" element={<VariancePage />} />
            <Route path="/departments/:departmentId/comparison" element={<ComparisonPage />} />
          </Routes>
        )}
      </main>
    </>
  )
}
