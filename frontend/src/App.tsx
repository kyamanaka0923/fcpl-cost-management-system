import { Link, Route, Routes } from 'react-router-dom'
import DepartmentListPage from './pages/DepartmentListPage'
import DepartmentDetailPage from './pages/DepartmentDetailPage'
import BudgetEditPage from './pages/BudgetEditPage'
import ActualsPage from './pages/ActualsPage'
import VariancePage from './pages/VariancePage'
import ComparisonPage from './pages/ComparisonPage'

export default function App() {
  return (
    <>
      <header className="app-header">
        <h1>
          <Link to="/" style={{ color: 'inherit' }}>
            総合原価管理システム
          </Link>
        </h1>
        <span className="subtitle">課別半期予算・実績管理・差異分析</span>
      </header>
      <main className="container">
        <Routes>
          <Route path="/" element={<DepartmentListPage />} />
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
