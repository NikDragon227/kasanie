import type { ReactNode } from 'react'
import { Link, useLocation, useSearchParams } from 'react-router-dom'
import { EmptyState, ErrorState, PageLoader } from '../components'
import { useApiData } from '../hooks'
import './schoolAdministration.css'

type Membership = { schoolId: number; name: string; role: string }
export function SchoolBoundary({ children }: { children: (schoolId: number) => ReactNode }) {
  const state = useApiData<Membership[]>('/api/school/memberships')
  const [params, setParams] = useSearchParams(), location = useLocation()
  if (state.loading) return <PageLoader />
  if (state.error) return <ErrorState message={state.error} retry={state.reload} />
  if (!state.data?.length) return <EmptyState title="Нет доступной школы">Обратитесь к администратору платформы.</EmptyState>
  const requested = Number(params.get('school'))
  const school = requested ? state.data.find(item => item.schoolId === requested) : state.data[0]
  if (!school) return <ErrorState message="Эта школа недоступна. Выберите школу из списка." />
  return <><section className="school-scope-bar"><label>{school.role === 'Admin' ? 'Управление школой · администратор платформы' : 'Школа'}<select aria-label="Управляемая школа" value={school.schoolId} onChange={e => { const next = new URLSearchParams(params); next.set('school', e.target.value); next.delete('team'); setParams(next) }}>{state.data.map(item => <option key={item.schoolId} value={item.schoolId}>{item.name}</option>)}</select></label>{school.role === 'Admin' && <Link className="button ghost" to="/admin/schools">← Все школы</Link>}<nav aria-label="Управление выбранной школой">{[['/school', 'Обзор'], ['/school/teams', 'Составы'], ['/school/coaches', 'Тренеры'], ['/school/players', 'Игроки'], ['/school/settings', 'Настройки']].map(([path, label]) => <Link aria-current={location.pathname === path ? 'page' : undefined} className={location.pathname === path ? 'active' : ''} key={path} to={`${path}?school=${school.schoolId}`}>{label}</Link>)}</nav></section><div key={school.schoolId}>{children(school.schoolId)}</div></>
}
