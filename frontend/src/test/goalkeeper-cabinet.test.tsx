import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { AuthProvider } from '../auth'
import { resetCsrf } from '../api'
import { GoalkeeperCabinetPage, KeeperTrainingEditor } from '../pages/GoalkeeperCabinet'
import { GoalkeeperStaffPanel } from '../pages/GoalkeeperStaffPanel'
import { keeperCalendarDays, newKeeperPlan, type KeeperWorkspace } from '../pages/goalkeeperModel'

const teams = [{ teamId: 3, schoolId: 2, school: 'Плазма СПОРТ', name: 'U17 — Первый состав', coach: 'Тренер ВР' }, { teamId: 4, schoolId: 3, school: 'Динамо', name: 'U15', coach: 'Тренер ВР' }]
const data: KeeperWorkspace = {
  players: [{ playerId: 1, firstName: 'Иван', lastName: 'Вратарь', dateOfBirth: '2010-01-01', preferredPosition: 'Вратарь', dominantFoot: 'Правая', sessions: 0, attended: 0 }],
  exercises: [{ id: 9, title: 'Ловля низкого мяча', category: 'Техника', description: 'Две серии по 5 повторений', equipment: 'Мяч', durationMinutes: 10, isActive: true }],
  trainings: [], matches: []
}
const json = (body: unknown, status = 200) => Promise.resolve(new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } }))
function mockRequests(appointments = teams) {
  return vi.spyOn(globalThis, 'fetch').mockImplementation((input, options) => {
    const path = String(input)
    if (path === '/api/me') return json({ id: 'keeper-user', email: 'keeper@example.test', roles: ['Coach'] })
    if (path === '/api/auth/csrf') return json({ token: 'test' })
    if (options?.method === 'POST' || options?.method === 'PUT') return json({ id: 1, version: 1, status: 'Planned' })
    if (path.endsWith('/goalkeeper-coaches')) return json([])
    if (path === '/api/coach/goalkeepers/teams') return json(appointments)
    if (path.endsWith('/workspace')) return json(data)
    return json({})
  })
}
function setup(path = '/goalkeeper?team=3&school=2') {
  return render(<MemoryRouter initialEntries={[path]}><AuthProvider><GoalkeeperCabinetPage /></AuthProvider></MemoryRouter>)
}
beforeEach(() => { resetCsrf(); sessionStorage.clear() })
afterEach(() => vi.restoreAllMocks())

