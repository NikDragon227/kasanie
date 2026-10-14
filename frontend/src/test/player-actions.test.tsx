import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { PlayerActionMenu, similarPlayers, type ActionPlayer } from '../pages/PlayerActionMenu'
import { AdminSchoolOwnerDialog } from '../pages/AdminSchoolControls'
import { SchoolBoundary } from '../pages/SchoolScope'
import { resetCsrf } from '../api'

const player: ActionPlayer = { playerId: 1, firstName: 'Иван', lastName: 'Игрок', dateOfBirth: '2010-01-01', preferredPosition: 'Защитник', dominantFoot: 'Правая', experienceLevel: 'Любитель', inCurrentTeam: true }
const peer = { ...player, playerId: 2, firstName: 'Пётр', lastName: 'Защитник' }
const other = { ...player, playerId: 3, firstName: 'Игрок', lastName: 'Резерва', inCurrentTeam: false }
function mockApi() {
  return vi.spyOn(globalThis, 'fetch').mockImplementation(input => Promise.resolve(new Response(JSON.stringify(String(input).endsWith('/actions') ? { teams: [{ id: 2, name: 'Резерв' }], players: [player, peer, other] } : String(input) === '/api/auth/csrf' ? { token: 'test' } : {}), { headers: { 'Content-Type': 'application/json' } })))
}
function menu() {
  return render(<MemoryRouter><PlayerActionMenu player={player} teamId={1} point={{ x: 10, y: 10 }} close={vi.fn()} run={async action => { await action(); return true }} /></MemoryRouter>)
}
afterEach(() => { vi.restoreAllMocks(); resetCsrf() })

describe('player actions', () => {
  it('shows direct note action and persists training rest', async () => {
    const fetch = mockApi()
    menu()
    expect(screen.getByRole('menuitem', { name: 'Создать заметку' })).toBeInTheDocument()
    expect(screen.queryByText('Прочее')).not.toBeInTheDocument()
    fireEvent.click(screen.getByRole('menuitem', { name: 'Тренировки' }))
    fireEvent.click(screen.getByRole('menuitem', { name: '3 дн.' }))
    await waitFor(() => expect(fetch.mock.calls.some(([, init]) => init?.method === 'PUT')).toBe(true))
    const request = fetch.mock.calls.find(([, init]) => init?.method === 'PUT')
    expect(request?.[0]).toBe('/api/coach/teams/1/players/1/training')
    expect(JSON.parse(String(request?.[1]?.body)).restDays).toBe(3)
  })
  it('compares only scoped candidates and displays missing assessments honestly', async () => {
    mockApi()
    menu()
    await screen.findByRole('menuitem', { name: 'Резерв' })
    fireEvent.click(screen.getByRole('menuitem', { name: 'Отчёт' }))
    fireEvent.click(screen.getByRole('menuitem', { name: 'Сравнить с игроком' }))
    expect(screen.getAllByText('Нет оценки')).toHaveLength(12)
    expect(screen.queryByRole('option', { name: /Игрок Резерва/ })).not.toBeInTheDocument()
    fireEvent.change(screen.getByLabelText('Область поиска'), { target: { value: 'accessible' } })
    expect(screen.getByRole('option', { name: /Игрок Резерва/ })).toBeInTheDocument()
    expect(screen.queryByRole('option', { name: /Иван Игрок/ })).not.toBeInTheDocument()
  })
  it('stores a coach note through the existing profile endpoint', async () => {
    const fetch = mockApi()
    menu()
    fireEvent.click(screen.getByRole('menuitem', { name: 'Создать заметку' }))
    fireEvent.change(screen.getByLabelText('Заметка'), { target: { value: 'Работать над первым пасом' } })
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить заметку' }))
    await waitFor(() => expect(fetch.mock.calls.some(([, init]) => init?.method === 'POST')).toBe(true))
    const call = fetch.mock.calls.find(([, init]) => init?.method === 'POST')
    expect(call?.[0]).toBe('/api/coach/players/1/notes')
    expect(JSON.parse(String(call?.[1]?.body))).toEqual({ text: 'Работать над первым пасом' })
  })
  it('ranks same-position peers and never includes the selected player', () => {
    const ranked = similarPlayers(player, [{ ...peer, playerId: 4, preferredPosition: 'Вратарь' }, player, peer])
    expect(ranked.map(p => p.playerId)).toEqual([2, 4])
  })
})

describe('school administration', () => {
  it('changes the owner with optimistic expected owner and confirmation', async () => {
    const fetch = mockApi()
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    const reload = vi.fn().mockResolvedValue(undefined)
    render(<MemoryRouter><AdminSchoolOwnerDialog school={{ id: 90, name: 'Школа', ownerEmail: 'old@example.test' }} close={vi.fn()} reload={reload} /></MemoryRouter>)
    fireEvent.change(screen.getByLabelText('Email нового владельца'), { target: { value: 'new@example.test' } })
    fireEvent.click(screen.getByRole('button', { name: 'Назначить владельца' }))
    expect(await screen.findByRole('status')).toHaveTextContent('Владелец школы изменён.')
    const call = fetch.mock.calls.find(([, init]) => init?.method === 'PUT')
    expect(call?.[0]).toBe('/api/admin/schools/90/owner')
    expect(JSON.parse(String(call?.[1]?.body))).toEqual({ ownerEmail: 'new@example.test', expectedOwnerEmail: 'old@example.test' })
    expect(reload).toHaveBeenCalled()
  })
  it('preserves selected school in navigation instead of silently opening the first', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(JSON.stringify([{ schoolId: 1, name: 'Первая', role: 'Admin' }, { schoolId: 2, name: 'Вторая', role: 'Admin' }])))
    render(<MemoryRouter initialEntries={['/school?school=2']}><SchoolBoundary>{id => <p>Школа {id}</p>}</SchoolBoundary></MemoryRouter>)
    expect(await screen.findByText('Школа 2')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Тренеры' })).toHaveAttribute('href', '/school/coaches?school=2')
  })
})
