import { useEffect, useId, useMemo, useState, type FormEvent } from 'react'
import { Link, useLocation, useParams, useSearchParams } from 'react-router-dom'
import { ApiError, api, post } from '../api'
import { useAuth } from '../auth'
import { CityInput } from '../CityInput'

type Venue = { id: number; slug: string; name: string; city: string; district?: string; address: string; latitude: number; longitude: number; indoor: boolean; isVerified: boolean }
type Activity = { id: number; slug: string; sportSlug: string; sport: string; eventType: string; title: string; description: string; organizerName: string; startAt: string; endAt: string; price: number; currency: string; skillLevel: string; minimumAge: number; maximumAge?: number; capacity: number; participantsCount: number; availablePlaces: number; waitlistAvailablePlaces: number; status: string; isRecurring: boolean; equipmentRequirements?: string; rules?: string; cancellationPolicy?: string; venue: Venue }
type SearchItem = { activity: Activity; distanceKm?: number }
type SearchResult = { total: number; items: SearchItem[] }
type Sport = { id: number; slug: string; name: string }
type Coordinates = [number, number]

type YandexEvent = { get: (name: string) => Coordinates }
type YandexEventManager = { add: (name: string, handler: (event: YandexEvent) => void) => void }
type YandexGeoObjects = { add: (object: unknown) => void; removeAll: () => void; getBounds: () => number[][] | null }
type YandexMap = { geoObjects: YandexGeoObjects; events: YandexEventManager; setBounds: (bounds: number[][], options: Record<string, unknown>) => void; destroy: () => void }
type YandexPlacemark = { events: YandexEventManager }
type YandexMapsApi = {
  ready: (callback: () => void) => void
  Map: new (container: string, state: Record<string, unknown>, options?: Record<string, unknown>) => YandexMap
  Placemark: new (coordinates: Coordinates, properties?: Record<string, unknown>, options?: Record<string, unknown>) => YandexPlacemark
}

declare global { interface Window { ymaps?: YandexMapsApi } }

const yandexMapsApiKey = import.meta.env.VITE_YANDEX_MAPS_API_KEY?.trim() ?? ''
let yandexMapsPromise: Promise<YandexMapsApi> | null = null

const eventLabels: Record<string, string> = {
  Game: 'Игра', GroupTraining: 'Совместная тренировка', CoachTraining: 'Тренировка с тренером',
  OpenTeamTraining: 'Открытая тренировка', TrainingPartner: 'Ищу партнёра', PlayerRecruitment: 'Ищу команду',
  RecurringGroup: 'Регулярная группа', Tournament: 'Турнир', Trial: 'Просмотр', OpenPractice: 'Свободная тренировка'
}

const russianCityDistricts: Record<string, string[]> = {
  Москва: ['Центральный', 'Северный', 'Северо-Восточный', 'Восточный', 'Юго-Восточный', 'Южный', 'Юго-Западный', 'Западный', 'Северо-Западный', 'Зеленоградский', 'Троицкий', 'Новомосковский'],
  'Санкт-Петербург': ['Адмиралтейский', 'Василеостровский', 'Выборгский', 'Калининский', 'Кировский', 'Колпинский', 'Красногвардейский', 'Красносельский', 'Кронштадтский', 'Курортный', 'Московский', 'Невский', 'Петроградский', 'Петродворцовый', 'Приморский', 'Пушкинский', 'Фрунзенский', 'Центральный'],
  Казань: ['Авиастроительный', 'Вахитовский', 'Кировский', 'Московский', 'Ново-Савиновский', 'Приволжский', 'Советский'],
  Новосибирск: ['Дзержинский', 'Железнодорожный', 'Заельцовский', 'Калининский', 'Кировский', 'Ленинский', 'Октябрьский', 'Первомайский', 'Советский', 'Центральный'],
  Екатеринбург: ['Академический', 'Верх-Исетский', 'Железнодорожный', 'Кировский', 'Ленинский', 'Октябрьский', 'Орджоникидзевский', 'Чкаловский'],
  'Нижний Новгород': ['Автозаводский', 'Канавинский', 'Ленинский', 'Московский', 'Нижегородский', 'Приокский', 'Советский', 'Сормовский'],
  Самара: ['Железнодорожный', 'Кировский', 'Красноглинский', 'Куйбышевский', 'Ленинский', 'Октябрьский', 'Промышленный', 'Самарский', 'Советский'],
  Омск: ['Кировский', 'Ленинский', 'Октябрьский', 'Советский', 'Центральный'],
  Челябинск: ['Калининский', 'Курчатовский', 'Ленинский', 'Металлургический', 'Советский', 'Тракторозаводский', 'Центральный'],
  'Ростов-на-Дону': ['Ворошиловский', 'Железнодорожный', 'Кировский', 'Ленинский', 'Октябрьский', 'Первомайский', 'Пролетарский', 'Советский'],
  Уфа: ['Дёмский', 'Калининский', 'Кировский', 'Ленинский', 'Октябрьский', 'Орджоникидзевский', 'Советский'],
  Красноярск: ['Железнодорожный', 'Кировский', 'Ленинский', 'Октябрьский', 'Советский', 'Свердловский', 'Центральный'],
  Пермь: ['Дзержинский', 'Индустриальный', 'Кировский', 'Ленинский', 'Мотовилихинский', 'Орджоникидзевский', 'Свердловский'],
  Волгоград: ['Ворошиловский', 'Дзержинский', 'Кировский', 'Красноармейский', 'Краснооктябрьский', 'Советский', 'Тракторозаводский', 'Центральный'],
  Краснодар: ['Западный', 'Карасунский', 'Прикубанский', 'Центральный']
}

