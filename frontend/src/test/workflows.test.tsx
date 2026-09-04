import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AuthProvider } from '../auth'
import { RadarChart, type Skills } from '../components'
import { CityInput } from '../CityInput'
import { LoginForm, PortalUserRegisterPage, RegisterPage, RegistrationChoicePage } from '../pages/PublicPages'
import { HomePage } from '../pages/HomePage'
import { AssessmentForm, PlayerDashboard, WorkoutPage } from '../pages/PlayerPages'
import { AdminDashboard } from '../pages/RolePages'
import { GuestParticipationPage, MyActivitiesPage, OrganizerActivitiesPage, PublicActivityPage, SportsNearbyPage } from '../pages/SportsNearbyPages'

const json = (value: unknown, status = 200) => Promise.resolve(new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } }))
const publicActivity = {
  id: 1, slug: 'football-evening', sportSlug: 'football', sport: 'Футбол', eventType: 'Game', gameFormat: '6×6', title: 'Футбол вечером',
  description: 'Открытая игра для взрослых', organizerName: 'Команда на Московской', startAt: '2099-08-28T18:00:00Z',
  endAt: '2099-08-28T20:00:00Z', price: 0, currency: 'RUB', skillLevel: 'Любой', minimumAge: 18, capacity: 12,
  participantsCount: 4, availablePlaces: 8, waitlistAvailablePlaces: 4, status: 'Published', isRecurring: false,
  venue: { id: 1, slug: 'central', name: 'Центральное поле', city: 'Казань', district: 'Центр', address: 'Тестовая, 1', latitude: 55.79, longitude: 49.12, indoor: false, isVerified: true }
}

beforeEach(() => { vi.restoreAllMocks() })

