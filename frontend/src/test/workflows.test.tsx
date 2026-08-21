import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AuthProvider } from '../auth'
import { RadarChart, type Skills } from '../components'
import { CityInput } from '../CityInput'
import { LoginForm } from '../pages/PublicPages'
import { AssessmentForm, PlayerDashboard, WorkoutPage } from '../pages/PlayerPages'

const json = (value: unknown, status = 200) => Promise.resolve(new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } }))

beforeEach(() => { vi.restoreAllMocks() })

describe('critical workflows', () => {
  it('searches a city and submits only its name', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation((input) => {
      if (String(input).startsWith('/api/reference/cities?q=%D0%9A%D0%B0%D0%B7')) return json([{ city: 'Казань', region: 'Республика Татарстан' }])
      return json({})
    })
    let submitted: FormData | undefined
    render(<form onSubmit={event => { event.preventDefault(); submitted = new FormData(event.currentTarget) }}><label>Город<CityInput required /></label><button>Сохранить</button></form>)
    await userEvent.type(screen.getByLabelText('Город'), 'Каз')
    expect(await screen.findByText('Республика Татарстан')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: /казань/i }))
    await userEvent.click(screen.getByRole('button', { name: 'Сохранить' }))
    expect(submitted?.get('city')).toBe('Казань')
  })

  it('positions every radar label around the chart', () => {
    const values: Skills = { speed: 70, endurance: 65, ballControl: 75, passing: 60, shooting: 55, agility: 80 }
    const { container } = render(<RadarChart values={values} />)
    const labels = [...container.querySelectorAll('svg.radar text')]
    const coordinates = labels.map(label => `${label.getAttribute('x')},${label.getAttribute('y')}`)

    expect(labels).toHaveLength(6)
    expect(new Set(coordinates)).toHaveLength(6)
    expect(labels.every(label => Number(label.getAttribute('x')) > 20 && Number(label.getAttribute('y')) > 5)).toBe(true)
  })

  it('validates login before sending request', async () => {
    const fetch = vi.spyOn(globalThis, 'fetch').mockImplementation(() => json({}, 401))
    render(<MemoryRouter><AuthProvider><LoginForm /></AuthProvider></MemoryRouter>)
    await userEvent.type(screen.getByLabelText('Email'), 'incorrect')
    await userEvent.click(screen.getByRole('button', { name: 'Войти' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('корректный email')
    expect(fetch).toHaveBeenCalledTimes(1) // only AuthProvider /api/me
  })

  it('rejects out-of-range assessment result', async () => {
    const onComplete = vi.fn()
    render(<AssessmentForm data={{ demoNotice: 'DEMO', definitions: [{ id: 1, name: 'Спринт', description: 'Тест', instructions: 'Бежать', unit: 'сек', skillCategory: 'Speed', minimumReasonableValue: 3, maximumReasonableValue: 10 }] }} onComplete={onComplete} />)
    fireEvent.change(screen.getByLabelText('Результат: Спринт'), { target: { value: '20' } })
    await userEvent.click(screen.getByRole('button', { name: 'Завершить' }))
    expect(screen.getByRole('alert')).toHaveTextContent('от 3 до 10')
    expect(onComplete).not.toHaveBeenCalled()
  })

  it('shows a loading state and then dashboard data', async () => {
    let resolve!: (value: Response) => void
    vi.spyOn(globalThis, 'fetch').mockImplementation(() => new Promise<Response>(r => { resolve = r }))
    render(<MemoryRouter><PlayerDashboard /></MemoryRouter>)
    expect(screen.getByRole('status')).toHaveTextContent('Загрузка')
    resolve(new Response(JSON.stringify({ profile: { firstName: 'Артём' }, level: 72, weakestSkills: [{ name: 'Контроль', score: 48 }], weeklyCompletion: 50, achievements: [] }), { status: 200, headers: { 'Content-Type': 'application/json' } }))
    expect(await screen.findByText('Привет, Артём!')).toBeInTheDocument()
    expect(screen.getByText('72')).toBeInTheDocument()
  })

  it('persists exercise marks and completes a workout', async () => {
    const requests: string[] = []
    vi.spyOn(globalThis, 'fetch').mockImplementation((input, init) => {
      const url = String(input); requests.push(`${init?.method ?? 'GET'} ${url}`)
      if (url === '/api/auth/csrf') return json({ token: 'csrf' })
      if (url.includes('/sessions/9/exercises/11')) return Promise.resolve(new Response(null, { status: 204 }))
      if (url.endsWith('/sessions/9/complete')) return json({ id: 9 })
      return json({ id: 9, status: 'InProgress', startedAt: '2026-08-13', day: { title: 'Техника', plannedDate: '2026-08-13' }, exercises: [{ trainingExerciseId: 11, name: 'Слалом', instructions: 'Веди мяч', skillCategory: 'BallControl', targetDurationMinutes: 15, equipment: 'Мяч', result: { isCompleted: false } }] })
    })
    render(<MemoryRouter initialEntries={['/player/training/9']}><Routes><Route path="/player/training/:sessionId" element={<WorkoutPage />} /><Route path="/player/progress" element={<div>Прогресс открыт</div>} /></Routes></MemoryRouter>)
    expect(await screen.findByText('Слалом')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Отметить: Слалом' }))
    await waitFor(() => expect(requests.some(x => x.includes('PUT /api/training/sessions/9/exercises/11'))).toBe(true))
    await userEvent.click(screen.getByRole('button', { name: 'Завершить тренировку' }))
    expect(await screen.findByText('Прогресс открыт')).toBeInTheDocument()
    expect(requests.some(x => x.includes('POST /api/training/sessions/9/complete'))).toBe(true)
  })

  it('saves perceived difficulty and exercise feedback', async () => {
    const updates: string[] = []
    vi.spyOn(globalThis, 'fetch').mockImplementation((input, init) => {
      const url = String(input)
      if (url === '/api/auth/csrf') return json({ token: 'csrf' })
      if (url.includes('/sessions/9/exercises/11')) { updates.push(String(init?.body)); return Promise.resolve(new Response(null, { status: 204 })) }
      return json({ id: 9, status: 'InProgress', startedAt: '2026-08-13', day: { title: 'Техника', plannedDate: '2026-08-13' }, exercises: [{ trainingExerciseId: 11, name: 'Слалом', instructions: 'Веди мяч', skillCategory: 'BallControl', targetDurationMinutes: 15, equipment: 'Мяч', result: { isCompleted: false } }] })
    })
    render(<MemoryRouter initialEntries={['/player/training/9']}><Routes><Route path="/player/training/:sessionId" element={<WorkoutPage />} /></Routes></MemoryRouter>)
    await screen.findByText('Слалом')
    await userEvent.selectOptions(screen.getByLabelText('Сложность: Слалом'), '4')
    await userEvent.type(screen.getByLabelText('Комментарий: Слалом'), 'Нужен перерыв')
    await userEvent.tab()
    await waitFor(() => expect(updates).toHaveLength(2))
    expect(updates.at(0)).toContain('"perceivedDifficulty":4')
    expect(updates.at(1)).toContain('Нужен перерыв')
  })
})