const discoverySportSlugs = new Set(['football', 'basketball', 'running', 'volleyball', 'tennis', 'badminton', 'workout', 'hockey'])
const sportIcons: Record<string, string> = { football: '◉', basketball: '◒', running: '↗', volleyball: '◈', hockey: '◆', tennis: '◌', badminton: '◇', workout: '⌁' }

function FormatIcon({ type }: { type: string }) {
  const shared = { viewBox: '0 0 48 48', 'aria-hidden': true, focusable: false } as const
  if (type === 'all') return <svg {...shared}><rect x="7" y="7" width="14" height="14" rx="4" /><rect x="27" y="7" width="14" height="14" rx="4" /><rect x="7" y="27" width="14" height="14" rx="4" /><path d="M34 26v16M26 34h16" /></svg>
  if (type === 'evening') return <svg {...shared}><path d="M31.5 8.5a16.5 16.5 0 1 0 8 27.8A17.5 17.5 0 0 1 31.5 8.5Z" /><circle cx="30" cy="31" r="6.5" /><path d="m26 29 4-2.4 4 2.4-1.5 4.5h-5Z" /></svg>
  if (type === 'group') return <svg {...shared}><circle cx="15" cy="16" r="5" /><circle cx="33" cy="16" r="5" /><path d="M7 37c.7-7.2 3.4-11 8-11s7.3 3.8 8 11M25 37c.7-7.2 3.4-11 8-11s7.3 3.8 8 11" /><path d="M20 21.5h8M25 18.5l3 3-3 3" /></svg>
  if (type === 'coach') return <svg {...shared}><rect x="6" y="8" width="27" height="31" rx="6" /><path d="M13 30c4-7 8-10 14-13M13 17h7M20 32h7" /><circle cx="38" cy="16" r="4" /><path d="M38 20v9M33 38c.5-6 2.2-9 5-9s4.5 3 5 9" /></svg>
  if (type === 'team') return <svg {...shared}><circle cx="24" cy="13" r="5" /><circle cx="11" cy="22" r="4" /><circle cx="37" cy="22" r="4" /><path d="M15 40c.6-8.7 3.6-13 9-13s8.4 4.3 9 13M4.5 40c.4-6.8 2.6-10.5 6.5-10.5 2.2 0 4 1.1 5.1 3.3M43.5 40c-.4-6.8-2.6-10.5-6.5-10.5-2.2 0-4 1.1-5.1 3.3" /></svg>
  return <svg {...shared}><path d="M15 8h18v8c0 7-3.6 11-9 11s-9-4-9-11Z" /><path d="M15 12H8v3c0 5 3 8 8 8M33 12h7v3c0 5-3 8-8 8M24 27v7M17 40h14M19 34h10" /><circle cx="24" cy="15" r="3" /></svg>
}

const formatDate = (value: string) => new Intl.DateTimeFormat('ru-RU', { weekday: 'short', day: 'numeric', month: 'long', hour: '2-digit', minute: '2-digit' }).format(new Date(value))
const formatPrice = (value: number) => value === 0 ? 'Бесплатно' : `${new Intl.NumberFormat('ru-RU').format(value)} ₽`
const today = () => new Date().toISOString().slice(0, 10)
const escapeHtml = (value: string) => value.replace(/[&<>'"]/g, character => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' })[character] ?? character)

