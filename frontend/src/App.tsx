import { Link, Route, Routes } from 'react-router-dom'
import ProjectListPage from './pages/ProjectListPage'
import ProjectDetailPage from './pages/ProjectDetailPage'
import PlanEditPage from './pages/PlanEditPage'
import RevenuePlanEditPage from './pages/RevenuePlanEditPage'
import ActualsPage from './pages/ActualsPage'
import RevenueActualsPage from './pages/RevenueActualsPage'
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
        <span className="subtitle">予算策定・実績管理・差異分析</span>
      </header>
      <main className="container">
        <Routes>
          <Route path="/" element={<ProjectListPage />} />
          <Route path="/projects/:projectId" element={<ProjectDetailPage />} />
          <Route path="/projects/:projectId/plans/:planId" element={<PlanEditPage />} />
          <Route
            path="/projects/:projectId/revenue-plans/:planId"
            element={<RevenuePlanEditPage />}
          />
          <Route path="/projects/:projectId/actuals" element={<ActualsPage />} />
          <Route path="/projects/:projectId/revenue-actuals" element={<RevenueActualsPage />} />
          <Route path="/projects/:projectId/variance" element={<VariancePage />} />
          <Route path="/projects/:projectId/comparison" element={<ComparisonPage />} />
        </Routes>
      </main>
    </>
  )
}