describe('goalkeeper cabinet', () => {
  it('requires a specialist appointment instead of falling back to the general squad', async () => {
    const fetch = mockRequests([])
    setup()
    expect(await screen.findByText('Вы пока не назначены тренером вратарей')).toBeInTheDocument()
    expect(fetch.mock.calls.some(([path]) => String(path).endsWith('/workspace'))).toBe(false)
  })
  it('switches only between appointed schools and fetches the selected workspace', async () => {
    const fetch = mockRequests()
    setup()
    await screen.findByText('От упражнения до итога')
    fireEvent.change(screen.getByLabelText('Школа тренера вратарей'), { target: { value: '3' } })
    await waitFor(() => expect(fetch.mock.calls.some(([path]) => String(path) === '/api/coach/goalkeepers/teams/4/workspace')).toBe(true))
    expect(screen.getByLabelText('Команда тренера вратарей')).toHaveValue('4')
    expect(screen.queryByRole('option', { name: 'U17 — Первый состав' })).not.toBeInTheDocument()
  })
  it('does not request a foreign team supplied in the URL', async () => {
    const fetch = mockRequests()
    setup('/goalkeeper?team=999')
    expect(await screen.findByText(/Этот состав не назначен вам/)).toBeInTheDocument()
    expect(fetch.mock.calls.some(([path]) => String(path).endsWith('/workspace'))).toBe(false)
  })
  it('creates a personal goalkeeper exercise without using the admin library', async () => {
    const fetch = mockRequests()
    setup()
    await screen.findByText('От упражнения до итога')
    fireEvent.click(screen.getByRole('button', { name: /^Упражнения$/ }))
    fireEvent.click(screen.getByRole('button', { name: '+ Добавить упражнение' }))
    fireEvent.change(screen.getByLabelText('Название упражнения'), { target: { value: 'Выход один в один' } })
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить упражнение' }))
    await waitFor(() => expect(fetch.mock.calls.some(([path, options]) => String(path) === '/api/coach/goalkeepers/teams/3/exercises' && options?.method === 'POST')).toBe(true))
    expect(fetch.mock.calls.some(([path]) => String(path).startsWith('/api/admin'))).toBe(false)
  })
  it('builds a complete ordered session plan from a library snapshot', async () => {
    const fetch = mockRequests(), saved = vi.fn(async () => {})
    render(<KeeperTrainingEditor teamId={3} initial={newKeeperPlan('2026-10-22', data.players)} data={data} names={data.players} register={() => {}} saved={saved} copy={() => {}} />)
    fireEvent.change(screen.getByLabelText('Добавить упражнение в план'), { target: { value: '9' } })
    fireEvent.click(screen.getByRole('button', { name: '+ Свой этап' }))
    fireEvent.change(screen.getByLabelText('Название этапа 2'), { target: { value: 'Заминка' } })
    fireEvent.click(screen.getByRole('button', { name: 'Этап 2 вверх' }))
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить план' }))
    await waitFor(() => expect(saved).toHaveBeenCalled())
    const request = fetch.mock.calls.find(([path, options]) => String(path) === '/api/coach/goalkeepers/teams/3/trainings' && options?.method === 'POST')
    const body = JSON.parse(String(request?.[1]?.body))
    expect(body.plan.map((x: { title: string }) => x.title)).toEqual(['Заминка', 'Ловля низкого мяча'])
    expect(body.plan[1].instructions).toBe('Две серии по 5 повторений')
    expect(body.players).toEqual([{ playerId: 1, attendance: 'Unknown', rating: null, notes: '' }])
    expect(body.status).toBe('Planned')
    expect(body.scheduledAt).toContain('2026-10-22T')
  })
  it('keeps draft contents when the server reports a stale version', async () => {
    const fetch = mockRequests()
    fetch.mockImplementation((input, options) => String(input) === '/api/auth/csrf' ? json({ token: 'x' }) : options?.method === 'PUT' ? json({ message: 'Обновите страницу: версия изменена' }, 409) : json({}))
    const initial = { ...newKeeperPlan('2026-10-22', data.players), version: 2, plan: [{ exerciseId: null, title: 'Ловля', stage: 'Разминка', durationMinutes: 10, instructions: '' }] }
    render(<KeeperTrainingEditor id={3} teamId={3} initial={initial} data={data} names={data.players} register={() => {}} saved={vi.fn(async () => {})} copy={() => {}} />)
    fireEvent.change(screen.getByLabelText('Тема тренировки'), { target: { value: 'Мой несохранённый план' } })
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить план' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('версия изменена')
    expect(screen.getByLabelText('Тема тренировки')).toHaveValue('Мой несохранённый план')
  })
  it('requires attendance before completion and completed sessions are read-only', () => {
    mockRequests()
    const initial = { ...newKeeperPlan('2026-10-20', data.players), plan: [{ exerciseId: null, title: 'Ловля', stage: 'Разминка', durationMinutes: 10, instructions: '' }] }
    const view = render(<KeeperTrainingEditor teamId={3} initial={initial} data={data} names={data.players} register={() => {}} saved={vi.fn(async () => {})} copy={() => {}} />)
    fireEvent.click(screen.getByRole('button', { name: 'Завершить тренировку' }))
    expect(screen.getByRole('alert')).toHaveTextContent('посещаемость всех вратарей')
    view.unmount()
    const copy = vi.fn()
    render(<KeeperTrainingEditor id={3} teamId={3} initial={{ ...initial, status: 'Completed' }} data={data} names={data.players} register={() => {}} saved={vi.fn(async () => {})} copy={copy} />)
    expect(screen.getByLabelText('Тема тренировки')).toBeDisabled()
    expect(screen.queryByRole('button', { name: 'Сохранить план' })).not.toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Повторить на другую дату' }))
    expect(copy).toHaveBeenCalledOnce()
  })
  it('assigns specialists using a separate permission endpoint', async () => {
    const fetch = mockRequests()
    render(<GoalkeeperStaffPanel schoolId={2} teamId={3} coaches={[{ coachId: 7, displayName: 'Тренер ВР', isActive: true }]} />)
    await waitFor(() => expect(screen.getByLabelText('Назначить тренера вратарей')).toBeEnabled())
    fireEvent.change(screen.getByLabelText('Назначить тренера вратарей'), { target: { value: '7' } })
    fireEvent.click(screen.getByRole('button', { name: 'Назначить тренером вратарей' }))
    await waitFor(() => expect(fetch.mock.calls.some(([path, options]) => String(path) === '/api/school/2/teams/3/goalkeeper-coaches' && options?.method === 'POST' && String(options.body) === '{"coachId":7}')).toBe(true))
  })
  it('confirms completion inside the page before sending the final result', async () => {
    const fetch = mockRequests(), saved = vi.fn(async () => {})
    const initial = { ...newKeeperPlan('2026-10-20', data.players), players: [{ playerId: 1, attendance: 'Present', rating: 8, notes: 'Демонстрация' }], plan: [{ exerciseId: null, title: 'Ловля', stage: 'Разминка', durationMinutes: 10, instructions: '' }] }
    render(<KeeperTrainingEditor teamId={3} initial={initial} data={data} names={data.players} register={() => {}} saved={saved} copy={() => {}} />)
    fireEvent.click(screen.getByRole('button', { name: 'Завершить тренировку' }))
    expect(screen.getByRole('alertdialog')).toHaveTextContent('Завершить занятие?')
    expect(fetch.mock.calls.some(([, options]) => options?.method === 'POST')).toBe(false)
    fireEvent.click(screen.getByRole('button', { name: 'Подтвердить завершение' }))
    await waitFor(() => expect(saved).toHaveBeenCalled())
    const request = fetch.mock.calls.find(([, options]) => options?.method === 'POST')
    expect(JSON.parse(String(request?.[1]?.body)).status).toBe('Completed')
  })
})

describe('goalkeeper calendar dates', () => {
  it('starts on Monday, handles leap years and never skips the final month day', () => {
    const january = keeperCalendarDays('2027-01-15', 'month')
    expect(january[0]).toBe('2026-12-28')
    expect(january.at(-1)).toBe('2027-01-31')
    expect(keeperCalendarDays('2024-02-15', 'month')).toContain('2024-02-29')
    expect(keeperCalendarDays('2026-10-22', 'week')).toEqual(['2026-10-19', '2026-10-20', '2026-10-21', '2026-10-22', '2026-10-23', '2026-10-24', '2026-10-25'])
  })
})