function loadYandexMaps() {
  if (window.ymaps) return Promise.resolve(window.ymaps)
  if (!yandexMapsApiKey) return Promise.reject(new Error('YANDEX_MAPS_KEY_MISSING'))
  if (yandexMapsPromise) return yandexMapsPromise
  yandexMapsPromise = new Promise<YandexMapsApi>((resolve, reject) => {
    const script = document.createElement('script')
    script.src = `https://api-maps.yandex.ru/2.1/?apikey=${encodeURIComponent(yandexMapsApiKey)}&lang=ru_RU`
    script.async = true
    script.onload = () => window.ymaps?.ready(() => window.ymaps ? resolve(window.ymaps) : reject(new Error('YANDEX_MAPS_LOAD_FAILED')))
    script.onerror = () => reject(new Error('YANDEX_MAPS_LOAD_FAILED'))
    document.head.appendChild(script)
  })
  return yandexMapsPromise
}

function PublicHeader() {
  const { user } = useAuth()
  return <header className="nearby-header"><Link className="brand" to="/"><span className="brand-mark">К</span><span><strong>КАСАНИЕ</strong><small>спорт рядом</small></span></Link><nav><Link to="/sports">Найти занятие</Link><Link to={user ? '/organizer/activities' : '/register-organizer'}>Организаторам</Link>{user ? <Link className="button ghost" to="/organizer/activities">Мои события</Link> : <Link className="button ghost" to="/login">Войти</Link>}</nav></header>
}

function YandexActivitiesMap({ items, compact = false }: { items: SearchItem[]; compact?: boolean }) {
  const mapId = `yandex-map-${useId().replace(/:/g, '')}`
  const [loadFailed, setLoadFailed] = useState(false)

  useEffect(() => {
    if (!yandexMapsApiKey || items.length === 0) return
    let disposed = false
    let map: YandexMap | null = null
    void loadYandexMaps().then(ymaps => {
      if (disposed) return
      const first = items[0].activity.venue
      map = new ymaps.Map(mapId, { center: [first.latitude, first.longitude], zoom: 12, controls: ['zoomControl', 'geolocationControl'] })
      items.forEach(({ activity }, index) => {
        const venue = activity.venue
        map?.geoObjects.add(new ymaps.Placemark(
          [venue.latitude, venue.longitude],
          {
            iconContent: String(index + 1),
            balloonContentHeader: escapeHtml(activity.title),
            balloonContentBody: `${escapeHtml(formatDate(activity.startAt))}<br>${escapeHtml(venue.address)}<br><a href="/activities/${encodeURIComponent(activity.slug)}">Открыть событие</a>`
          },
          { preset: 'islands#darkGreenStretchyIcon' }
        ))
      })
      const bounds = map.geoObjects.getBounds()
      if (bounds && items.length > 1) map.setBounds(bounds, { checkZoomRange: true, zoomMargin: 56 })
    }).catch(() => setLoadFailed(true))
    return () => { disposed = true; map?.destroy() }
  }, [items, mapId])

  const showSetup = !yandexMapsApiKey || loadFailed
  return <div className={`nearby-map${compact ? ' compact' : ''}`} aria-label="Карта найденных занятий">
    <div id={mapId} className="yandex-map-canvas" />
    {showSetup && <div className="map-setup"><div className="map-fallback-grid" aria-hidden="true"><i /><i /><i /></div><b>Яндекс Карта</b><span>Здесь будут показаны места игр и тренировок.</span></div>}
    {!showSetup && items.length === 0 && <div className="map-setup"><b>Яндекс Карта</b><span>Здесь появятся найденные события.</span></div>}
    {!compact && <div className="map-caption"><b>Яндекс Карта</b><span>{items.length ? 'Нажмите на метку события' : 'Измените фильтры поиска'}</span></div>}
  </div>
}

function YandexLocationPicker({ value, onChange }: { value: Coordinates | null; onChange: (coordinates: Coordinates) => void }) {
  const mapId = `yandex-picker-${useId().replace(/:/g, '')}`
  const [loadFailed, setLoadFailed] = useState(false)

  useEffect(() => {
    if (!yandexMapsApiKey) return
    let disposed = false
    let map: YandexMap | null = null
    void loadYandexMaps().then(ymaps => {
      if (disposed) return
      const initial: Coordinates = value ?? [55.7963, 49.1064]
      map = new ymaps.Map(mapId, { center: initial, zoom: 12, controls: ['zoomControl', 'geolocationControl', 'searchControl'] })
      const place = (coordinates: Coordinates) => {
        map?.geoObjects.removeAll()
        map?.geoObjects.add(new ymaps.Placemark(coordinates, { iconCaption: 'Точка встречи' }, { preset: 'islands#darkGreenDotIconWithCaption', draggable: true }))
      }
      if (value) place(value)
      map.events.add('click', event => { const coordinates = event.get('coords'); place(coordinates); onChange(coordinates) })
    }).catch(() => setLoadFailed(true))
    return () => { disposed = true; map?.destroy() }
  }, [mapId, onChange, value])

  if (!yandexMapsApiKey || loadFailed) return <div className="coordinate-fallback"><p>Для выбора точки на карте добавьте ключ Яндекс Карт. Пока координаты можно указать вручную.</p><div><label>Широта<input type="number" step="0.000001" value={value?.[0] ?? ''} onChange={event => onChange([Number(event.target.value), value?.[1] ?? 49.1064])} /></label><label>Долгота<input type="number" step="0.000001" value={value?.[1] ?? ''} onChange={event => onChange([value?.[0] ?? 55.7963, Number(event.target.value)])} /></label></div></div>
  return <div className="location-picker"><div id={mapId} className="yandex-map-canvas" /><span>{value ? `${value[0].toFixed(6)}, ${value[1].toFixed(6)}` : 'Нажмите на карту, чтобы поставить точку встречи'}</span></div>
}