describe('critical workflows', () => {
  it('presents the product, live platform statistics and primary public paths', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(input => {
      if (String(input) === '/api/public/platform-stats') return json({ users: 250000, teams: 18500, tournaments: 3200, coaches: 6800, trustPercent: null })
      return json({}, 404)
    })
    render(<MemoryRouter><HomePage /></MemoryRouter>)
    expect(screen.getByRole('heading', { name: /Спорт начинается.*первого касания/i })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Найти игру/i })).toHaveAttribute('href', '/sports')
    expect(screen.getByRole('link', { name: /Стать участником/i })).toHaveAttribute('href', '/join')
    expect(screen.getByRole('link', { name: /Дети и родители:/i })).toHaveAttribute('href', '/register-parent')
    expect(screen.getByRole('link', { name: /Тренеры:/i })).toHaveAttribute('href', '/register-coach')
    expect(await screen.findByText(/250\s000/)).toBeInTheDocument()
    expect(screen.getByText(/18\s500/)).toBeInTheDocument()
    expect(screen.getByText('доверие пользователей')).toBeInTheDocument()
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

  it.each([['Parent', 'родителя'], ['Coach', 'тренера']] as const)('simplifies the %s registration story', (role, roleName) => {
    render(<MemoryRouter><PortalUserRegisterPage role={role} /></MemoryRouter>)
    expect(screen.getByRole('heading', { name: `Кабинет ${roleName}` })).toBeInTheDocument()
    expect(screen.getByText('Оценка навыков, персональные тренировки и связь с тренером — всё в одном месте.')).toBeInTheDocument()
    expect(screen.queryByText('Система развития игрока')).not.toBeInTheDocument()
    expect(screen.getByLabelText('Дата рождения')).toBeInTheDocument()
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
    expect(screen.getByRole('button', { name: /Организатор\s*organizer@kasanie\.local/i })).toBeInTheDocument()
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

  it('shows real platform statistics and changes the reporting period', async () => {
    const requested: string[] = []
    vi.spyOn(globalThis, 'fetch').mockImplementation((input) => {
      requested.push(String(input))
      const days = String(input).includes('days=7') ? 7 : 30
      return json({ periodDays: days, generatedAt: '2026-08-29T12:00:00Z', users: 38, newUsers: 6, activeUsers: 21, players: 20, coaches: 4, parents: 8, organizers: 6, schools: 3, activeSchools: 3, teams: 7, activeTeams: 6, publishedActivities: 35, newActivities: 5, upcomingActivities: 9, registrations: 48, newRegistrations: 10, completedTrainings: 12, exercises: 20, assessments: 6, programs: 4, auditEvents: 125, roles: [{ role: 'Player', count: 20 }], trend: Array.from({ length: days }, (_, index) => ({ date: `2026-08-${String(index + 1).padStart(2, '0')}`, users: index === 0 ? 6 : 0, activities: 0, registrations: 0, trainings: 0 })), activityTypes: [{ type: 'Game', count: 12 }], topCities: [{ city: 'Казань', count: 12 }] })
    })

    render(<MemoryRouter><AdminDashboard /></MemoryRouter>)

    expect(await screen.findByRole('heading', { name: 'Статистика платформы' })).toBeInTheDocument()
    expect(screen.getByText('+6 за 30 дней')).toBeInTheDocument()
    expect(screen.getByText('Казань')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: '7 дней' }))
    await waitFor(() => expect(requested.some(x => x.includes('days=7'))).toBe(true))
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
    expect(screen.queryByLabelText('Район')).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Время')).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Ещё фильтры' }))
    expect(screen.getByLabelText('Район')).toBeInTheDocument()
    expect(screen.getByLabelText('Время')).toBeInTheDocument()
    expect(screen.queryByText('Без регистрации для поиска')).not.toBeInTheDocument()
    expect(screen.queryByRole('navigation', { name: 'Спорт' })).not.toBeInTheDocument()
    expect(screen.getByRole('option', { name: 'Бадминтон' })).toBeInTheDocument()
    expect(screen.queryByText('Мини-футбол')).not.toBeInTheDocument()
    expect(screen.getByRole('navigation', { name: 'Быстрый выбор формата' }).querySelectorAll('img')).toHaveLength(6)
    expect(screen.getByText('8 мест свободно')).toBeInTheDocument()
  })

  it('searches public activities around the current location and radius', async () => {
    const requested: string[] = []
    vi.spyOn(globalThis, 'fetch').mockImplementation((input) => {
      requested.push(String(input))
      if (String(input) === '/api/me') return json({ message: 'Unauthorized' }, 401)
      if (String(input) === '/api/public/sports') return json([{ id: 1, slug: 'football', name: 'Футбол' }])
      if (String(input).startsWith('/api/public/activities')) return json({ total: 0, items: [] })
      return json({})
    })
    Object.defineProperty(navigator, 'geolocation', {
      configurable: true,
      value: { getCurrentPosition: vi.fn((success: PositionCallback) => success({ coords: { latitude: 55.7963, longitude: 49.1064 } } as GeolocationPosition)) }
    })

    render(<MemoryRouter initialEntries={['/sports']}><AuthProvider><SportsNearbyPage /></AuthProvider></MemoryRouter>)
    await screen.findByRole('heading', { name: 'Доступные активности' })
    await userEvent.click(screen.getByRole('button', { name: 'Рядом со мной' }))

    await waitFor(() => expect(requested.some(url => url.includes('latitude=55.796300') && url.includes('longitude=49.106400') && url.includes('radiusKm=10'))).toBe(true))
    expect(screen.getByText('Показываем активности в радиусе 10 км от выбранной точки.')).toBeInTheDocument()
  })

  it('loads every sport by default and can return to all sports', async () => {
    const requested: string[] = []
    vi.spyOn(globalThis, 'fetch').mockImplementation((input) => {
      requested.push(String(input))
      if (String(input) === '/api/me') return json({ message: 'Unauthorized' }, 401)
      if (String(input) === '/api/public/sports') return json([{ id: 1, slug: 'football', name: 'Футбол' }, { id: 2, slug: 'hockey', name: 'Хоккей' }])
      if (String(input).startsWith('/api/public/activities')) return json({ total: 0, items: [] })
      return json({})
    })

    render(<MemoryRouter initialEntries={['/sports']}><AuthProvider><SportsNearbyPage /></AuthProvider></MemoryRouter>)
    await screen.findByRole('heading', { name: 'Доступные активности' })

    await waitFor(() => expect(requested.some(url => url.startsWith('/api/public/activities'))).toBe(true))
    expect(requested.some(url => url.startsWith('/api/public/activities') && url.includes('sport='))).toBe(false)
    expect(screen.getByRole('combobox', { name: 'Спорт' })).toHaveValue('')
    expect(screen.getByRole('option', { name: 'Все виды спорта' })).toBeInTheDocument()

    await userEvent.selectOptions(screen.getByRole('combobox', { name: 'Спорт' }), 'hockey')
    await userEvent.click(screen.getByRole('button', { name: 'Найти события' }))
    await waitFor(() => expect(requested.some(url => url.includes('sport=hockey'))).toBe(true))

    await userEvent.selectOptions(screen.getByRole('combobox', { name: 'Спорт' }), '')
    await userEvent.click(screen.getByRole('button', { name: 'Найти события' }))
    await waitFor(() => expect(requested.filter(url => url.startsWith('/api/public/activities')).at(-1)).not.toContain('sport='))
  })

  it('filters by the selected sport game format and persists sorting in the URL', async () => {
    const requested: string[] = []
    vi.spyOn(globalThis, 'fetch').mockImplementation((input) => {
      requested.push(String(input))
      if (String(input) === '/api/me') return json({ message: 'Unauthorized' }, 401)
      if (String(input) === '/api/public/sports') return json([{ id: 1, slug: 'football', name: 'Футбол' }, { id: 2, slug: 'hockey', name: 'Хоккей' }])
      if (String(input).startsWith('/api/public/activities')) return json({ total: 0, items: [] })
      return json({})
    })

    render(<MemoryRouter initialEntries={['/sports']}><AuthProvider><SportsNearbyPage /></AuthProvider></MemoryRouter>)
    await screen.findByRole('heading', { name: 'Доступные активности' })
    await userEvent.selectOptions(screen.getByRole('combobox', { name: 'Спорт' }), 'hockey')
    await userEvent.click(screen.getByRole('button', { name: 'Ещё фильтры' }))
    expect(screen.getByRole('option', { name: '5+1 — 5 полевых и вратарь' })).toBeInTheDocument()
    await userEvent.selectOptions(screen.getByRole('combobox', { name: 'Формат игры' }), '5+1')
    await userEvent.click(screen.getByRole('button', { name: 'Найти события' }))
    await waitFor(() => expect(requested.some(url => url.includes('sport=hockey') && url.includes('gameFormat=5%2B1'))).toBe(true))
    await userEvent.selectOptions(screen.getByRole('combobox', { name: 'Сортировка результатов' }), 'availability')
    await waitFor(() => expect(requested.some(url => url.includes('sort=availability'))).toBe(true))
  })

  it('shows browser instructions when geolocation permission is blocked', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation((input) => {
      if (String(input) === '/api/me') return json({ message: 'Unauthorized' }, 401)
      if (String(input) === '/api/public/sports') return json([{ id: 1, slug: 'football', name: 'Футбол' }])
      if (String(input).startsWith('/api/public/activities')) return json({ total: 0, items: [] })
      return json({})
    })
    Object.defineProperty(navigator, 'geolocation', {
      configurable: true,
      value: { getCurrentPosition: vi.fn((_success: PositionCallback, failure: PositionErrorCallback) => failure({ code: 1, PERMISSION_DENIED: 1, POSITION_UNAVAILABLE: 2, TIMEOUT: 3, message: 'denied' })) }
    })
    Object.defineProperty(navigator, 'permissions', {
      configurable: true,
      value: { query: vi.fn(() => Promise.resolve({ state: 'denied' })) }
    })

    render(<MemoryRouter initialEntries={['/sports']}><AuthProvider><SportsNearbyPage /></AuthProvider></MemoryRouter>)
    await screen.findByRole('heading', { name: 'Доступные активности' })
    await userEvent.click(screen.getByRole('button', { name: 'Рядом со мной' }))
    const permissionLink = await screen.findByRole('button', { name: 'Разрешить доступ к геопозиции' })
    await userEvent.click(permissionLink)

    expect(screen.getByText(/Нажмите значок замка слева от адреса сайта/)).toBeInTheDocument()
  })

  it('lets a visitor mark attendance without creating an account', async () => {
    const requests: string[] = []
    vi.spyOn(globalThis, 'fetch').mockImplementation((input, init) => {
      const url = String(input); requests.push(`${init?.method ?? 'GET'} ${url}`)
      if (url === '/api/me') return json({ message: 'Unauthorized' }, 401)
      if (url === '/api/auth/csrf') return json({ token: 'csrf' })
      if (url === '/api/public/activities/football-evening') return json(publicActivity)
      if (url === '/api/public/activities/1/guest-join') return json({ activityId: 1, status: 'Confirmed', name: 'Алексей', cancellationToken: 'a'.repeat(64), managePath: `/guest/participations/${'a'.repeat(64)}` })
      return json({})
    })

    render(<MemoryRouter initialEntries={['/activities/football-evening']}><AuthProvider><Routes><Route path="/activities/:slug" element={<PublicActivityPage />} /></Routes></AuthProvider></MemoryRouter>)

    await userEvent.click(await screen.findByRole('button', { name: 'Я буду' }))
    await userEvent.type(screen.getByLabelText('Как вас зовут'), 'Алексей')
    await userEvent.type(screen.getByLabelText('Телефон, email или Telegram'), '@alexey_sport')
    await userEvent.click(screen.getByRole('checkbox', { name: /Мне исполнилось 18 лет/i }))
    await userEvent.click(screen.getByRole('button', { name: 'Подтвердить участие' }))

    expect(await screen.findByRole('heading', { name: 'Вы в игре, Алексей!' })).toBeInTheDocument()
    expect(screen.getAllByRole('link', { name: 'Добавить в календарь' })[0]).toHaveAttribute('download', 'kasanie-football-evening.ics')
    expect(screen.getByRole('link', { name: 'Управлять записью или отменить' })).toHaveAttribute('href', `/guest/participations/${'a'.repeat(64)}`)
    expect(requests).toContain('POST /api/public/activities/1/guest-join')
  })

  it('lets a guest cancel participation using only the private management link', async () => {
    const token = 'b'.repeat(64)
    let status = 'Confirmed'
    vi.spyOn(globalThis, 'fetch').mockImplementation((input, init) => {
      const url = String(input)
      if (url === '/api/me') return json({ message: 'Unauthorized' }, 401)
      if (url === '/api/auth/csrf') return json({ token: 'csrf' })
      if (url === `/api/public/guest-participations/${token}`) return json({ guestName: 'Алексей', status, joinedAt: '2099-08-20T12:00:00Z', activity: publicActivity })
      if (url === `/api/public/guest-participations/${token}/cancel` && init?.method === 'POST') { status = 'Cancelled'; return Promise.resolve(new Response(null, { status: 204 })) }
      return json({})
    })

    render(<MemoryRouter initialEntries={[`/guest/participations/${token}`]}><AuthProvider><Routes><Route path="/guest/participations/:token" element={<GuestParticipationPage />} /></Routes></AuthProvider></MemoryRouter>)
    expect(await screen.findByRole('heading', { name: 'Алексей, ваша запись' })).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Отменить участие' }))
    await userEvent.click(screen.getByRole('button', { name: 'Да, отменить запись' }))
    expect(await screen.findByText('Запись отменена, место освобождено.')).toBeInTheDocument()
  })

  it('restores the persisted participation status on the activity page', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation((input) => {
      const url = String(input)
      if (url === '/api/me') return json({ id: 'adult-a', email: 'adult@example.test', roles: ['Coach'] })
      if (url === '/api/public/activities/football-evening') return json(publicActivity)
      if (url === '/api/activities/1/participation') return json({ activityId: 1, status: 'Confirmed', joinedAt: '2026-08-31T12:00:00Z', confirmedAt: '2026-08-31T12:00:00Z' })
      return json({})
    })

    render(<MemoryRouter initialEntries={['/activities/football-evening']}><AuthProvider><Routes><Route path="/activities/:slug" element={<PublicActivityPage />} /></Routes></AuthProvider></MemoryRouter>)

    expect(await screen.findByText('Вы записаны')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Отменить участие' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Присоединиться' })).not.toBeInTheDocument()
  })

  it('shows the authenticated user activities and cancellation confirmation', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation((input) => {
      const url = String(input)
      if (url === '/api/me') return json({ id: 'adult-a', email: 'adult@example.test', roles: ['Coach'] })
      if (url === '/api/activities/mine') return json([{ activity: publicActivity, participation: { activityId: 1, status: 'Confirmed', joinedAt: '2026-08-31T12:00:00Z', confirmedAt: '2026-08-31T12:00:00Z' } }])
      return json({})
    })

    render(<MemoryRouter initialEntries={['/my/activities']}><AuthProvider><MyActivitiesPage /></AuthProvider></MemoryRouter>)

    expect(await screen.findByRole('heading', { name: 'Футбол вечером' })).toBeInTheDocument()
    expect(screen.getByText('Вы записаны')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Отменить участие' }))
    expect(screen.getByRole('button', { name: 'Подтвердить отмену' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Оставить запись' })).toBeInTheDocument()
  })

  it('lets an organizer choose whether they occupy a participant place', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation((input) => {
      const url = String(input)
      if (url === '/api/me') return json({ id: 'organizer-a', email: 'organizer@example.test', roles: ['Organizer'] })
      if (url === '/api/public/sports') return json([{ id: 1, slug: 'football', name: 'Футбол' }, { id: 2, slug: 'basketball', name: 'Баскетбол' }, { id: 3, slug: 'futsal', name: 'Мини-футбол' }, { id: 4, slug: 'hockey', name: 'Хоккей' }, { id: 5, slug: 'volleyball', name: 'Волейбол' }])
      if (url === '/api/public/venues') return json([{ id: 1, slug: 'central', name: 'Центральное поле', city: 'Казань', district: 'Центр', address: 'Тестовая, 1', latitude: 55.79, longitude: 49.12, indoor: false, isVerified: true }])
      if (url === '/api/organizer/activities/') return json([])
      return json({})
    })

    render(<MemoryRouter><AuthProvider><OrganizerActivitiesPage /></AuthProvider></MemoryRouter>)

    await userEvent.click(await screen.findByRole('button', { name: '+ Новое событие' }))
    const participates = await screen.findByRole('checkbox', { name: /Я тоже участвую в активности/i })
    expect(screen.getByRole('combobox', { name: 'Формат игры' })).toHaveValue('5×5')
    expect(screen.queryByRole('option', { name: 'Мини-футбол' })).not.toBeInTheDocument()
    await userEvent.selectOptions(screen.getByRole('combobox', { name: 'Вид спорта' }), '2')
    expect(screen.getByRole('combobox', { name: 'Формат игры' })).toHaveValue('3×3')
    expect(screen.getByRole('option', { name: '5×5 — классический баскетбол' })).toBeInTheDocument()
    expect(screen.queryByRole('option', { name: /6×6/ })).not.toBeInTheDocument()
    await userEvent.selectOptions(screen.getByRole('combobox', { name: 'Вид спорта' }), '4')
    expect(screen.getByRole('combobox', { name: 'Формат игры' })).toHaveValue('3+1')
    expect(screen.getByRole('option', { name: '5+1 — 5 полевых и вратарь' })).toBeInTheDocument()
    expect(screen.queryByRole('option', { name: /4×4/ })).not.toBeInTheDocument()
    await userEvent.selectOptions(screen.getByRole('combobox', { name: 'Вид спорта' }), '5')
    expect(screen.getByRole('option', { name: '2×2 — пляжный волейбол' })).toBeInTheDocument()
    expect(screen.getByRole('option', { name: '6×6 — классический волейбол' })).toBeInTheDocument()
    expect(screen.queryByRole('option', { name: /3×3/ })).not.toBeInTheDocument()
    expect(participates).not.toBeChecked()
    await userEvent.click(participates)
    expect(participates).toBeChecked()
    expect(screen.getByText(/займёт одно место из общего лимита/i)).toBeInTheDocument()
  })

  it('lets the organizer delete an own draft with confirmation and sign out', async () => {
    const requests: string[] = []
    let deleted = false
    vi.spyOn(globalThis, 'fetch').mockImplementation((input, init) => {
      const url = String(input); const method = init?.method ?? 'GET'; requests.push(`${method} ${url}`)
      if (url === '/api/me') return json({ id: 'organizer-a', email: 'organizer@example.test', roles: ['Organizer'] })
      if (url === '/api/auth/csrf') return json({ token: 'csrf' })
      if (url === '/api/auth/logout') return Promise.resolve(new Response(null, { status: 204 }))
      if (url === '/api/public/sports') return json([{ id: 1, slug: 'football', name: 'Футбол' }])
      if (url === '/api/public/venues') return json([publicActivity.venue])
      if (url === '/api/organizer/activities/1' && method === 'DELETE') { deleted = true; return Promise.resolve(new Response(null, { status: 204 })) }
      if (url === '/api/organizer/activities/') return json(deleted ? [] : [{ ...publicActivity, status: 'Draft', organizerParticipates: false, isCurrentUserOrganizer: true }])
      return json({})
    })

    render(<MemoryRouter initialEntries={['/organizer/activities']}><AuthProvider><Routes><Route path="/organizer/activities" element={<OrganizerActivitiesPage />} /><Route path="/login" element={<div>Экран входа</div>} /></Routes></AuthProvider></MemoryRouter>)

    expect(await screen.findByRole('heading', { name: 'Футбол вечером' })).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Удалить' }))
    expect(screen.getByRole('button', { name: 'Подтвердить удаление' })).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Подтвердить удаление' }))
    await waitFor(() => expect(requests).toContain('DELETE /api/organizer/activities/1'))
    expect(await screen.findByText('Событий пока нет')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Меню профиля' }))
    await userEvent.click(screen.getByRole('menuitem', { name: 'Выйти' }))
    expect(await screen.findByText('Экран входа')).toBeInTheDocument()
    expect(requests).toContain('POST /api/auth/logout')
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
