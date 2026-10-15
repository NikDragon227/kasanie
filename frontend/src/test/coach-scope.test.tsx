import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { Link, MemoryRouter, useLocation } from 'react-router-dom'
import { beforeEach, afterEach, describe, expect, it, vi } from 'vitest'
import { CoachScopeBar, CoachScopeProvider, coachScopedLink, useCoachScope } from '../CoachScope'
import { CoachTeamsPage } from '../pages/CoachTeamPage'
import { CoachDashboard } from '../pages/RolePages'
import { useApiData } from '../hooks'
import { useEffect } from 'react'

const teams = [
  { teamId: 2, schoolId: 3, name: 'U15 — Первый состав', school: 'Динамо', players: 0 },
  { teamId: 3, schoolId: 2, name: 'U17 — Первый состав', school: 'Плазма СПОРТ', players: 20 },
  { teamId: 4, schoolId: 2, name: 'U17 — Второй состав', school: 'Плазма СПОРТ', players: 1 }
]
const schools = [{ schoolId: 3, name: 'Динамо' }, { schoolId: 2, name: 'Плазма СПОРТ' }, { schoolId: 5, name: 'Без состава' }]
const json = (value: unknown) => Promise.resolve(new Response(JSON.stringify(value), { headers: { 'Content-Type': 'application/json' } }))
function Harness() {
  const scope = useCoachScope(), location = useLocation()
  return <><CoachScopeBar /><p role="status">{scope.error || `${scope.schoolId}:${scope.teamId}`}</p><Link to={scope.link('/coach/teams?tab=squad&view=planning')}>Планировщик</Link><Link to="/coach/trainings">Прямая ссылка без параметров</Link><div>{location.pathname}{location.search}</div></>
}
function setup(path = '/coach/teams', userId = 'scope-test', children = <Harness />) {
  return render(<MemoryRouter initialEntries={[path]}><CoachScopeProvider userId={userId}>{children}</CoachScopeProvider></MemoryRouter>)
}
beforeEach(() => { sessionStorage.clear() })
afterEach(() => vi.restoreAllMocks())
function mockScope() {
  return vi.spyOn(globalThis, 'fetch').mockImplementation(input => String(input) === '/api/coach/teams' ? json(teams) : String(input) === '/api/school/memberships' ? json(schools) : json({ unfinished: [], risks: [], upcomingEvents: [] }))
}
describe('coach school context', () => {
  it('separates schools, filters squads and preserves selection through navigation and refresh', async () => {
    mockScope()
    const view = setup()
    await screen.findByText('/coach/teams?school=3&team=2')
    expect(screen.getByLabelText('Команда и состав')).toHaveValue('2')
    fireEvent.change(screen.getByLabelText('Школа тренера'), { target: { value: '2' } })
    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('2:3'))
    expect(screen.queryByRole('option', { name: 'U15 — Первый состав' })).not.toBeInTheDocument()
    fireEvent.change(screen.getByLabelText('Команда и состав'), { target: { value: '4' } })
    fireEvent.click(screen.getByRole('link', { name: 'Планировщик' }))
    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('2:4'))
    expect(screen.getByRole('link', { name: 'Планировщик' })).toHaveAttribute('href', '/coach/teams?tab=squad&view=planning&school=2&team=4')
    fireEvent.click(screen.getByRole('link', { name: 'Прямая ссылка без параметров' }))
    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('2:4'))
    view.unmount()
    setup('/coach')
    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('2:4'))
  })
  it('does not reuse a different user preference or accept a foreign team', async () => {
    mockScope()
    sessionStorage.setItem('kasanie:coach-scope:another-user', JSON.stringify({ schoolId: 2, teamId: 3 }))
    const view = setup()
    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('3:2'))
    view.unmount()
    setup('/coach/teams?school=2&team=999')
    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('Этот состав не назначен тренеру.'))
  })
  it('shows schools without a coaching assignment honestly instead of substituting another school', async () => {
    mockScope()
    setup()
    await screen.findByText('/coach/teams?school=3&team=2')
    fireEvent.change(screen.getByLabelText('Школа тренера'), { target: { value: '5' } })
    expect(screen.getByLabelText('Команда и состав')).toBeDisabled()
    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('5:0'))
    expect(screen.getByRole('option', { name: 'Состав не назначен' })).toBeInTheDocument()
  })
  it('filters the dashboard by school after switching', async () => {
    const fetch = mockScope()
    setup('/coach', 'dashboard', <CoachDashboard />)
    await screen.findByRole('heading', { level: 1, name: 'Кабинет тренера' })
    fireEvent.change(screen.getByLabelText('Школа тренера'), { target: { value: '2' } })
    await waitFor(() => expect(fetch.mock.calls.some(([input]) => String(input) === '/api/coach/workday?schoolId=2')).toBe(true))
    expect(screen.getByLabelText('Школа тренера')).toHaveValue('2')
  })
  it('does not request an unassigned command center', async () => {
    const fetch = mockScope()
    setup('/coach/teams?school=5', 'unassigned', <CoachTeamsPage />)
    expect(await screen.findByText('В этой школе состав не назначен')).toBeInTheDocument()
    expect(fetch.mock.calls.some(([input]) => String(input).includes('command-center'))).toBe(false)
  })
  it('preserves route-specific filters in navigation', () => {
    expect(coachScopedLink('/coach/teams?tab=squad&view=planning', 2, 3)).toBe('/coach/teams?tab=squad&view=planning&school=2&team=3')
    expect(coachScopedLink('/account/security', 2, 3)).toBe('/account/security')
  })
  it('does not switch school when an unsaved-plan guard rejects the change', async () => {
    mockScope()
    const guard = vi.fn(() => false)
    function Guarded() {
      const { registerGuard } = useCoachScope()
      useEffect(() => registerGuard(guard), [registerGuard])
      return <Harness />
    }
    setup('/coach/teams', 'guarded', <Guarded />)
    await screen.findByText('/coach/teams?school=3&team=2')
    fireEvent.change(screen.getByLabelText('Школа тренера'), { target: { value: '2' } })
    expect(guard).toHaveBeenCalledOnce()
    expect(screen.getByLabelText('Школа тренера')).toHaveValue('3')
  })
})

describe('scope request races', () => {
  it('ignores a slow response from the previous school', async () => {
    let finishOld!: (response: Response) => void
    vi.spyOn(globalThis, 'fetch').mockImplementation(input => String(input) === '/school/old' ? new Promise<Response>(resolve => { finishOld = resolve }) : json('new-school'))
    function Result({ path }: { path: string }) { const state = useApiData<string>(path); return <p>{state.data}</p> }
    const view = render(<Result path="/school/old" />)
    view.rerender(<Result path="/school/new" />)
    expect(await screen.findByText('new-school')).toBeInTheDocument()
    await act(async () => finishOld(new Response(JSON.stringify('old-school'))))
    expect(screen.getByText('new-school')).toBeInTheDocument()
    expect(screen.queryByText('old-school')).not.toBeInTheDocument()
  })
})