export function OrganizerRegisterPage() {
  const { user } = useAuth()
  const [done, setDone] = useState(false)
  const [show, setShow] = useState(false)
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')

  if (user) return <div className="nearby-page"><PublicHeader /><main className="organizer-signup"><section><span className="eyebrow">Вы уже вошли</span><h1>Можно создавать событие.</h1><Link className="button large" to="/organizer/activities">Перейти к моим событиям</Link></section></main></div>

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const values = new FormData(event.currentTarget)
    const dateOfBirth = String(values.get('dateOfBirth') ?? '')
    const birth = new Date(`${dateOfBirth}T00:00:00`)
    const currentDate = new Date()
    const eighteenthBirthday = new Date(birth.getFullYear() + 18, birth.getMonth(), birth.getDate())
    setError('')
    if (!dateOfBirth || Number.isNaN(birth.getTime()) || eighteenthBirthday > currentDate) return setError('Регистрация организатора доступна только с 18 лет.')
    if (String(values.get('password') ?? '').length < 8) return setError('Пароль должен содержать не менее 8 символов.')
    setPending(true)
    try {
      await post('/api/auth/register-organizer', { email: values.get('email'), password: values.get('password'), dateOfBirth, displayName: values.get('displayName'), city: values.get('city') })
      setDone(true)
    } catch (e) {
      const fieldErrors = e instanceof ApiError ? Object.values(e.body.errors as Record<string, string[]> | undefined ?? {}).flat() : []
      setError(fieldErrors.join(' ') || (e instanceof Error ? e.message : 'Не удалось создать аккаунт организатора.'))
    } finally { setPending(false) }
  }

  return <div className="nearby-page"><PublicHeader /><main className="organizer-signup">{done ? <section><span className="eyebrow">Почти готово</span><h1>Подтвердите email.</h1><p>Мы отправили ссылку. После подтверждения войдите и создайте первое событие.</p><Link className="button large" to="/login">Перейти ко входу</Link></section> : <section><div className="organizer-signup-copy"><h1>Создайте<br />активность.</h1></div><form className="organizer-signup-form" onSubmit={submit}><h2>Аккаунт организатора</h2><label>Как вас показывать участникам<input name="displayName" required maxLength={120} placeholder="Алексей или Команда на Московской" /></label><label>Дата рождения<input name="dateOfBirth" type="date" required /></label><label>Город<CityInput required /></label><label>Email<input name="email" type="email" autoComplete="email" required /></label><label>Пароль<span className="password-control"><input name="password" type={show ? 'text' : 'password'} autoComplete="new-password" minLength={8} required /><button type="button" className="password-toggle" onClick={() => setShow(value => !value)} aria-pressed={show}>{show ? 'Скрыть' : 'Показать'}</button></span><small>Не менее 8 символов: строчная и заглавная буквы, цифра и специальный знак.</small></label>{error && <div className="form-error" role="alert">{error}</div>}<button className="button large" disabled={pending}>{pending ? 'Создаём…' : 'Создать аккаунт'}</button><p>Уже есть аккаунт? <Link to="/login" state={{ from: '/organizer/activities' }}>Войти</Link></p></form></section>}</main></div>
}

