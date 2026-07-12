import { Link, Route, Routes } from 'react-router-dom'
import DivisionListPage from './pages/DivisionListPage'
import DivisionDetailPage from './pages/DivisionDetailPage'
import DepartmentDetailPage from './pages/DepartmentDetailPage'
import BudgetEditPage from './pages/BudgetEditPage'
import ActualsPage from './pages/ActualsPage'
import VariancePage from './pages/VariancePage'
import ComparisonPage from './pages/ComparisonPage'
import CostElementsPage from './pages/CostElementsPage'

export default function App() {
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
        </nav>
      </header>
      <main className="container">
        <Routes>
          <Route path="/" element={<DivisionListPage />} />
          <Route path="/cost-elements" element={<CostElementsPage />} />
          <Route path="/divisions/:divisionId" element={<DivisionDetailPage />} />
          <Route path="/departments/:departmentId" element={<DepartmentDetailPage />} />
          <Route path="/departments/:departmentId/budgets/:budgetId" element={<BudgetEditPage />} />
          <Route path="/departments/:departmentId/actuals" element={<ActualsPage />} />
          <Route path="/departments/:departmentId/variance" element={<VariancePage />} />
          <Route path="/departments/:departmentId/comparison" element={<ComparisonPage />} />
        </Routes>
      </main>
    </>
  )
}
