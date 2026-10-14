import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { TrainingBoard } from '../pages/TrainingWorkspace'
import { dateKey, defaultPlayerSettings, monday, monthWeeks, weekDates, type TrainingResponse, type TrainingTeamData } from '../pages/trainingModel'
import { resetCsrf } from '../api'

const date = '2026-02-04'
const data: TrainingTeamData = {
  team: { id: 1, name: 'U17 — Первый состав', school: 'Спартак', trainingCycleStage: 'Базовый этап', tacticFormation: '4-4-2' },
  players: [
    { playerId: 1, firstName: 'Иван', lastName: 'Игрок', dateOfBirth: '2010-01-01', preferredPosition: 'Защитник', dominantFoot: 'Правая', experienceLevel: 'Начальный' },
    { playerId: 2, firstName: 'Пётр', lastName: 'Вратарь', dateOfBirth: '2009-02-01', preferredPosition: 'Вратарь', dominantFoot: 'Левая', experienceLevel: 'Средний' }
  ], injuries: [], matches: []
}
function response(): TrainingResponse {
  return { version: 0, coaches: [{ id: 3, name: 'Илья Морозов', isHeadCoach: true }], lockedSlots: [], state: { responsibleCoachId: null, tactics: [], calendar: [], players: [], mentoring: [], coaches: [], satisfaction: null, matchPreparation: null, lastMatchComment: null } }
}
function board(view = 'overview', value = response()) {
  return render(<MemoryRouter initialEntries={[`/coach/trainings?trainingView=${view}`]}><TrainingBoard data={data} response={value} calendarDate={date} onCalendarDate={vi.fn()} /></MemoryRouter>)
}
afterEach(() => { vi.restoreAllMocks(); resetCsrf() })

describe('training calendar dates', () => {
  it('starts every week on Monday and includes Sunday, including year boundaries', () => {
    expect(dateKey(monday(new Date('2027-01-01T12:00:00')))).toBe('2026-12-28')
    expect(weekDates(new Date('2027-01-01T12:00:00')).map(dateKey)).toEqual(['2026-12-28', '2026-12-29', '2026-12-30', '2026-12-31', '2027-01-01', '2027-01-02', '2027-01-03'])
    for (let month = 0; month < 12; month++) {
      const dates = monthWeeks(new Date(2026, month, 1, 12)).flat()
      expect(dates[0].getDay()).toBe(1)
      expect(dates.at(-1)?.getDay()).toBe(0)
      expect(dates.filter(d => d.getMonth() === month)).toHaveLength(new Date(2026, month + 1, 0).getDate())
    }
  })
  it('uses the player position for the initial positional group', () => {
    expect(defaultPlayerSettings(data.players[0]).group).toBe('Defence')
    expect(defaultPlayerSettings(data.players[1]).group).toBe('Goalkeepers')
  })
})