export function SportsNearbyPage() {
  const [params, setParams] = useSearchParams()
  const [result, setResult] = useState<SearchResult | null>(null)
  const [sports, setSports] = useState<Sport[]>([])
  const [error, setError] = useState('')
  const cityParam = params.get('city') ?? ''
  const [selectedCity, setSelectedCity] = useState(cityParam)
  const query = params.toString()

  useEffect(() => { void api<Sport[]>('/api/public/sports').then(setSports).catch(() => setSports([{ id: 1, slug: 'football', name: 'Футбол' }])) }, [])
  useEffect(() => {
    setError('')
    void api<SearchResult>(`/api/public/activities${query ? `?${query}` : '?sport=football'}`).then(setResult).catch(error => setError(error instanceof Error ? error.message : 'Не удалось загрузить события.'))
  }, [query])
  useEffect(() => setSelectedCity(cityParam), [cityParam])

  const submit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    const next = new URLSearchParams()
    for (const key of ['city', 'district', 'date', 'time', 'sport', 'type']) {
      const value = String(form.get(key) ?? '').trim()
      if (value) next.set(key, value)
    }
    if (form.get('freeOnly')) next.set('freeOnly', 'true')
    if (form.get('availableOnly')) next.set('availableOnly', 'true')
    setParams(next)
  }

  const mapItems = useMemo(() => result?.items ?? [], [result])
  const visibleSports = useMemo(() => sports.filter(sport => discoverySportSlugs.has(sport.slug)), [sports])
  const formatLinks = [
    { value: '', icon: 'all', label: 'Все события' },
    { value: 'Game', icon: 'evening', label: 'Поиграть вечером' },
    { value: 'GroupTraining', icon: 'group', label: 'Совместные тренировки' },
    { value: 'CoachTraining', icon: 'coach', label: 'С тренером' },
    { value: 'PlayerRecruitment', icon: 'team', label: 'Ищу команду' },
    { value: 'Tournament', icon: 'tournament', label: 'Турниры' }
  ]
  const linkForFormat = (value: string) => {
    const next = new URLSearchParams(params)
    if (value) next.set('type', value); else next.delete('type')
    return `/sports${next.size ? `?${next}` : ''}`
  }
  const linkForSport = (value: string) => {
    const next = new URLSearchParams(params)
    next.set('sport', value)
    return `/sports?${next}`
  }
  const districtOptions = russianCityDistricts[selectedCity.trim()] ?? []
  const selectedSportName = sports.find(sport => sport.slug === (params.get('sport') ?? 'football'))?.name ?? 'Спорт'

  return <div className="nearby-page search-discovery"><PublicHeader /><main>
    <section className="nearby-hero">
      <div className="nearby-hero-title"><h1>Спорт рядом с вами</h1></div>
      <form key={`${query}-${sports.length}`} className="nearby-search" onSubmit={submit}>
        <label><b>Город</b><CityInput name="city" defaultValue={params.get('city') ?? ''} onValueChange={setSelectedCity} /></label>
        <label><b>Район</b><input name="district" list="nearby-districts" defaultValue={params.get('district') ?? ''} placeholder={districtOptions.length ? 'Выберите район' : 'Любой район'} /><datalist id="nearby-districts">{districtOptions.map(district => <option key={district} value={district} />)}</datalist></label>
        <label><b>Дата</b><input name="date" type="date" min={today()} defaultValue={params.get('date') ?? ''} /></label>
        <label><b>Время</b><input name="time" type="time" defaultValue={params.get('time') ?? ''} /></label>
        <label><b>Спорт</b><select name="sport" defaultValue={params.get('sport') ?? 'football'}>{visibleSports.map(sport => <option key={sport.id} value={sport.slug}>{sport.name}</option>)}</select></label>
        <label className="nearby-format"><b>Формат</b><select name="type" defaultValue={params.get('type') ?? ''}><option value="">Все форматы</option><option value="Game">Поиграть вечером</option><option value="GroupTraining">Совместная тренировка</option><option value="CoachTraining">Тренировка с тренером</option><option value="PlayerRecruitment">Ищу команду</option><option value="Tournament">Участие в турнире</option></select></label>
        <button className="nearby-search-button" aria-label="Найти события"><span>⌕</span><b>Найти</b></button>
        <div className="nearby-checks"><label><input name="availableOnly" type="checkbox" defaultChecked={params.get('availableOnly') === 'true'} /> Есть места</label><label><input name="freeOnly" type="checkbox" defaultChecked={params.get('freeOnly') === 'true'} /> Бесплатно</label></div>
      </form>
      <nav className="popular-sports" aria-label="Спорт"><b>Спорт</b><div>{visibleSports.map(sport => <Link key={sport.id} className={(params.get('sport') ?? 'football') === sport.slug ? 'active' : ''} to={linkForSport(sport.slug)}><span>{sportIcons[sport.slug] ?? '●'}</span>{sport.name}</Link>)}</div></nav>
      <nav className="nearby-categories" aria-label="Быстрый выбор формата">{formatLinks.map(item => <Link key={item.label} className={`format-${item.icon} ${(params.get('type') ?? '') === item.value ? 'active' : ''}`} to={linkForFormat(item.value)}><span className="format-icon"><FormatIcon type={item.icon} /></span><b>{item.label}</b></Link>)}</nav>
    </section>
    <section className="nearby-results"><div className="nearby-results-head"><div><span className="eyebrow">{selectedSportName} рядом</span><h2>Доступные активности</h2><p>{result ? `${result.total} ${result.total === 1 ? 'вариант' : 'варианта'} по выбранным фильтрам` : 'Подбираем варианты рядом'}</p></div><div className="nearby-results-actions"><Link className="button" to="/register-organizer">+ Стать организатором</Link></div></div>
      {error && <div className="form-error" role="alert">{error}</div>}
      <div className="nearby-results-grid"><div className="activity-list">{result?.items.map(({ activity, distanceKm }) => <ActivityCard key={activity.id} activity={activity} distanceKm={distanceKm} />)}{result && result.total === 0 && <div className="nearby-empty"><b>Пока ничего не найдено</b><p>Попробуйте выбрать другой день, район или формат.</p></div>}</div><YandexActivitiesMap items={mapItems} /></div>
    </section>
  </main><footer className="nearby-footer"><span>Касание · Спорт рядом</span></footer></div>
}

