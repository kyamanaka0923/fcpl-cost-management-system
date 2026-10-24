import { expect, type APIRequestContext, type Page } from '@playwright/test'
import { createBdd, test as base } from 'playwright-bdd'

/** 区分の日本語表記 → API の表記。 */
export const 区分 = (日本語: string): string => {
  const map: Record<string, string> = {
    売上高: 'Revenue',
    加工費: 'Processing',
    外注費: 'Outsourcing',
    期間費用: 'PeriodCost',
  }
  const value = map[日本語]
  if (!value) throw new Error(`未知の区分です: ${日本語}`)
  return value
}

/** 画面の初期表示と同じ今期(4〜9月 = 上期、10〜3月 = 前年度の下期)。src/api.ts の currentFiscalHalf と同じ規則。 */
export const 今期 = (): string => {
  const now = new Date()
  const month = now.getMonth() + 1
  const year = now.getFullYear()
  if (month >= 4 && month <= 9) return `${year}-H1`
  return month >= 10 ? `${year}-H2` : `${year - 1}-H2`
}

type 組織 = { id: string; code: string; name: string }

/**
 * シナリオ1本ぶんの業務データ。ブラウザE2Eは同じDBを全シナリオで使うため、
 * フィーチャーに書いた部・課の名前や費目コードにシナリオ固有の接尾辞を付けて衝突を避ける。
 * 案件は課の中で一意であればよいので、フィーチャーに書いたコード・名称のまま使う。
 * 「前提」の準備は画面を通さず API で行い、操作と検証だけを画面で行う。
 */
export class 業務データ {
  readonly suffix = `${Date.now() % 1_000_000}${Math.floor(Math.random() * 1000)}`
  private readonly 部 = new Map<string, 組織>()
  private readonly 課 = new Map<string, 組織>()
  private readonly 案件 = new Map<string, string>() // `${課}/${案件名}` → 案件Id
  private 連番 = 0

  constructor(private readonly request: APIRequestContext) {}

  /** フィーチャー上の部・課の名前を、実際に登録する名前にする。 */
  実名 = (name: string): string => `${name}-${this.suffix}`

  /** 費目コードはシステム共通なので接尾辞を付ける。 */
  費目コード = (code: string): string => `${code}-${this.suffix}`

  /** 画面から登録する部・課のコードを払い出して覚えておく。 */
  部を予約(name: string): 組織 {
    const org = { id: '', code: `D${++this.連番}-${this.suffix}`, name: this.実名(name) }
    this.部.set(name, org)
    return org
  }

  課を予約(name: string): 組織 {
    const org = { id: '', code: `S${++this.連番}-${this.suffix}`, name: this.実名(name) }
    this.課.set(name, org)
    return org
  }

  部を取得(name: string): 組織 {
    const org = this.部.get(name)
    if (!org) throw new Error(`部 "${name}" が未登録です。`)
    return org
  }

  課を取得(name: string): 組織 {
    const org = this.課.get(name)
    if (!org) throw new Error(`課 "${name}" が未登録です。`)
    return org
  }

  // ---- API による準備 ----

  private async post(path: string, data: unknown): Promise<{ id: string }> {
    const res = await this.request.post(path, { data })
    expect(res.ok(), `${path}: ${res.status()} ${await res.text()}`).toBeTruthy()
    return res.json()
  }

  private async put(path: string, data: unknown): Promise<void> {
    const res = await this.request.put(path, { data })
    expect(res.ok(), `${path}: ${res.status()} ${await res.text()}`).toBeTruthy()
  }

  async 部と課を用意(division: string, department: string): Promise<void> {
    if (!this.部.has(division)) {
      const org = this.部を予約(division)
      org.id = (await this.post('/api/divisions', { code: org.code, name: org.name })).id
    }
    const dept = this.課を予約(department)
    dept.id = (
      await this.post(`/api/divisions/${this.部を取得(division).id}/departments`, {
        code: dept.code,
        name: dept.name,
      })
    ).id
  }

  async 案件を用意(department: string, code: string, name: string): Promise<void> {
    const res = await this.post(`/api/departments/${this.課を取得(department).id}/projects`, {
      code,
      name,
    })
    this.案件.set(`${department}/${name}`, res.id)
  }

  案件Id(department: string, name: string): string {
    const id = this.案件.get(`${department}/${name}`)
    if (!id) throw new Error(`課 "${department}" の案件 "${name}" が未登録です。`)
    return id
  }

  async 承認済み予算を用意(department: string, lines: Record<string, string>[]): Promise<void> {
    const deptId = this.課を取得(department).id
    const budget = await this.post(`/api/departments/${deptId}/budgets`, {
      fiscalHalf: 今期(),
      label: '当初予算',
    })
    for (const line of lines) {
      await this.put(`/api/budgets/${budget.id}/lines`, {
        category: 区分(line['区分']),
        projectId: line['案件'] ? this.案件Id(department, line['案件']) : null,
        elementCode: line['費目'] || null,
        amount: Number(line['金額']),
      })
    }
    await this.post(`/api/budgets/${budget.id}/approve`, {})
  }

  async 実績を用意(department: string, lines: Record<string, string>[]): Promise<void> {
    const deptId = this.課を取得(department).id
    for (const line of lines) {
      await this.post(`/api/departments/${deptId}/actuals`, {
        fiscalHalf: 今期(),
        category: 区分(line['区分']),
        projectId: line['案件'] ? this.案件Id(department, line['案件']) : null,
        elementCode: line['費目'] || null,
        amount: Number(line['金額']),
      })
    }
  }
}

export const test = base.extend<{ 業務: 業務データ }>({
  // Playwright のフィクスチャ引数は慣例で use と書くが、React Hooks の lint 規則に誤検知されるため provide と呼ぶ
  業務: async ({ request }, provide) => {
    await provide(new 業務データ(request))
  },
})

export const { Given, When, Then } = createBdd(test)

/** 複数のステップ文で共有するハンドラの引数(フィクスチャ)。 */
export type 画面 = { page: Page; 業務: 業務データ }