describe('training workspace', () => {
  it('lists only players with active rest status, excluding normal and expired rest', () => {
    const value = response()
    value.state.players = [{ ...defaultPlayerSettings(data.players[0]), intensity: 'Rest' }, { ...defaultPlayerSettings(data.players[1]), restUntil: '2000-01-01' }]
    board('rest', value)
    const table = within(screen.getByRole('table'))
    expect(table.getByText('Иван Игрок')).toBeInTheDocument()
    expect(table.queryByText('Пётр Вратарь')).not.toBeInTheDocument()
  })
  it('opens seven training sections and keeps medical unknowns explicit', () => {
    board()
    for (const title of ['Обзор', 'Календарь', 'Группы', 'Наставничество', 'Индивидуальные', 'Отдых', 'Тренеры']) expect(screen.getByRole('button', { name: title })).toBeInTheDocument()
    expect(screen.getByText(/Это не подтверждение готовности/)).toBeInTheDocument()
    expect(screen.getByText('Неделя ещё не заполнена')).toBeInTheDocument()
  })
  it('locks a match and links the journal without opening an editable slot', () => {
    const value = response()
    value.lockedSlots = [{ date, slot: 1, kind: 'Match', title: 'Зенит', id: 7, status: 'Запланирован', minutes: 90 }, { date, slot: 0, kind: 'Training', title: 'Работа с мячом', id: 8, status: 'Completed', minutes: 60 }]
    board('calendar', value)
    expect(screen.getByRole('button', { name: /матч Зенит, слот заблокирован/ })).toBeDisabled()
    expect(screen.getByRole('link', { name: /Работа с мячом, открыть журнал/ })).toHaveAttribute('href', '/coach/trainings/8')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })
  it('plans a session and persists it with the concurrency version', async () => {
    const fetch = vi.spyOn(globalThis, 'fetch').mockImplementation((input, init) => {
      if (String(input) === '/api/auth/csrf') return Promise.resolve(new Response(JSON.stringify({ token: 'test-token' })))
      expect(init?.method).toBe('PUT')
      return Promise.resolve(new Response(JSON.stringify({ version: 1 }), { headers: { 'Content-Type': 'application/json' } }))
    })
    board('calendar')
    fireEvent.click(screen.getByRole('button', { name: 'Тренировка 1, 4 февр.: Отдых' }))
    const dialog = within(screen.getByRole('dialog'))
    fireEvent.change(dialog.getByLabelText('Направление'), { target: { value: 'SetPieces' } })
    fireEvent.change(dialog.getByLabelText('Сессия'), { target: { value: 'Угловые' } })
    fireEvent.click(dialog.getByRole('button', { name: 'Добавить в план' }))
    expect(screen.getByRole('button', { name: 'Тренировка 1, 4 февр.: Угловые' })).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить план' }))
    await waitFor(() => expect(screen.getByText('План тренировок сохранён.')).toBeInTheDocument())
    const call = fetch.mock.calls.find(([, init]) => init?.method === 'PUT')
    const payload = JSON.parse(String(call?.[1]?.body))
    expect(payload.version).toBe(0)
    expect(payload.state.calendar[0]).toMatchObject({ date, slot: 0, category: 'SetPieces', session: 'Угловые', minutes: 60 })
  })
  it('edits individual focus, moves positional groups and assigns coaching tasks', () => {
    board('individual')
    fireEvent.change(screen.getByLabelText('Дополнительное направление'), { target: { value: 'Навесы' } })
    fireEvent.change(screen.getByLabelText('Тренировочная группа'), { target: { value: 'Attack' } })
    fireEvent.click(screen.getByRole('button', { name: 'Группы' }))
    const attack = screen.getByRole('heading', { name: 'Нападение' }).closest('article')!
    expect(within(attack).getByText('Иван Игрок')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Тренеры' }))
    fireEvent.click(screen.getByRole('button', { name: 'Изменить тренерские задачи' }))
    fireEvent.click(screen.getByRole('checkbox', { name: 'Илья Морозов: Защита · тактика' }))
    fireEvent.click(screen.getByRole('button', { name: 'Готово' }))
    expect(screen.getByText('Илья Морозов')).toBeInTheDocument()
    expect(screen.getByText('Лёгкая нагрузка')).toBeInTheDocument()
  })
  it('does not allow the same player in two mentoring groups', () => {
    board('mentoring')
    const groups = screen.getAllByLabelText('Добавить игрока')
    fireEvent.change(groups[0], { target: { value: '1' } })
    expect(within(groups[1]).queryByRole('option', { name: /Иван Игрок/ })).not.toBeInTheDocument()
    expect(screen.getAllByLabelText('Наставник')[0]).toHaveTextContent('Иван Игрок')
  })
  it('shows a failed save without marking the draft as saved', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(input => Promise.resolve(new Response(JSON.stringify(String(input) === '/api/auth/csrf' ? { token: 'test-token' } : { message: 'План изменён другим тренером.' }), { status: String(input) === '/api/auth/csrf' ? 200 : 409 })))
    board()
    fireEvent.change(screen.getByLabelText('Ответственный за тренировки'), { target: { value: '3' } })
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить план' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('План изменён другим тренером.')
    expect(screen.getByText('Есть несохранённые изменения')).toBeInTheDocument()
  })
})