function ActivityCard({ activity, distanceKm }: { activity: Activity; distanceKm?: number }) {
  const start = new Date(activity.startAt)
  const time = new Intl.DateTimeFormat('ru-RU', { hour: '2-digit', minute: '2-digit' }).format(start)
  const day = new Intl.DateTimeFormat('ru-RU', { day: 'numeric', month: 'short' }).format(start)
  return <Link className="activity-card" to={`/activities/${activity.slug}`}>
    <div className={`activity-card-cover activity-${activity.eventType.toLowerCase()}`}><span className="activity-card-badge">{eventLabels[activity.eventType] ?? activity.eventType}</span><span className="activity-card-time"><b>{time}</b><small>{day}</small></span><i aria-hidden="true" /></div>
    <div className="activity-card-body"><div className="activity-card-top"><span>{activity.venue.district || activity.venue.city}</span><b>{formatPrice(activity.price)}</b></div><h3>{activity.title}</h3><p>{activity.description}</p><div className="activity-meta"><span>⌖ {activity.venue.city}{activity.venue.district ? ` · ${activity.venue.district}` : ''}{typeof distanceKm === 'number' ? ` · ${distanceKm} км` : ''}</span><span>◎ {activity.skillLevel}</span><span>Организатор: {activity.organizerName}</span></div><div className="activity-card-footer"><span className={activity.availablePlaces > 0 ? 'places-ok' : 'places-full'}>{activity.availablePlaces > 0 ? `${activity.availablePlaces} мест свободно` : 'Лист ожидания'}</span><span>Подробнее →</span></div></div>
  </Link>
}

const localDateTime = (hoursFromNow: number) => {
  const date = new Date(Date.now() + hoursFromNow * 60 * 60 * 1000)
  date.setMinutes(date.getMinutes() - date.getTimezoneOffset())
  return date.toISOString().slice(0, 16)
}

