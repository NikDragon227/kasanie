import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AuthProvider } from '../auth'
import { RadarChart, type Skills } from '../components'
import { CityInput } from '../CityInput'
import { EntryLandingPage, LoginForm, RegisterPage, RegistrationChoicePage } from '../pages/PublicPages'
import { AssessmentForm, PlayerDashboard, WorkoutPage } from '../pages/PlayerPages'
import { SportsNearbyPage } from '../pages/SportsNearbyPages'

const json = (value: unknown, status = 200) => Promise.resolve(new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } }))

beforeEach(() => { vi.restoreAllMocks() })

describe('critical workflows', () => {
  it('splits the title page into public search and registration paths', () => {
    render(<MemoryRouter><EntryLandingPage /></MemoryRouter>)
    expect(screen.getByRole('link', { name: /Найти команду/i })).toHaveAttribute('href', '/sports')
    expect(screen.getByRole('link', { name: /Начать тренироваться/i })).toHaveAttribute('href', '/join')
  })

  it('offers all four registration roles', () => {
    render(<MemoryRouter><RegistrationChoicePage /></MemoryRouter>)
    for (const role of ['Игрок', 'Родитель', 'Тренер', 'Организатор']) expect(screen.getByRole('heading', { name: role })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Тренер/ })).toHaveAttribute('href', '/register-coach')
  })

  it('shows the player registration trajectory message', () => {
    render(<MemoryRouter><RegisterPage /></MemoryRouter>)
    expect(screen.getByRole('heading', { name: 'Построй свою траекторию' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Большой путь начинается с малого шага.' })).toBeInTheDocument()
    expect(screen.queryByText('Лев Яшин')).not.toBeInTheDocument()
    expect(screen.getByLabelText('Дата рождения')).toBeInTheDocument()
    for (const deferredField of ['Город', 'Позиция', 'Ведущая нога', 'Опыт']) expect(screen.queryByLabelText(deferredField)).not.toBeInTheDocument()
  })

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

  it('shows public nearby activities without authentication', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation((input) => {
      if (String(input) === '/api/me') return json({ message: 'Unauthorized' }, 401)
      if (String(input) === '/api/public/sports') return json([{ id: 1, slug: 'football', name: 'Футбол' }, { id: 2, slug: 'futsal', name: 'Мини-футбол' }, { id: 3, slug: 'badminton', name: 'Бадминтон' }])
      if (String(input).startsWith('/api/public/activities')) return json({ total: 1, items: [{ activity: { id: 1, slug: 'football-evening', sportSlug: 'football', sport: 'Футбол', eventType: 'Game', title: 'Футбол вечером', description: 'Открытая игра для взрослых', organizerName: 'Команда на Московской', startAt: '2026-08-28T18:00:00Z', endAt: '2026-08-28T20:00:00Z', price: 0, currency: 'RUB', skillLevel: 'Любой', minimumAge: 18, capacity: 12, participantsCount: 4, availablePlaces: 8, waitlistAvailablePlaces: 4, status: 'Published', isRecurring: false, venue: { id: 1, slug: 'central', name: 'Центральное поле', city: 'Казань', district: 'Центр', address: 'Тестовая, 1', latitude: 55.79, longitude: 49.12, indoor: false, isVerified: true } } }] })
      return json({})
    })

    render(<MemoryRouter initialEntries={['/sports']}><AuthProvider><SportsNearbyPage /></AuthProvider></MemoryRouter>)

    expect(await screen.findByText('Футбол вечером')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Доступные активности' })).toBeInTheDocument()
    expect(screen.getByLabelText('Город')).toBeInTheDocument()
    expect(screen.getByLabelText('Район')).toBeInTheDocument()
    expect(screen.getByLabelText('Время')).toBeInTheDocument()
    expect(screen.queryByText('Без регистрации для поиска')).not.toBeInTheDocument()
    expect(screen.getByRole('navigation', { name: 'Спорт' })).toBeInTheDocument()
    expect(screen.getByText('Бадминтон')).toBeInTheDocument()
    expect(screen.queryByText('Мини-футбол')).not.toBeInTheDocument()
    expect(screen.getByRole('navigation', { name: 'Быстрый выбор формата' }).querySelectorAll('svg')).toHaveLength(6)
    expect(screen.getByText('8 мест свободно')).toBeInTheDocument()
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