export function OrganizerActivitiesPage() {
  const [activities, setActivities] = useState<Activity[]>([])
  const [sports, setSports] = useState<Sport[]>([])
  const [venues, setVenues] = useState<Venue[]>([])
  const [venueChoice, setVenueChoice] = useState('')
  const [meetingPoint, setMeetingPoint] = useState<Coordinates | null>(null)
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null)
  const [pending, setPending] = useState(false)
  const reload = async () => setActivities(await api<Activity[]>('/api/organizer/activities/'))

  useEffect(() => {
    void Promise.all([api<Sport[]>('/api/public/sports'), api<Venue[]>('/api/public/venues'), api<Activity[]>('/api/organizer/activities/')]).then(([sportItems, venueItems, activityItems]) => {
      setSports(sportItems); setVenues(venueItems); setActivities(activityItems); setVenueChoice(venueItems[0]?.id.toString() ?? 'new')
    }).catch(error => setMessage({ text: error instanceof Error ? error.message : 'Не удалось загрузить кабинет организатора.', ok: false }))
  }, [])

  const create = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const form = event.currentTarget
    const values = new FormData(form)
    setPending(true); setMessage(null)
    try {
      let venueId = Number(venueChoice)
      if (venueChoice === 'new') {
        if (!meetingPoint) throw new Error('Поставьте точку встречи на карте.')
        const venue = await post<Venue>('/api/organizer/venues/', {
          name: values.get('venueName'), city: values.get('venueCity'), district: values.get('venueDistrict'), address: values.get('venueAddress'),
          latitude: meetingPoint[0], longitude: meetingPoint[1], indoor: values.get('indoor') === 'on', region: null
        })
        venueId = venue.id
        setVenues(current => [...current, venue])
      }
      await post('/api/organizer/activities/', {
        sportId: Number(values.get('sportId')), venueId, eventType: Number(values.get('eventType')),
        title: String(values.get('title') ?? ''), description: String(values.get('description') ?? ''),
        startAt: new Date(String(values.get('startAt'))).toISOString(), endAt: new Date(String(values.get('endAt'))).toISOString(),
        capacity: Number(values.get('capacity')), waitlistCapacity: Number(values.get('waitlistCapacity')), price: Number(values.get('price')),
        skillLevel: String(values.get('skillLevel') ?? 'Любой'), minimumAge: 18, maximumAge: null,
        equipmentRequirements: null, rules: null, cancellationPolicy: 'Сообщите об отмене заранее.', registrationDeadline: null,
        isRecurring: false, recurrenceRule: null
      })
      form.reset(); setMeetingPoint(null); setMessage({ text: 'Черновик создан. Проверьте карточку и опубликуйте событие.', ok: true }); await reload()
    } catch (error) { setMessage({ text: error instanceof Error ? error.message : 'Не удалось создать событие.', ok: false }) } finally { setPending(false) }
  }

  const action = async (activity: Activity, value: 'publish' | 'cancel') => {
    setPending(true); setMessage(null)
    try { await post(`/api/organizer/activities/${activity.id}/${value}`); setMessage({ text: value === 'publish' ? 'Событие опубликовано.' : 'Событие отменено.', ok: true }); await reload() }
    catch (error) { setMessage({ text: error instanceof Error ? error.message : 'Операция не выполнена.', ok: false }) } finally { setPending(false) }
  }

  return <div className="nearby-page"><PublicHeader /><main className="organizer-page"><header><span className="eyebrow">Кабинет организатора</span><h1>Создавайте игры,<br />а люди найдут их сами.</h1><p>Укажите формат, время и точку встречи. Карточка появится в поиске после публикации.</p></header><section className="organizer-grid"><form className="organizer-form" onSubmit={create}><h2>Новое событие</h2><div className="form-grid"><label>Название<input name="title" required maxLength={120} placeholder="Футбол 6×6 вечером" /></label><label>Формат<select name="eventType" defaultValue="0"><option value="0">Поиграть вечером</option><option value="1">Совместная тренировка</option><option value="2">Тренировка с тренером</option><option value="5">Ищу команду на постоянку</option><option value="7">Турнир</option></select></label><label>Вид спорта<select name="sportId" required>{sports.map(sport => <option key={sport.id} value={sport.id}>{sport.name}</option>)}</select></label><label>Место встречи<select value={venueChoice} onChange={event => setVenueChoice(event.target.value)} required>{venues.map(venue => <option key={venue.id} value={venue.id}>{venue.city} · {venue.name}</option>)}<option value="new">Поставить новую точку на карте</option></select></label>{venueChoice === 'new' && <div className="new-venue-fields full"><label>Название места<input name="venueName" required maxLength={160} placeholder="Поле на Московской" /></label><label>Город<input name="venueCity" required maxLength={120} placeholder="Казань" /></label><label>Район<input name="venueDistrict" maxLength={120} placeholder="Вахитовский" /></label><label>Адрес или ориентир<input name="venueAddress" required maxLength={240} placeholder="ул. Московская, 1" /></label><label className="venue-indoor"><input name="indoor" type="checkbox" /> Крытая площадка</label><YandexLocationPicker value={meetingPoint} onChange={setMeetingPoint} /></div>}<label>Начало<input name="startAt" type="datetime-local" required defaultValue={localDateTime(24)} /></label><label>Окончание<input name="endAt" type="datetime-local" required defaultValue={localDateTime(26)} /></label><label>Участников<input name="capacity" type="number" min="2" max="500" defaultValue="12" required /></label><label>Лист ожидания<input name="waitlistCapacity" type="number" min="0" max="500" defaultValue="4" required /></label><label>Цена, ₽<input name="price" type="number" min="0" step="1" defaultValue="0" required /></label><label>Уровень<select name="skillLevel" defaultValue="Любой"><option>Любой</option><option>Начинающий</option><option>Средний</option><option>Продвинутый</option></select></label><label className="full">Описание<textarea name="description" required maxLength={4000} placeholder="Для кого занятие, что будет происходить и что взять с собой." /></label></div><button className="button large" disabled={pending || sports.length === 0}>{pending ? 'Сохраняем…' : 'Создать черновик'}</button>{message && <div className={message.ok ? 'success-message' : 'form-error'}>{message.text}</div>}</form><section className="organizer-list"><div><span className="eyebrow">Ваши события</span><h2>{activities.length}</h2></div>{activities.map(item => <article key={item.id}><span className={`activity-status ${item.status.toLowerCase()}`}>{item.status}</span><h3>{item.title}</h3><p>{formatDate(item.startAt)} · {item.venue.name}</p><small>{item.participantsCount}/{item.capacity} участников</small><div>{item.status === 'Draft' && <button className="button" disabled={pending} onClick={() => void action(item, 'publish')}>Опубликовать</button>}{item.status !== 'Cancelled' && item.status !== 'Completed' && <button className="button ghost" disabled={pending} onClick={() => void action(item, 'cancel')}>Отменить</button>}<Link to={`/activities/${item.slug}`}>Карточка →</Link></div></article>)}{activities.length === 0 && <div className="nearby-empty"><b>Событий пока нет</b><p>Создайте первый черновик по форме слева.</p></div>}</section></section></main></div>
}

export function PublicActivityPage() {
  const { slug = '' } = useParams()
  const location = useLocation()
  const { user } = useAuth()
  const [activity, setActivity] = useState<Activity | null>(null)
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null)
  const [pending, setPending] = useState(false)
  useEffect(() => { void api<Activity>(`/api/public/activities/${slug}`).then(setActivity).catch(error => setMessage({ text: error instanceof Error ? error.message : 'Событие не найдено.', ok: false })) }, [slug])
  const join = async () => { if (!activity) return; setPending(true); setMessage(null); try { const result = await post<{ status: string }>(`/api/activities/${activity.id}/join`); setMessage({ text: result.status === 'Waitlisted' ? 'Вы добавлены в лист ожидания.' : 'Вы записаны. Организатор увидит ваше участие.', ok: true }); setActivity(await api<Activity>(`/api/public/activities/${slug}`)) } catch (error) { setMessage({ text: error instanceof Error ? error.message : 'Не удалось записаться.', ok: false }) } finally { setPending(false) } }
  if (!activity) return <div className="nearby-page"><PublicHeader /><main className="activity-detail-loading">{message?.text ?? 'Загружаем событие…'}</main></div>
  const canJoin = activity.availablePlaces > 0 || activity.waitlistAvailablePlaces > 0
  return <div className="nearby-page"><PublicHeader /><main className="activity-detail"><Link className="back-link" to="/sports">← Все занятия</Link><section className="activity-detail-hero"><div><span className="activity-type">{eventLabels[activity.eventType] ?? activity.eventType}</span><h1>{activity.title}</h1><p>{activity.description}</p><div className="activity-detail-meta"><span><small>Когда</small><b>{formatDate(activity.startAt)}</b></span><span><small>Где</small><b>{activity.venue.name}</b></span><span><small>Организатор</small><b>{activity.organizerName}</b></span><span><small>Стоимость</small><b>{formatPrice(activity.price)}</b></span></div></div><aside><span className="eyebrow">Запись</span><strong>{activity.availablePlaces}</strong><small>{activity.availablePlaces > 0 ? `свободных мест из ${activity.capacity}` : activity.waitlistAvailablePlaces > 0 ? `мест в листе ожидания: ${activity.waitlistAvailablePlaces}` : 'запись закрыта'}</small>{user ? <button className="button large" disabled={pending || !canJoin} onClick={() => void join()}>{pending ? 'Записываем…' : activity.availablePlaces > 0 ? 'Присоединиться' : activity.waitlistAvailablePlaces > 0 ? 'Встать в лист ожидания' : 'Мест нет'}</button> : <Link className="button large" to="/login" state={{ from: location.pathname }}>Войти и присоединиться</Link>}{message && <div className={message.ok ? 'success-message' : 'form-error'} role={message.ok ? 'status' : 'alert'}>{message.text}</div>}</aside></section><section className="activity-detail-grid"><article><span className="eyebrow">Площадка</span><h2>{activity.venue.name}</h2><p>{activity.venue.city}{activity.venue.district ? `, ${activity.venue.district}` : ''}<br />{activity.venue.address}</p><YandexActivitiesMap compact items={[{ activity }]} /></article><article><span className="eyebrow">Условия</span><h2>Что важно знать</h2><dl><div><dt>Уровень</dt><dd>{activity.skillLevel}</dd></div><div><dt>Возраст</dt><dd>от {activity.minimumAge} лет{activity.maximumAge ? ` до ${activity.maximumAge}` : ''}</dd></div><div><dt>Инвентарь</dt><dd>{activity.equipmentRequirements ?? 'Уточните у организатора'}</dd></div><div><dt>Правила</dt><dd>{activity.rules ?? 'Уважайте других участников и сообщайте об отмене заранее.'}</dd></div></dl></article></section></main></div>
}
