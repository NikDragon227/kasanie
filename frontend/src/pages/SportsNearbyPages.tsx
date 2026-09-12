import { useCallback, useEffect, useId, useMemo, useRef, useState, type FormEvent } from 'react'
import { Link, useLocation, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { ApiError, api, post, put, remove } from '../api'
import { useAuth } from '../auth'
import { primaryRole, roleHome, roleLabel } from '../components'
import { CityInput } from '../CityInput'

type Venue = { id: number; slug: string; name: string; city: string; district?: string; address: string; latitude: number; longitude: number; indoor: boolean; isVerified: boolean }
type Activity = { id: number; slug: string; sportSlug: string; sport: string; eventType: string; gameFormat?: string; title: string; description: string; organizerName: string; startAt: string; endAt: string; price: number; currency: string; skillLevel: string; minimumAge: number; maximumAge?: number; capacity: number; waitlistCapacity?: number; participantsCount: number; availablePlaces: number; waitlistAvailablePlaces: number; status: string; isRecurring: boolean; organizerParticipates: boolean; isCurrentUserOrganizer: boolean; equipmentRequirements?: string; rules?: string; cancellationPolicy?: string; venue: Venue }
type SearchItem = { activity: Activity; distanceKm?: number }
type SearchResult = { total: number; items: SearchItem[] }
type Sport = { id: number; slug: string; name: string }
type GuestJoinResult = { activityId: number; status: string; name: string; cancellationToken: string; managePath: string }
type GuestParticipation = { guestName: string; status: string; joinedAt: string; cancelledAt?: string; activity: Activity }
type Coordinates = [number, number]
type Participation = { activityId: number; status: string; joinedAt: string; confirmedAt?: string; cancelledAt?: string }
type ParticipantActivity = { activity: Activity; participation: Participation }
type OrganizerParticipant = { id: number; displayName: string; contact?: string; status: string; joinedAt: string; confirmedAt?: string; cancelledAt?: string; reportCount: number; viewerHasReported: boolean }
type OrganizerParticipants = { activityId: number; capacity: number; confirmedCount: number; waitlistedCount: number; cancelledCount: number; items: OrganizerParticipant[] }
type ParticipantReport = { reportId: number; reason: string; comment: string; createdAt: string; isMine: boolean; sourceActivityTitle: string }

type YandexEvent = { get: <T = Coordinates>(name: string) => T }
type YandexEventManager = { add: (name: string, handler: (event: YandexEvent) => void) => void }
type YandexGeometry = { getCoordinates: () => Coordinates }
type YandexGeoObjects = { add: (object: unknown) => void; removeAll: () => void; getBounds: () => number[][] | null }
type YandexMap = { geoObjects: YandexGeoObjects; events: YandexEventManager; setBounds: (bounds: number[][], options: Record<string, unknown>) => void; setCenter: (center: Coordinates, zoom?: number, options?: Record<string, unknown>) => void; getCenter: () => Coordinates; getBounds: () => number[][]; destroy: () => void }
type YandexPlacemark = { events: YandexEventManager; options: { set: (name: string, value: unknown) => void }; geometry: YandexGeometry }
type YandexMapsApi = {
  ready: (callback: () => void) => void
  Map: new (container: string, state: Record<string, unknown>, options?: Record<string, unknown>) => YandexMap
  Placemark: new (coordinates: Coordinates, properties?: Record<string, unknown>, options?: Record<string, unknown>) => YandexPlacemark
}

type GeocodedLocation = { coordinates: Coordinates; address: string; city: string; district: string; region: string }
type MapViewport = { latitude: number; longitude: number; radiusKm: number }

declare global { interface Window { ymaps?: YandexMapsApi } }

const yandexMapsApiKey = import.meta.env.VITE_YANDEX_MAPS_API_KEY?.trim() ?? ''
let yandexMapsPromise: Promise<YandexMapsApi> | null = null

const eventLabels: Record<string, string> = {
  Game: 'Игра', GroupTraining: 'Совместная тренировка', CoachTraining: 'Тренировка с тренером',
  OpenTeamTraining: 'Открытая тренировка', TrainingPartner: 'Ищу партнёра', PlayerRecruitment: 'Ищу команду',
  RecurringGroup: 'Регулярная группа', Tournament: 'Турнир', Trial: 'Просмотр', OpenPractice: 'Свободная тренировка'
}

const participationLabels: Record<string, string> = {
  Pending: 'Ожидает подтверждения', Confirmed: 'Вы записаны', Waitlisted: 'Вы в листе ожидания', Cancelled: 'Участие отменено',
  Attended: 'Вы участвовали', NoShow: 'Не участвовали', Rejected: 'Запись отменена организатором'
}
const organizerParticipationLabels: Record<string, string> = {
  Pending: 'Ожидает', Confirmed: 'Подтверждён', Waitlisted: 'Лист ожидания', Cancelled: 'Отменил участие',
  Attended: 'Участвовал', NoShow: 'Не пришёл', Rejected: 'Удалён организатором'
}
const reportReasonLabels: Record<string, string> = {
  NoShow: 'Не пришёл', LateArrival: 'Опоздал', AggressiveOrConflict: 'Агрессия или конфликт',
  UnsafePlay: 'Опасная игра', Other: 'Другое'
}
const reportReasonOrder = ['NoShow', 'LateArrival', 'AggressiveOrConflict', 'UnsafePlay', 'Other']

function ParticipantRosterRow({ participant, activityPast, participantLoading, confirmRemovalId, onConfirmRemoval, onRemove, onFileReport, onRetractReport, loadReports }: {
  participant: OrganizerParticipant
  activityPast: boolean
  participantLoading: boolean
  confirmRemovalId: number | null
  onConfirmRemoval: (id: number | null) => void
  onRemove: (id: number) => Promise<void>
  onFileReport: (id: number, reason: string, comment: string) => Promise<void>
  onRetractReport: (reportId: number) => Promise<void>
  loadReports: (id: number) => Promise<ParticipantReport[]>
}) {
  const [showReports, setShowReports] = useState(false)
  const [reports, setReports] = useState<ParticipantReport[] | null>(null)
  const [showForm, setShowForm] = useState(false)
  const [reason, setReason] = useState('NoShow')
  const [comment, setComment] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  const toggleReports = async () => {
    const next = !showReports
    setShowReports(next)
    if (next) { try { setReports(await loadReports(participant.id)) } catch { setReports([]) } }
  }
  const submitReport = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault(); setBusy(true); setError('')
    try {
      await onFileReport(participant.id, reason, comment.trim())
      setComment(''); setShowForm(false)
      if (showReports) setReports(await loadReports(participant.id))
    } catch (requestError) { setError(requestError instanceof Error ? requestError.message : 'Не удалось отправить жалобу.') }
    finally { setBusy(false) }
  }
  const retract = async (reportId: number) => {
    setBusy(true); setError('')
    try { await onRetractReport(reportId); setReports(await loadReports(participant.id)) }
    catch (requestError) { setError(requestError instanceof Error ? requestError.message : 'Не удалось отозвать жалобу.') }
    finally { setBusy(false) }
  }

  return <article>
    <div>
      <b>{participant.displayName}</b>
      <span className={`participant-status status-${participant.status.toLowerCase()}`}>{organizerParticipationLabels[participant.status] ?? participant.status}</span>
      {participant.reportCount > 0 && <button type="button" className="report-badge" onClick={() => void toggleReports()} aria-expanded={showReports} title="Жалобы других организаторов">! {participant.reportCount}</button>}
      {participant.contact && <small>Контакт: {participant.contact}</small>}
      <small>Записался: {formatDate(participant.joinedAt)}</small>
    </div>
    {showReports && <div className="participant-reports">
      {reports === null ? <small>Загружаем…</small> : reports.length === 0 ? <small>Активных жалоб нет.</small> : reports.map(report => <div key={report.reportId} className="participant-report">
        <b>{reportReasonLabels[report.reason] ?? report.reason}</b>
        <p>{report.comment}</p>
        <small>{report.sourceActivityTitle} · {formatDate(report.createdAt)}{report.isMine ? ' · ваша' : ''}</small>
        {report.isMine && <button type="button" className="button ghost" disabled={busy} onClick={() => void retract(report.reportId)}>Отозвать</button>}
      </div>)}
    </div>}
    <div className="participant-remove">
      {activityPast && !participant.viewerHasReported && (showForm
        ? <form className="participant-report-form" onSubmit={submitReport}>
            <select value={reason} onChange={event => setReason(event.target.value)}>{reportReasonOrder.map(value => <option key={value} value={value}>{reportReasonLabels[value]}</option>)}</select>
            <textarea required maxLength={2000} value={comment} onChange={event => setComment(event.target.value)} placeholder="Что произошло" />
            <button className="button" disabled={busy || comment.trim().length === 0}>Отправить жалобу</button>
            <button type="button" className="button ghost" onClick={() => setShowForm(false)}>Отмена</button>
          </form>
        : <button type="button" className="button ghost" onClick={() => setShowForm(true)}>Пожаловаться</button>)}
      {activityPast && participant.viewerHasReported && <small>Вы оставили жалобу</small>}
      {!['Cancelled', 'Rejected'].includes(participant.status) && (confirmRemovalId === participant.id
        ? <><button type="button" className="button danger" disabled={participantLoading} onClick={() => void onRemove(participant.id)}>Подтвердить удаление</button><button type="button" className="button ghost" onClick={() => onConfirmRemoval(null)}>Не удалять</button></>
        : <button type="button" className="button ghost danger" onClick={() => onConfirmRemoval(participant.id)}>Удалить</button>)}
    </div>
    {error && <div className="form-error" role="alert">{error}</div>}
  </article>
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

const discoverySportSlugs = new Set(['football', 'basketball', 'running', 'volleyball', 'tennis', 'badminton', 'workout', 'hockey', 'adaptive-sport'])
const fallbackSports: Sport[] = [
  { id: 1, slug: 'football', name: 'Футбол' },
  { id: 2, slug: 'basketball', name: 'Баскетбол' },
  { id: 3, slug: 'running', name: 'Бег' },
  { id: 4, slug: 'volleyball', name: 'Волейбол' },
  { id: 5, slug: 'tennis', name: 'Теннис' },
  { id: 6, slug: 'badminton', name: 'Бадминтон' },
  { id: 7, slug: 'workout', name: 'Функциональные тренировки' },
  { id: 8, slug: 'hockey', name: 'Хоккей' },
  { id: 9, slug: 'adaptive-sport', name: 'Адаптивный спорт' }
]
type GameFormatOption = { value: string; label: string; display: string }
const gameFormatsBySport: Record<string, GameFormatOption[]> = {
  football: ['5×5', '6×6', '7×7', '8×8', '9×9', '10×10', '11×11'].map(value => ({ value, label: `${value} — включая вратаря`, display: value })),
  basketball: [
    { value: '3×3', label: '3×3 — полкорта', display: '3×3' },
    { value: '5×5', label: '5×5 — классический баскетбол', display: '5×5' }
  ],
  volleyball: [
    { value: '2×2', label: '2×2 — пляжный волейбол', display: '2×2 · пляжный' },
    { value: '6×6', label: '6×6 — классический волейбол', display: '6×6 · классический' }
  ],
  hockey: [
    { value: '3+1', label: '3+1 — 3 полевых и вратарь', display: '3+1' },
    { value: '5+1', label: '5+1 — 5 полевых и вратарь', display: '5+1' }
  ],
  tennis: [
    { value: '1×1', label: '1×1 — одиночный разряд', display: '1×1 · одиночный' },
    { value: '2×2', label: '2×2 — парный разряд', display: '2×2 · парный' }
  ],
  badminton: [
    { value: '1×1', label: '1×1 — одиночный разряд', display: '1×1 · одиночный' },
    { value: '2×2', label: '2×2 — парный разряд', display: '2×2 · парный' }
  ]
}

const formatGameFormat = (sportSlug: string, value?: string) => value
  ? gameFormatsBySport[sportSlug]?.find(option => option.value === value)?.display ?? value
  : ''

const yandexRouteUrl = (venue: Venue) => `https://yandex.ru/maps/?rtext=~${venue.latitude},${venue.longitude}&rtt=auto`
const calendarDataUrl = (activity: Activity) => {
  const stamp = (value: string) => new Date(value).toISOString().replace(/[-:]/g, '').replace(/\.\d{3}Z$/, 'Z')
  const escape = (value: string) => value.replace(/\\/g, '\\\\').replace(/\n/g, '\\n').replace(/,/g, '\\,').replace(/;/g, '\\;')
  const content = [
    'BEGIN:VCALENDAR', 'VERSION:2.0', 'PRODID:-//Kasanie//Sports Nearby//RU', 'BEGIN:VEVENT',
    `UID:activity-${activity.id}@kasanie`, `DTSTAMP:${stamp(new Date().toISOString())}`,
    `DTSTART:${stamp(activity.startAt)}`, `DTEND:${stamp(activity.endAt)}`,
    `SUMMARY:${escape(activity.title)}`, `DESCRIPTION:${escape(activity.description)}`,
    `LOCATION:${escape(`${activity.venue.city}, ${activity.venue.address}`)}`,
    'END:VEVENT', 'END:VCALENDAR'
  ].join('\r\n')
  return `data:text/calendar;charset=utf-8,${encodeURIComponent(content)}`
}

const formatIconFiles: Record<string, string> = {
  all: 'all-events', evening: 'evening-game', group: 'joint-training', coach: 'with-coach', team: 'find-team', tournament: 'tournaments'
}
function FormatIcon({ type }: { type: string }) {
  return <img src={`/brand/icons/${formatIconFiles[type] ?? 'all-events'}.webp`} alt="" width={46} height={46} loading="lazy" />
}

const formatDate = (value: string) => new Intl.DateTimeFormat('ru-RU', { weekday: 'short', day: 'numeric', month: 'long', hour: '2-digit', minute: '2-digit' }).format(new Date(value))
const formatPrice = (value: number) => value === 0 ? 'Бесплатно' : `${new Intl.NumberFormat('ru-RU').format(value)} ₽`
const pluralRu = (value: number, forms: [string, string, string]) => {
  const modulo100 = Math.abs(value) % 100
  const modulo10 = modulo100 % 10
  if (modulo100 > 10 && modulo100 < 20) return forms[2]
  if (modulo10 === 1) return forms[0]
  if (modulo10 >= 2 && modulo10 <= 4) return forms[1]
  return forms[2]
}
const localDateInputValue = (date: Date) => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
const today = () => localDateInputValue(new Date())
const nextWholeHour = () => {
  const date = new Date()
  date.setHours(date.getHours() + 1, 0, 0, 0)
  return { date: localDateInputValue(date), time: `${String(date.getHours()).padStart(2, '0')}:00` }
}
const escapeHtml = (value: string) => value.replace(/[&<>'"]/g, character => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' })[character] ?? character)

function loadYandexMaps() {
  if (window.ymaps) return Promise.resolve(window.ymaps)
  if (!yandexMapsApiKey) return Promise.reject(new Error('YANDEX_MAPS_KEY_MISSING'))
  if (yandexMapsPromise) return yandexMapsPromise
  yandexMapsPromise = new Promise<YandexMapsApi>((resolve, reject) => {
    const script = document.createElement('script')
    script.src = `https://api-maps.yandex.ru/2.1.77/?apikey=${encodeURIComponent(yandexMapsApiKey)}&lang=ru_RU&csp=true`
    script.async = true
    script.onload = () => {
      const api = window.ymaps
      if (!api) { reject(new Error('YANDEX_MAPS_LOAD_FAILED')); return }
      api.ready(() => resolve(api))
    }
    script.onerror = () => reject(new Error('YANDEX_MAPS_LOAD_FAILED'))
    document.head.appendChild(script)
  })
  return yandexMapsPromise
}

async function geocodeLocation(request: string | Coordinates) {
  const query = typeof request === 'string'
    ? `query=${encodeURIComponent(request)}`
    : `latitude=${encodeURIComponent(request[0])}&longitude=${encodeURIComponent(request[1])}`
  return api<GeocodedLocation>(`/api/public/geocode?${query}`)
}

function distanceKm(first: Coordinates, second: Coordinates) {
  const radians = (value: number) => value * Math.PI / 180
  const latitude = radians(second[0] - first[0])
  const longitude = radians(second[1] - first[1])
  const a = Math.sin(latitude / 2) ** 2 + Math.cos(radians(first[0])) * Math.cos(radians(second[0])) * Math.sin(longitude / 2) ** 2
  return 6371 * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a))
}

function PublicHeader() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [loggingOut, setLoggingOut] = useState(false)
  const [menuOpen, setMenuOpen] = useState(false)
  const accountRef = useRef<HTMLDivElement>(null)
  const isOrganizer = user?.roles.includes('Organizer') ?? false
  const role = primaryRole(user?.roles ?? [])
  const cabinetPath = roleHome[role] ?? '/my/activities'

  useEffect(() => { setMenuOpen(false) }, [location.pathname])
  useEffect(() => {
    if (!menuOpen) return
    const onPointerDown = (event: PointerEvent) => { if (!accountRef.current?.contains(event.target as Node)) setMenuOpen(false) }
    const onKeyDown = (event: KeyboardEvent) => { if (event.key === 'Escape') setMenuOpen(false) }
    document.addEventListener('pointerdown', onPointerDown)
    document.addEventListener('keydown', onKeyDown)
    return () => { document.removeEventListener('pointerdown', onPointerDown); document.removeEventListener('keydown', onKeyDown) }
  }, [menuOpen])

  const signOut = async () => {
    setLoggingOut(true)
    try { await logout(); navigate('/login', { replace: true }) }
    finally { setLoggingOut(false) }
  }

  const isDiscoveryHome = location.pathname === '/' || location.pathname === '/sports'

  return <header className="nearby-header"><Link className="brand" to="/" aria-label="Касание — главная"><span className="brand-emblem brand-emblem-outlined"><img src="/brand/kasanie-logo-official.png" alt="" /></span><span><strong>КАСАНИЕ</strong><small>спортивная платформа</small></span></Link><nav><Link to={isOrganizer ? '/organizer/activities' : '/register-organizer'}>Организаторам</Link>{isDiscoveryHome && <Link className="button nearby-develop-button" to="/join">Развиваться</Link>}{user
    ? <div className="nearby-account" ref={accountRef}>
        <button type="button" className="nearby-account-trigger" aria-label="Меню профиля" aria-haspopup="menu" aria-expanded={menuOpen} onClick={() => setMenuOpen(open => !open)}>
          <span className="nearby-account-avatar" aria-hidden>{user.email[0]?.toUpperCase() ?? '?'}</span>
          <span className="nearby-account-id"><small>{roleLabel(role)}</small><b>{user.email}</b></span>
          <span className="nearby-account-caret" aria-hidden>▾</span>
        </button>
        {menuOpen && <div className="nearby-account-menu" role="menu">
          <Link role="menuitem" to={cabinetPath}>Мой кабинет</Link>
          <Link role="menuitem" to="/my/activities">Мои активности</Link>
          <Link role="menuitem" to="/account/security">Безопасность</Link>
          <button type="button" role="menuitem" className="nearby-account-signout" disabled={loggingOut} onClick={() => void signOut()}>{loggingOut ? 'Выходим…' : 'Выйти'}</button>
        </div>}
      </div>
    : <Link className="button ghost" to="/login">Войти</Link>}</nav></header>
}

function YandexActivitiesMap({ items, compact = false, selectedActivityId, onSelect, onSearchArea, fallbackLatitude, fallbackLongitude }: {
  items: SearchItem[]
  compact?: boolean
  selectedActivityId?: number | null
  onSelect?: (activityId: number) => void
  onSearchArea?: (viewport: MapViewport) => void
  fallbackLatitude?: number
  fallbackLongitude?: number
}) {
  const mapId = `yandex-map-${useId().replace(/:/g, '')}`
  const [loadFailed, setLoadFailed] = useState(false)
  const [viewport, setViewport] = useState<MapViewport | null>(null)
  const placemarks = useRef<Map<number, YandexPlacemark>>(new Map())
  const selectRef = useRef(onSelect)
  useEffect(() => { selectRef.current = onSelect }, [onSelect])

  useEffect(() => {
    if (!yandexMapsApiKey) return
    let disposed = false
    let map: YandexMap | null = null
    const markerStore = placemarks.current
    void loadYandexMaps().then(ymaps => {
      if (disposed) return
      const first = items[0]?.activity.venue
      const center: Coordinates = first ? [first.latitude, first.longitude] : [fallbackLatitude ?? 55.751244, fallbackLongitude ?? 37.618423]
      map = new ymaps.Map(mapId, { center, zoom: items.length > 0 ? 12 : 10, controls: ['zoomControl', 'geolocationControl'] })
      markerStore.clear()
      items.forEach(({ activity }, index) => {
        const venue = activity.venue
        const placemark = new ymaps.Placemark(
          [venue.latitude, venue.longitude],
          {
            iconContent: String(index + 1),
            balloonContentHeader: escapeHtml(activity.title),
            balloonContentBody: `${escapeHtml(formatDate(activity.startAt))}<br>${escapeHtml(venue.address)}<br><a href="/activities/${encodeURIComponent(activity.slug)}">Открыть событие</a>`
          },
          { preset: 'islands#darkGreenStretchyIcon' }
        )
        placemark.events.add('click', () => selectRef.current?.(activity.id))
        markerStore.set(activity.id, placemark)
        map?.geoObjects.add(placemark)
      })
      const bounds = map.geoObjects.getBounds()
      if (bounds && items.length > 1) map.setBounds(bounds, { checkZoomRange: true, zoomMargin: 56 })
      if (onSearchArea) {
        const coordinates = items.map(item => [item.activity.venue.latitude, item.activity.venue.longitude] as Coordinates)
        const latitude = coordinates.reduce((sum, point) => sum + point[0], 0) / coordinates.length
        const longitude = coordinates.reduce((sum, point) => sum + point[1], 0) / coordinates.length
        const initialRadius = Math.max(1, Math.min(100, Math.ceil(Math.max(...coordinates.map(point => distanceKm([latitude, longitude], point)), 10))))
        setViewport({ latitude, longitude, radiusKm: initialRadius })
        map.events.add('boundschange', event => {
        const center = event.get<Coordinates>('newCenter') ?? map?.getCenter()
        const nextBounds = event.get<number[][]>('newBounds') ?? map?.getBounds()
        if (!center || !nextBounds?.[1]) return
        setViewport({ latitude: center[0], longitude: center[1], radiusKm: Math.min(100, Math.max(1, Math.ceil(distanceKm(center, nextBounds[1] as Coordinates)))) })
        })
      }
    }).catch(() => setLoadFailed(true))
    return () => { disposed = true; markerStore.clear(); map?.destroy() }
  }, [items, mapId, onSearchArea, fallbackLatitude, fallbackLongitude])

  useEffect(() => {
    placemarks.current.forEach((placemark, activityId) => placemark.options.set('preset', activityId === selectedActivityId ? 'islands#redStretchyIcon' : 'islands#darkGreenStretchyIcon'))
  }, [selectedActivityId])

  const showSetup = !yandexMapsApiKey || loadFailed
  return <div className={`nearby-map${compact ? ' compact' : ''}`} aria-label="Карта найденных занятий">
    <div id={mapId} className="yandex-map-canvas" />
    {showSetup && <div className="map-setup"><div className="map-fallback-grid" aria-hidden="true"><i /><i /><i /></div><b>Яндекс Карта</b><span>Здесь будут показаны места игр и тренировок.</span></div>}
    {!compact && onSearchArea && viewport && <button type="button" className="map-area-search" onClick={() => onSearchArea(viewport)}>Искать в этой области</button>}
  </div>
}

function YandexLocationPicker({ value, addressQuery, onChange, onResolved }: {
  value: Coordinates | null
  addressQuery: string
  onChange: (coordinates: Coordinates) => void
  onResolved: (location: GeocodedLocation) => void
}) {
  const mapId = `yandex-picker-${useId().replace(/:/g, '')}`
  const [loadFailed, setLoadFailed] = useState(false)
  const [query, setQuery] = useState(addressQuery)
  const [lookupMessage, setLookupMessage] = useState('')
  const [resolving, setResolving] = useState(false)
  const placeRef = useRef<((coordinates: Coordinates) => void) | null>(null)
  const mapRef = useRef<YandexMap | null>(null)
  const changeRef = useRef(onChange)
  const resolvedRef = useRef(onResolved)
  useEffect(() => { changeRef.current = onChange; resolvedRef.current = onResolved }, [onChange, onResolved])
  useEffect(() => { if (addressQuery.trim()) setQuery(addressQuery) }, [addressQuery])

  useEffect(() => {
    if (!yandexMapsApiKey) return
    let disposed = false
    let map: YandexMap | null = null
    void loadYandexMaps().then(ymaps => {
      if (disposed) return
      const initial: Coordinates = value ?? [55.7963, 49.1064]
      map = new ymaps.Map(mapId, { center: initial, zoom: 12, controls: ['zoomControl', 'geolocationControl', 'searchControl'] })
      mapRef.current = map
      const place = (coordinates: Coordinates) => {
        map?.geoObjects.removeAll()
        const placemark = new ymaps.Placemark(coordinates, { iconCaption: 'Точка встречи' }, { preset: 'islands#darkGreenDotIconWithCaption', draggable: true })
        placemark.events.add('dragend', () => {
          const next = placemark.geometry.getCoordinates()
          changeRef.current(next)
          void geocodeLocation(next).then(location => resolvedRef.current(location)).catch(() => undefined)
        })
        map?.geoObjects.add(placemark)
      }
      placeRef.current = place
      if (value) place(value)
      map.events.add('click', event => {
        const coordinates = event.get<Coordinates>('coords')
        place(coordinates)
        changeRef.current(coordinates)
        void geocodeLocation(coordinates).then(location => resolvedRef.current(location)).catch(() => undefined)
      })
    }).catch(() => setLoadFailed(true))
    return () => { disposed = true; placeRef.current = null; mapRef.current = null; map?.destroy() }
  }, [mapId, value])

  const findAddress = async () => {
    if (!query.trim()) return setLookupMessage('Введите город и адрес площадки.')
    setResolving(true); setLookupMessage('')
    try {
      const location = await geocodeLocation(query.trim())
      placeRef.current?.(location.coordinates)
      mapRef.current?.setCenter(location.coordinates, 16, { duration: 300 })
      onChange(location.coordinates)
      onResolved(location)
      setLookupMessage('Адрес найден. Проверьте положение метки.')
    } catch (error) { setLookupMessage(error instanceof Error ? error.message : 'Не удалось найти адрес.') }
    finally { setResolving(false) }
  }

  if (!yandexMapsApiKey || loadFailed) return <div className="coordinate-fallback"><p>Для выбора точки на карте добавьте ключ Яндекс Карт. Пока координаты можно указать вручную.</p><div><label>Широта<input type="number" step="0.000001" value={value?.[0] ?? ''} onChange={event => onChange([Number(event.target.value), value?.[1] ?? 49.1064])} /></label><label>Долгота<input type="number" step="0.000001" value={value?.[1] ?? ''} onChange={event => onChange([value?.[0] ?? 55.7963, Number(event.target.value)])} /></label></div></div>
  return <div className="location-picker-wrap"><div className="location-search"><label>Найти адрес на карте<input value={query} onChange={event => setQuery(event.target.value)} placeholder="Казань, ул. Московская, 1" /></label><button type="button" className="button ghost" disabled={resolving} onClick={() => void findAddress()}>{resolving ? 'Ищем…' : 'Найти адрес'}</button></div>{lookupMessage && <small className="location-message">{lookupMessage}</small>}<div className="location-picker"><div id={mapId} className="yandex-map-canvas" /><span>{value ? `${value[0].toFixed(6)}, ${value[1].toFixed(6)}` : 'Найдите адрес или нажмите на карту'}</span></div></div>
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

type SavedFilter = { id: string; name: string; query: string }
const SAVED_FILTERS_KEY = 'kasanie:sports:saved-filters'
const RECENT_ACTIVITIES_KEY = 'kasanie:sports:recent-activities'

function readSavedFilters(): SavedFilter[] {
  try {
    const raw = localStorage.getItem(SAVED_FILTERS_KEY)
    const parsed = raw ? JSON.parse(raw) : []
    return Array.isArray(parsed)
      ? parsed.filter((x): x is SavedFilter => x && typeof x.id === 'string' && typeof x.name === 'string' && typeof x.query === 'string')
      : []
  } catch { return [] }
}
function writeSavedFilters(items: SavedFilter[]) {
  try { localStorage.setItem(SAVED_FILTERS_KEY, JSON.stringify(items.slice(0, 12))) } catch { /* storage unavailable */ }
}

function readRecentActivityIds(): number[] {
  try {
    const parsed = JSON.parse(localStorage.getItem(RECENT_ACTIVITIES_KEY) ?? '[]')
    return Array.isArray(parsed) ? parsed.filter((value): value is number => Number.isInteger(value)).slice(0, 12) : []
  } catch { return [] }
}

function rememberActivityId(activityId: number) {
  try { localStorage.setItem(RECENT_ACTIVITIES_KEY, JSON.stringify([activityId, ...readRecentActivityIds().filter(id => id !== activityId)].slice(0, 12))) } catch { /* storage unavailable */ }
}

function SavedFilters({ query, onApply }: { query: string; onApply: (query: string) => void }) {
  const [items, setItems] = useState<SavedFilter[]>(() => readSavedFilters())
  const [naming, setNaming] = useState(false)
  const [name, setName] = useState('')

  const persist = (next: SavedFilter[]) => { const capped = next.slice(0, 12); setItems(capped); writeSavedFilters(capped) }
  const alreadySaved = items.some(item => item.query === query)
  const canSave = query.length > 0 && !alreadySaved

  const save = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const trimmed = name.trim().slice(0, 40)
    if (!trimmed || !canSave) return
    persist([{ id: `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`, name: trimmed, query }, ...items])
    setName(''); setNaming(false)
  }

  if (items.length === 0 && query.length === 0) return null
  return <div className="saved-filters">
    <span className="saved-filters-label">Сохранённые фильтры</span>
    <div className="saved-filters-chips">
      {items.map(item => <span key={item.id} className="saved-filter-chip">
        <button type="button" onClick={() => onApply(item.query)}>{item.name}</button>
        <button type="button" className="saved-filter-remove" aria-label={`Удалить набор «${item.name}»`} onClick={() => persist(items.filter(x => x.id !== item.id))}>✕</button>
      </span>)}
      {naming
        ? <form className="saved-filter-name" onSubmit={save}>
            <input autoFocus maxLength={40} value={name} onChange={event => setName(event.target.value)} placeholder="Название набора" aria-label="Название набора фильтров" />
            <button type="submit" className="button" disabled={name.trim().length === 0}>Сохранить</button>
            <button type="button" className="button ghost" onClick={() => { setNaming(false); setName('') }}>Отмена</button>
          </form>
        : <button type="button" className="saved-filter-add" disabled={!canSave} onClick={() => setNaming(true)}>+ Сохранить фильтр</button>}
    </div>
  </div>
}

export function SportsNearbyPage() {
  const [params, setParams] = useSearchParams()
  const [result, setResult] = useState<SearchResult | null>(null)
  const [sports, setSports] = useState<Sport[]>([])
  const [error, setError] = useState('')
  const [geoError, setGeoError] = useState('')
  const [showLocationHelp, setShowLocationHelp] = useState(false)
  const [locating, setLocating] = useState(false)
  const [selectedActivityId, setSelectedActivityId] = useState<number | null>(null)
  const [showMoreFilters, setShowMoreFilters] = useState(false)
  const [recentActivityIds] = useState(readRecentActivityIds)
  const cityParam = params.get('city') ?? ''
  const [selectedCity, setSelectedCity] = useState(cityParam)
  const sportParam = params.get('sport') ?? ''
  const [selectedSearchSport, setSelectedSearchSport] = useState(sportParam)
  const [defaultSearchDateTime] = useState(nextWholeHour)
  const query = params.toString()

  useEffect(() => { void api<Sport[]>('/api/public/sports').then(setSports).catch(() => setSports(fallbackSports)) }, [])
  useEffect(() => {
    setError('')
    void api<SearchResult>(`/api/public/activities${query ? `?${query}` : ''}`).then(setResult).catch(error => {
      setError(error instanceof ApiError && error.status === 404
        ? 'Поиск активностей временно недоступен. Попробуйте обновить страницу позже.'
        : error instanceof Error ? error.message : 'Не удалось загрузить события.')
    })
  }, [query])
  useEffect(() => setSelectedCity(cityParam), [cityParam])
  useEffect(() => setSelectedSearchSport(sportParam), [sportParam])
  useEffect(() => {
    if (params.has('latitude') || params.get('district') || params.get('time') || params.get('gameFormat') || params.get('availableOnly') === 'true' || params.get('freeOnly') === 'true') setShowMoreFilters(true)
  }, [query, params])

  const submit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    const next = new URLSearchParams(params)
    for (const key of ['city', 'sport', 'date', 'district', 'time', 'gameFormat']) {
      if (!form.has(key)) continue
      const value = String(form.get(key) ?? '').trim()
      if (value) next.set(key, value)
      else next.delete(key)
    }
    for (const key of ['freeOnly', 'availableOnly']) {
      if (!form.has(key)) continue
      if (form.get(key)) next.set(key, 'true')
      else next.delete(key)
    }
    const latitude = params.get('latitude'); const longitude = params.get('longitude')
    if (next.get('city')) {
      next.delete('latitude'); next.delete('longitude'); next.delete('radiusKm')
    } else if (latitude && longitude) {
      next.set('latitude', latitude); next.set('longitude', longitude)
      if (form.has('radiusKm')) next.set('radiusKm', String(form.get('radiusKm') ?? '10'))
    }
    setParams(next)
  }

  const locateCurrentPosition = () => {
    setGeoError('')
    setShowLocationHelp(false)
    if (!navigator.geolocation) return setGeoError('Этот браузер не поддерживает определение местоположения.')
    setLocating(true)
    navigator.geolocation.getCurrentPosition(position => {
      const next = new URLSearchParams(params)
      next.delete('city'); next.delete('district')
      next.set('latitude', position.coords.latitude.toFixed(6)); next.set('longitude', position.coords.longitude.toFixed(6))
      next.set('radiusKm', next.get('radiusKm') ?? '10')
      setSelectedCity(''); setParams(next); setLocating(false)
    }, geolocationError => {
      setGeoError(geolocationError.code === geolocationError.PERMISSION_DENIED
        ? 'Не удалось определить местоположение.'
        : 'Не удалось определить местоположение. Попробуйте ещё раз.')
      setShowLocationHelp(geolocationError.code === geolocationError.PERMISSION_DENIED)
      setLocating(false)
    }, { enableHighAccuracy: true, timeout: 10000 })
  }

  const requestLocationPermission = async () => {
    if (!navigator.permissions) return locateCurrentPosition()
    try {
      const permission = await navigator.permissions.query({ name: 'geolocation' })
      if (permission.state === 'denied') {
        setShowLocationHelp(true)
        return
      }
    } catch {
      return locateCurrentPosition()
    }
    locateCurrentPosition()
  }

  const clearCurrentLocation = () => {
    const next = new URLSearchParams(params)
    next.delete('latitude'); next.delete('longitude'); next.delete('radiusKm')
    setParams(next); setGeoError('')
  }

  const searchMapArea = useCallback((viewport: MapViewport) => {
    const next = new URLSearchParams(params)
    next.delete('city'); next.delete('district')
    next.set('latitude', viewport.latitude.toFixed(6)); next.set('longitude', viewport.longitude.toFixed(6)); next.set('radiusKm', String(viewport.radiusKm))
    setSelectedCity(''); setParams(next)
  }, [params, setParams])

  const selectFromMap = useCallback((activityId: number) => {
    setSelectedActivityId(activityId)
    document.getElementById(`activity-card-${activityId}`)?.scrollIntoView({ behavior: 'smooth', block: 'center' })
  }, [])

  const mapItems = useMemo(() => result?.items ?? [], [result])
  const visibleSports = useMemo(() => sports.filter(sport => discoverySportSlugs.has(sport.slug)), [sports])
  const availableSearchGameFormats = gameFormatsBySport[selectedSearchSport] ?? []
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
    return `/${next.size ? `?${next}` : ''}`
  }
  const districtOptions = russianCityDistricts[selectedCity.trim()] ?? []
  const selectedSportName = sports.find(sport => sport.slug === params.get('sport'))?.name ?? 'Все виды спорта'
  const changeSort = (value: string) => {
    const next = new URLSearchParams(params)
    if (value && value !== 'recommended') next.set('sort', value); else next.delete('sort')
    setParams(next)
  }
  const applySavedFilter = useCallback((saved: string) => setParams(new URLSearchParams(saved)), [setParams])
  const isBrowseHome = params.size === 0
  const browseItems = result?.items ?? []
  const recentlyViewed = recentActivityIds.map(id => browseItems.find(item => item.activity.id === id)).filter((item): item is SearchItem => Boolean(item))
  const cityShelves = Array.from(browseItems.reduce((groups, item) => {
    const city = item.activity.venue.city
    groups.set(city, [...(groups.get(city) ?? []), item])
    return groups
  }, new Map<string, SearchItem[]>()))

  return <div className="nearby-page search-discovery"><PublicHeader /><main>
    <section className="nearby-hero">
      <form key={`${query}-${sports.length}`} className={`nearby-search${showMoreFilters ? ' is-expanded' : ''}`} onSubmit={submit}>
        <div className="nearby-search-primary">
          <label><b>Город</b><CityInput name="city" defaultValue={params.get('city') ?? ''} onValueChange={setSelectedCity} /></label>
          <label><b>Спорт</b><select name="sport" value={selectedSearchSport} onChange={event => setSelectedSearchSport(event.target.value)}><option value="">Все виды спорта</option>{visibleSports.map(sport => <option key={sport.id} value={sport.slug}>{sport.name}</option>)}</select></label>
          <label><b>Дата</b><input name="date" type="date" min={today()} defaultValue={params.get('date') ?? defaultSearchDateTime.date} /></label>
          <button type="button" className="nearby-more-toggle" aria-expanded={showMoreFilters} onClick={() => setShowMoreFilters(value => !value)}><span aria-hidden>☷</span>{showMoreFilters ? 'Скрыть' : 'Фильтры'} <i aria-hidden>{showMoreFilters ? '▴' : '▾'}</i></button>
          <button type="button" className={`nearby-geo-button${params.has('latitude') ? ' active' : ''}`} disabled={locating} onClick={params.has('latitude') ? clearCurrentLocation : locateCurrentPosition}><span aria-hidden>⌖</span>{locating ? 'Определяем…' : params.has('latitude') ? 'Рядом (сбросить)' : 'Рядом со мной'}</button>
          <button className="nearby-search-button" aria-label="Найти события"><span className="nearby-search-icon" aria-hidden /><b>Найти</b></button>
        </div>
        {showMoreFilters && <div className="nearby-search-more">
          <label><b>Район</b><input name="district" list="nearby-districts" defaultValue={params.get('district') ?? ''} placeholder={districtOptions.length ? 'Выберите район' : 'Любой район'} /><datalist id="nearby-districts">{districtOptions.map(district => <option key={district} value={district} />)}</datalist></label>
          <label><b>Время</b><input name="time" type="time" step="3600" defaultValue={params.get('time') ?? defaultSearchDateTime.time} /></label>
          <label><b>Формат игры</b><select key={selectedSearchSport} name="gameFormat" defaultValue={availableSearchGameFormats.some(option => option.value === params.get('gameFormat')) ? params.get('gameFormat') ?? '' : ''}><option value="">Любой формат</option>{availableSearchGameFormats.map(format => <option key={format.value} value={format.value}>{format.label}</option>)}</select></label>
          <div className="nearby-checks"><label><input name="availableOnly" type="checkbox" defaultChecked={params.get('availableOnly') === 'true'} /> Есть места</label><label><input name="freeOnly" type="checkbox" defaultChecked={params.get('freeOnly') === 'true'} /> Бесплатно</label><label className="radius-control">Радиус<select name="radiusKm" defaultValue={params.get('radiusKm') ?? '10'}><option value="1">1 км</option><option value="3">3 км</option><option value="5">5 км</option><option value="10">10 км</option><option value="25">25 км</option></select></label></div>
        </div>}
      </form>
      <SavedFilters query={query} onApply={applySavedFilter} />
      {params.has('latitude') && <p className="nearby-location-status">Показываем активности в радиусе {params.get('radiusKm') ?? '10'} км от выбранной точки.</p>}
      {geoError && <div className="form-error nearby-geo-error" role="alert">
        <span>{geoError}</span>{' '}
        <button type="button" className="nearby-permission-link" onClick={() => void requestLocationPermission()}>Разрешить доступ к геопозиции</button>
        {showLocationHelp && <p className="nearby-permission-help">Браузер не разрешает сайту открывать системные настройки напрямую. Нажмите значок замка слева от адреса сайта, выберите «Геоданные» → «Разрешить», затем обновите страницу.</p>}
      </div>}
      <nav className="nearby-categories" aria-label="Быстрый выбор формата">{formatLinks.map(item => <Link key={item.label} className={`format-${item.icon} ${(params.get('type') ?? '') === item.value ? 'active' : ''}`} to={linkForFormat(item.value)}><span className="format-icon"><FormatIcon type={item.icon} /></span><b>{item.label}</b></Link>)}</nav>
    </section>
    {isBrowseHome ? <section className="discovery-shelves" aria-label="Подборки активностей">
      {error && <div className="discovery-home-empty" role="alert"><h2>Не удалось загрузить подборки</h2><p>Проверьте соединение и попробуйте обновить страницу.</p><button className="button" type="button" onClick={() => window.location.reload()}>Обновить</button></div>}
      {!result && !error && <div className="discovery-shelf-loading">Подбираем активности…</div>}
      {recentlyViewed.length > 0 && <DiscoveryShelf title="Вы недавно смотрели" items={recentlyViewed} />}
      {browseItems.length > 0 && <DiscoveryShelf title="Актуальные события" items={browseItems.slice(0, 12)} />}
      {cityShelves.map(([city, items]) => <DiscoveryShelf key={city} title={`Активности: ${city}`} items={items} />)}
      {result?.total === 0 && <div className="discovery-home-empty"><h2>События скоро появятся</h2><p>Станьте первым организатором в своём городе или выберите другой город в поиске.</p><Link className="button" to="/register-organizer">Создать событие</Link></div>}
    </section> : <section className="nearby-results"><div className="nearby-results-head"><div><span className="eyebrow">{params.get('sport') ? `${selectedSportName} рядом` : 'Активности рядом'}</span><h2>Доступные активности</h2><p>{result ? `${result.total} ${pluralRu(result.total, ['вариант', 'варианта', 'вариантов'])} по выбранным фильтрам` : 'Подбираем варианты рядом'}</p></div><div className="nearby-results-actions"><label className="nearby-sort">Сначала<select aria-label="Сортировка результатов" value={params.get('sort') ?? 'recommended'} onChange={event => changeSort(event.target.value)}><option value="recommended">Рекомендованные</option><option value="distance" disabled={!params.has('latitude')}>Ближайшие</option><option value="date">По дате</option><option value="availability">Больше свободных мест</option><option value="price">Сначала дешевле</option></select></label><Link className="button" to="/register-organizer">Создать событие</Link></div></div>
      {error && <div className="form-error" role="alert">{error}</div>}
      <div className="nearby-results-grid"><div className="activity-list">{result?.items.map(({ activity, distanceKm }) => <ActivityCard key={activity.id} activity={activity} distanceKm={distanceKm} selected={selectedActivityId === activity.id} onSelect={setSelectedActivityId} />)}{result && result.total === 0 && <div className="nearby-empty"><span aria-hidden>⌕</span><b>Пока ничего не найдено</b><p>Измените дату или формат — либо создайте активность, которой не хватает рядом с вами.</p><div>{query && <Link className="button ghost" to="/">Сбросить фильтры</Link>}<Link className="button" to="/register-organizer">Создать событие</Link></div></div>}</div><YandexActivitiesMap items={mapItems} selectedActivityId={selectedActivityId} onSelect={selectFromMap} onSearchArea={searchMapArea} fallbackLatitude={params.has('latitude') ? Number(params.get('latitude')) : undefined} fallbackLongitude={params.has('longitude') ? Number(params.get('longitude')) : undefined} /></div>
    </section>}
    {isBrowseHome && <section className="nearby-services" aria-labelledby="nearby-services-title">
      <div className="nearby-services-head"><span className="eyebrow">Больше, чем поиск событий</span><h2 id="nearby-services-title">Развивайтесь в своём ритме</h2><p>От первой тренировки до управления командой — нужный маршрут уже внутри «Касания».</p></div>
      <div className="nearby-service-grid">
        <Link className="nearby-service-card service-team" to="/register-coach"><span className="nearby-service-copy"><small>Для тренеров и команд</small><strong>Управляйте командой</strong><p>Состав, расписание, тренировки и обратная связь в одном кабинете.</p><b>Открыть кабинет <span aria-hidden>→</span></b></span><img src="/brand/home-reference/coach.webp" alt="" loading="lazy" /></Link>
        <Link className="nearby-service-card service-player" to="/register"><span className="nearby-service-copy"><small>Для игроков</small><strong>Тренируйтесь индивидуально</strong><p>Персональный план, цели и понятная динамика прогресса.</p><b>Начать развиваться <span aria-hidden>→</span></b></span><img src="/brand/home-reference/player.webp" alt="" loading="lazy" /></Link>
        <Link className="nearby-service-card service-parent" to="/register-parent"><span className="nearby-service-copy"><small>Для родителей</small><strong>Будьте рядом с ребёнком</strong><p>Расписание, достижения и связь с тренером без лишних чатов.</p><b>Кабинет родителя <span aria-hidden>→</span></b></span><img src="/brand/home-reference/child.webp" alt="" loading="lazy" /></Link>
      </div>
      <div className="nearby-organizer-cta"><div><span className="eyebrow">Для организаторов</span><h2>Проводите игры и тренировки?</h2><p>Добавьте событие — участники найдут его через поиск и карту.</p></div><Link className="button" to="/register-organizer">Создать событие</Link></div>
    </section>}
  </main><footer className="nearby-footer"><div className="nearby-footer-main"><Link className="brand" to="/" aria-label="Касание — главная"><span className="brand-emblem brand-emblem-outlined"><img src="/brand/kasanie-logo-official.png" alt="" /></span><span><strong>КАСАНИЕ</strong><small>спорт рядом и развитие</small></span></Link><nav aria-label="Разделы сайта"><div><b>Активности</b><Link to="/">Найти рядом</Link><Link to="/register-organizer">Создать событие</Link></div><div><b>Развитие</b><Link to="/join">Выбрать роль</Link><Link to="/register-coach">Для тренеров</Link><Link to="/register-parent">Для родителей</Link></div><div><b>Аккаунт</b><Link to="/login">Войти</Link><Link to="/my/activities">Мои активности</Link></div></nav></div><div className="nearby-footer-bottom"><span>© {new Date().getFullYear()} Касание</span><span>Спортивная платформа для людей и команд</span></div></footer></div>
}

function DiscoveryShelf({ title, items }: { title: string; items: SearchItem[] }) {
  return <section className="discovery-shelf"><div className="discovery-shelf-head"><h2>{title}</h2><span>{items.length} {pluralRu(items.length, ['вариант', 'варианта', 'вариантов'])}</span></div><div className="discovery-shelf-track">{items.map(({ activity, distanceKm }) => <DiscoveryActivityCard key={activity.id} activity={activity} distanceKm={distanceKm} />)}</div></section>
}

const discoveryCoverByEvent: Record<string, string> = {
  Game: '/brand/home-reference/nearby-v2.png',
  GroupTraining: '/brand/home-reference/kids.webp',
  CoachTraining: '/brand/home-reference/coach-v2.png',
  OpenTeamTraining: '/brand/home-reference/board.webp',
  PlayerRecruitment: '/brand/home-reference/player-v2.png',
  Tournament: '/brand/home-reference/trophy.webp'
}

function DiscoveryActivityCard({ activity, distanceKm }: { activity: Activity; distanceKm?: number }) {
  const date = new Intl.DateTimeFormat('ru-RU', { weekday: 'short', day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' }).format(new Date(activity.startAt))
  return <Link className="discovery-activity-card" to={`/activities/${activity.slug}`} onClick={() => rememberActivityId(activity.id)}><span className={`discovery-activity-cover activity-${activity.eventType.toLowerCase()}`}><img src={discoveryCoverByEvent[activity.eventType] ?? '/brand/home-reference/ball.webp'} alt="" loading="lazy" /><b>{eventLabels[activity.eventType] ?? activity.eventType}</b></span><span className="discovery-activity-copy"><span><strong>{activity.title}</strong><b>{formatPrice(activity.price)}</b></span><small>{date}</small><small>{activity.venue.city} · {activity.venue.name}{typeof distanceKm === 'number' ? ` · ${distanceKm} км` : ''}</small><small>{activity.sport}{activity.gameFormat ? ` · ${formatGameFormat(activity.sportSlug, activity.gameFormat)}` : ''}</small></span></Link>
}

function ActivityCard({ activity, distanceKm, selected, onSelect }: { activity: Activity; distanceKm?: number; selected?: boolean; onSelect?: (activityId: number) => void }) {
  const start = new Date(activity.startAt)
  const time = new Intl.DateTimeFormat('ru-RU', { hour: '2-digit', minute: '2-digit' }).format(start)
  const day = new Intl.DateTimeFormat('ru-RU', { day: 'numeric', month: 'short' }).format(start)
  return <Link id={`activity-card-${activity.id}`} className={`activity-card${selected ? ' selected' : ''}`} to={`/activities/${activity.slug}`} onMouseEnter={() => onSelect?.(activity.id)} onFocus={() => onSelect?.(activity.id)}>
    <div className={`activity-card-cover activity-${activity.eventType.toLowerCase()}`}><span className="activity-card-badge">{eventLabels[activity.eventType] ?? activity.eventType}</span><span className="activity-card-time"><b>{time}</b><small>{day}</small></span><i aria-hidden="true" /></div>
    <div className="activity-card-body"><div className="activity-card-top"><span>{activity.venue.district || activity.venue.city}</span><b>{formatPrice(activity.price)}</b></div><h3>{activity.title}</h3><p>{activity.description}</p><div className="activity-meta"><span>⌖ {activity.venue.city}{activity.venue.district ? ` · ${activity.venue.district}` : ''}{typeof distanceKm === 'number' ? ` · ${distanceKm} км` : ''}</span><span>◫ {activity.sport}{activity.gameFormat ? ` · ${formatGameFormat(activity.sportSlug, activity.gameFormat)}` : ''}</span><span>◎ {activity.skillLevel}</span><span>Организатор: {activity.organizerName}</span></div><div className="activity-card-footer"><span className={activity.availablePlaces > 0 ? 'places-ok' : 'places-full'}>{activity.availablePlaces > 0 ? `${activity.availablePlaces} ${pluralRu(activity.availablePlaces, ['место свободно', 'места свободно', 'мест свободно'])}` : 'Мест нет'}</span><span>Подробнее →</span></div></div>
  </Link>
}

const localDateTime = (hoursFromNow: number) => {
  const date = new Date(Date.now() + hoursFromNow * 60 * 60 * 1000)
  date.setMinutes(date.getMinutes() - date.getTimezoneOffset())
  return date.toISOString().slice(0, 16)
}

const dateTimeInputValue = (value: string) => {
  const date = new Date(value)
  date.setMinutes(date.getMinutes() - date.getTimezoneOffset())
  return date.toISOString().slice(0, 16)
}

// Тот же день недели и время, но ближайшая дата в будущем (шаг — неделя).
const repeatedStartAt = (value: string) => {
  const date = new Date(value)
  const now = Date.now()
  while (date.getTime() <= now) date.setDate(date.getDate() + 7)
  date.setMinutes(date.getMinutes() - date.getTimezoneOffset())
  return date.toISOString().slice(0, 16)
}

const eventTypeValues: Record<string, number> = { Game: 0, GroupTraining: 1, CoachTraining: 2, OpenTeamTraining: 3, TrainingPartner: 4, PlayerRecruitment: 5, RecurringGroup: 6, Tournament: 7, Trial: 8, OpenPractice: 9 }
type VenueDraft = { name: string; city: string; district: string; address: string; region: string; indoor: boolean }
const emptyVenueDraft: VenueDraft = { name: '', city: '', district: '', address: '', region: '', indoor: false }

export function OrganizerActivitiesPage() {
  const [activities, setActivities] = useState<Activity[]>([])
  const [sports, setSports] = useState<Sport[]>([])
  const [selectedSportId, setSelectedSportId] = useState<number | null>(null)
  const [venues, setVenues] = useState<Venue[]>([])
  const [venueChoice, setVenueChoice] = useState('')
  const [meetingPoint, setMeetingPoint] = useState<Coordinates | null>(null)
  const [venueDraft, setVenueDraft] = useState<VenueDraft>(emptyVenueDraft)
  const [editing, setEditing] = useState<Activity | null>(null)
  const [template, setTemplate] = useState<Activity | null>(null)
  const [formVersion, setFormVersion] = useState(0)
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null)
  const [pending, setPending] = useState(false)
  const [participantActivity, setParticipantActivity] = useState<Activity | null>(null)
  const [participants, setParticipants] = useState<OrganizerParticipants | null>(null)
  const [participantLoading, setParticipantLoading] = useState(false)
  const [confirmRemovalId, setConfirmRemovalId] = useState<number | null>(null)
  const [confirmDeleteId, setConfirmDeleteId] = useState<number | null>(null)
  const [view, setView] = useState<'list' | 'form' | 'participants'>('list')
  const [nowMs] = useState(() => Date.now())
  const reload = async () => setActivities(await api<Activity[]>('/api/organizer/activities/'))

  useEffect(() => {
    void Promise.all([api<Sport[]>('/api/public/sports'), api<Venue[]>('/api/public/venues'), api<Activity[]>('/api/organizer/activities/')]).then(([sportItems, venueItems, activityItems]) => {
      const supportedSports = sportItems.filter(sport => discoverySportSlugs.has(sport.slug))
      setSports(supportedSports); setSelectedSportId(supportedSports[0]?.id ?? null); setVenues(venueItems); setActivities(activityItems); setVenueChoice(venueItems[0]?.id.toString() ?? 'new-map')
    }).catch(error => setMessage({ text: error instanceof Error ? error.message : 'Не удалось загрузить кабинет организатора.', ok: false }))
  }, [])

  const resolveVenue = useCallback((location: GeocodedLocation) => {
    setMeetingPoint(location.coordinates)
    setVenueDraft(current => ({ ...current, address: location.address || current.address, city: location.city || current.city, district: location.district || current.district, region: location.region || current.region }))
  }, [])

  const clearEditor = () => {
    setEditing(null); setTemplate(null); setSelectedSportId(sports[0]?.id ?? null); setMeetingPoint(null); setVenueDraft(emptyVenueDraft); setVenueChoice(venues[0]?.id.toString() ?? 'new-map'); setFormVersion(value => value + 1)
  }

  const startCreating = () => { clearEditor(); setMessage(null); setConfirmDeleteId(null); setView('form') }

  const backToList = () => { clearEditor(); setMessage(null); setView('list') }

  const startEditing = (activity: Activity) => {
    setEditing(activity); setTemplate(null); setSelectedSportId(sports.find(sport => sport.slug === activity.sportSlug)?.id ?? null); setVenueChoice(activity.venue.id.toString()); setMeetingPoint(null); setVenueDraft(emptyVenueDraft); setMessage(null); setFormVersion(value => value + 1); setView('form')
  }

  const startRepeating = (activity: Activity) => {
    setEditing(null); setTemplate(activity); setSelectedSportId(sports.find(sport => sport.slug === activity.sportSlug)?.id ?? null); setVenueChoice(activity.venue.id.toString()); setMeetingPoint(null); setVenueDraft(emptyVenueDraft); setMessage(null); setConfirmDeleteId(null); setFormVersion(value => value + 1); setView('form')
  }

  const save = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const form = event.currentTarget
    const values = new FormData(form)
    setPending(true); setMessage(null)
    try {
      let venueId = Number(venueChoice)
      if (venueChoice === 'new-map' || venueChoice === 'new-text') {
        let coordinates = meetingPoint
        if (venueChoice === 'new-map' && !coordinates) throw new Error('Поставьте точку встречи на карте.')
        if (venueChoice === 'new-text') {
          const resolved = await geocodeLocation([venueDraft.city, venueDraft.address].filter(Boolean).join(', '))
          coordinates = resolved.coordinates
        }
        if (!coordinates) throw new Error('Не удалось определить координаты места.')
        const venue = await post<Venue>('/api/organizer/venues/', {
          name: venueDraft.name, city: venueDraft.city, district: venueDraft.district, address: venueDraft.address,
          latitude: coordinates[0], longitude: coordinates[1], indoor: venueDraft.indoor, region: venueDraft.region || null
        })
        venueId = venue.id
        setVenues(current => [...current, venue])
      }
      const startAt = new Date(String(values.get('startAt')))
      const payload = {
        sportId: Number(values.get('sportId')), venueId, eventType: Number(values.get('eventType')), gameFormat: values.get('gameFormat') ? String(values.get('gameFormat')) : null,
        title: String(values.get('title') ?? ''), description: String(values.get('description') ?? ''),
        startAt: startAt.toISOString(), endAt: new Date(startAt.getTime() + 2 * 60 * 60 * 1000).toISOString(),
        capacity: Number(values.get('capacity')), waitlistCapacity: Number(values.get('waitlistCapacity') ?? 0), price: Number(values.get('price')),
        skillLevel: String(values.get('skillLevel') ?? 'Любой'), minimumAge: Number(values.get('minimumAge')), maximumAge: values.get('maximumAge') ? Number(values.get('maximumAge')) : null,
        equipmentRequirements: null, rules: `Приходите за ${Number(values.get('arrivalMinutes') ?? 15)} минут до начала.`, cancellationPolicy: 'Сообщите об отмене заранее.', registrationDeadline: null,
        isRecurring: false, recurrenceRule: null, organizerParticipates: values.get('organizerParticipates') === 'on'
      }
      if (editing) await put(`/api/organizer/activities/${editing.id}`, payload)
      else {
        const created = await post<{ id: number }>('/api/organizer/activities/', payload)
        await post(`/api/organizer/activities/${created.id}/publish`)
      }
      form.reset(); setMessage({ text: editing ? 'Изменения сохранены.' : 'Активность опубликована.', ok: true }); clearEditor(); setView('list'); await reload()
    } catch (error) { setMessage({ text: error instanceof Error ? error.message : 'Не удалось сохранить событие.', ok: false }) } finally { setPending(false) }
  }

  const action = async (activity: Activity, value: 'publish' | 'cancel') => {
    setPending(true); setMessage(null)
    try { await post(`/api/organizer/activities/${activity.id}/${value}`); setMessage({ text: value === 'publish' ? 'Событие опубликовано.' : 'Событие отменено.', ok: true }); await reload() }
    catch (error) { setMessage({ text: error instanceof Error ? error.message : 'Операция не выполнена.', ok: false }) } finally { setPending(false) }
  }

  const deleteActivity = async (activity: Activity) => {
    setPending(true); setMessage(null)
    try {
      await remove(`/api/organizer/activities/${activity.id}`)
      if (editing?.id === activity.id) { clearEditor(); setView('list') }
      if (participantActivity?.id === activity.id) { setParticipantActivity(null); setParticipants(null); setView('list') }
      setConfirmDeleteId(null); setMessage({ text: 'Активность удалена из кабинета.', ok: true }); await reload()
    } catch (error) { setMessage({ text: error instanceof Error ? error.message : 'Не удалось удалить активность.', ok: false }) }
    finally { setPending(false) }
  }

  const openParticipants = async (activity: Activity) => {
    setParticipantActivity(activity); setParticipantLoading(true); setConfirmRemovalId(null); setMessage(null); setView('participants')
    try { setParticipants(await api<OrganizerParticipants>(`/api/organizer/activities/${activity.id}/participants`)) }
    catch (error) { setParticipants(null); setMessage({ text: error instanceof Error ? error.message : 'Не удалось загрузить участников.', ok: false }) }
    finally { setParticipantLoading(false) }
  }

  const removeParticipant = async (participantId: number) => {
    if (!participantActivity) return
    setParticipantLoading(true); setMessage(null)
    try {
      await remove(`/api/organizer/activities/${participantActivity.id}/participants/${participantId}`)
      setParticipants(await api<OrganizerParticipants>(`/api/organizer/activities/${participantActivity.id}/participants`))
      setMessage({ text: 'Участник удалён. Место снова доступно.', ok: true }); setConfirmRemovalId(null); await reload()
    } catch (error) { setMessage({ text: error instanceof Error ? error.message : 'Не удалось удалить участника.', ok: false }) }
    finally { setParticipantLoading(false) }
  }

  const refreshParticipants = async () => {
    if (!participantActivity) return
    setParticipants(await api<OrganizerParticipants>(`/api/organizer/activities/${participantActivity.id}/participants`))
  }

  const addParticipant = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!participantActivity) return
    const form = event.currentTarget
    const values = new FormData(form)
    setParticipantLoading(true); setMessage(null)
    try {
      await post(`/api/organizer/activities/${participantActivity.id}/participants`, { name: values.get('name'), contact: values.get('contact') || null })
      form.reset()
      await refreshParticipants()
      setMessage({ text: 'Участник добавлен.', ok: true }); await reload()
    } catch (error) { setMessage({ text: error instanceof Error ? error.message : 'Не удалось добавить участника.', ok: false }) }
    finally { setParticipantLoading(false) }
  }

  const fileReport = async (participantId: number, reason: string, comment: string) => {
    if (!participantActivity) return
    await post(`/api/organizer/activities/${participantActivity.id}/participants/${participantId}/reports`, { reason, comment })
    await refreshParticipants()
  }

  const retractReport = async (reportId: number) => {
    if (!participantActivity) return
    await remove(`/api/organizer/activities/${participantActivity.id}/reports/${reportId}`)
    await refreshParticipants()
  }

  const loadReports = (participantId: number) =>
    api<ParticipantReport[]>(`/api/organizer/activities/${participantActivity!.id}/participants/${participantId}/reports`)

  const selectedSport = sports.find(sport => sport.id === selectedSportId)
  const availableGameFormats = selectedSport ? gameFormatsBySport[selectedSport.slug] ?? [] : []
  const source = editing ?? template

  return <div className="nearby-page"><PublicHeader /><main className="organizer-page"><header><span className="eyebrow">Кабинет организатора</span><h1>{view === 'form' ? (editing ? 'Редактирование события' : template ? 'Повторение события' : 'Новое событие') : 'Ваши события'}</h1><p>{view === 'form' ? 'Вид спорта, тип, формат, время и точка встречи. Карточка сразу появится в поиске.' : 'Список ваших событий. Создайте новое или откройте участников.'}</p></header>{view === 'list' && <><div className="organizer-toolbar"><button type="button" className="button large" disabled={sports.length === 0} onClick={startCreating}>+ Новое событие</button></div>{message && <div className={message.ok ? 'success-message' : 'form-error'}>{message.text}</div>}</>}{view === 'form' && <section className="organizer-view"><button type="button" className="button ghost organizer-back" onClick={backToList}>← К списку событий</button><form key={`${editing?.id ?? (template ? `t${template.id}` : 'new')}-${formVersion}`} className="organizer-form" onSubmit={save}><div className="form-grid"><label>Название<input name="title" required maxLength={120} placeholder="Футбол 6×6 вечером" defaultValue={source?.title ?? ''} /></label><label>Тип активности<select name="eventType" defaultValue={source ? eventTypeValues[source.eventType] ?? 0 : 0}><option value="0">Поиграть вечером</option><option value="1">Совместная тренировка</option><option value="2">Тренировка с тренером</option><option value="5">Ищу команду</option><option value="7">Турнир</option></select></label><label>Вид спорта<select name="sportId" required value={selectedSportId ?? ''} onChange={event => setSelectedSportId(Number(event.target.value))}>{sports.map(sport => <option key={sport.id} value={sport.id}>{sport.name}</option>)}</select></label>{availableGameFormats.length > 0 && <label>Формат игры<select key={`${selectedSport?.slug}-${editing?.id ?? (template ? `t${template.id}` : 'new')}`} name="gameFormat" required defaultValue={source?.gameFormat && availableGameFormats.some(option => option.value === source.gameFormat) ? source.gameFormat : availableGameFormats[0].value}>{availableGameFormats.map(format => <option key={format.value} value={format.value}>{format.label}</option>)}</select></label>}<label>Место встречи<select value={venueChoice} onChange={event => setVenueChoice(event.target.value)} required>{venues.map(venue => <option key={venue.id} value={venue.id}>{venue.city} · {venue.name}</option>)}<option value="new-map">Отметить новое место на карте</option><option value="new-text">Указать новый адрес текстом</option></select></label>{(venueChoice === 'new-map' || venueChoice === 'new-text') && <div className="new-venue-fields full"><label>Название места<input required maxLength={160} placeholder="Поле на Московской" value={venueDraft.name} onChange={event => setVenueDraft(current => ({ ...current, name: event.target.value }))} /></label><label>Город<input required maxLength={120} placeholder="Казань" value={venueDraft.city} onChange={event => setVenueDraft(current => ({ ...current, city: event.target.value }))} /></label><label>Район<input maxLength={120} placeholder="Вахитовский" value={venueDraft.district} onChange={event => setVenueDraft(current => ({ ...current, district: event.target.value }))} /></label><label>Адрес или ориентир<input required maxLength={240} placeholder="ул. Московская, 1" value={venueDraft.address} onChange={event => setVenueDraft(current => ({ ...current, address: event.target.value }))} /></label><label className="venue-indoor"><input type="checkbox" checked={venueDraft.indoor} onChange={event => setVenueDraft(current => ({ ...current, indoor: event.target.checked }))} /> Крытая площадка</label>{venueChoice === 'new-map' && <YandexLocationPicker value={meetingPoint} addressQuery={[venueDraft.city, venueDraft.address].filter(Boolean).join(', ')} onChange={setMeetingPoint} onResolved={resolveVenue} />}</div>}<label>Начало<input name="startAt" type="datetime-local" required defaultValue={editing ? dateTimeInputValue(editing.startAt) : template ? repeatedStartAt(template.startAt) : localDateTime(24)} /></label><label>За сколько минут приходить<input name="arrivalMinutes" type="number" min="0" max="180" defaultValue={Number(source?.rules?.match(/(\d+)\s+минут/)?.[1] ?? 15)} /></label><label>Количество мест, включая организатора<input name="capacity" type="number" min="2" max="500" defaultValue={source?.capacity ?? 12} required /></label><label>Размер листа ожидания<input name="waitlistCapacity" type="number" min="0" max="500" defaultValue={source?.waitlistCapacity ?? 0} /></label><label>Цена, ₽<input name="price" type="number" min="0" step="1" defaultValue={source?.price ?? 0} required /></label><label>Уровень<select name="skillLevel" defaultValue={source?.skillLevel ?? 'Любой'}><option>Любой</option><option>Начинающий</option><option>Средний</option><option>Продвинутый</option></select></label><label>Возраст от<input name="minimumAge" type="number" min="1" max="99" defaultValue={source?.minimumAge ?? 18} required /></label><label>Возраст до<input name="maximumAge" type="number" min="1" max="99" defaultValue={source?.maximumAge ?? ''} placeholder="Без ограничения" /></label><label className="full">Дополнительная информация<textarea name="description" required maxLength={4000} placeholder="Что важно знать участникам: инвентарь, одежда, ориентир или другие детали." defaultValue={source?.description ?? ''} /></label><label className="full organizer-participation"><span><input name="organizerParticipates" type="checkbox" defaultChecked={source?.organizerParticipates ?? false} /><b>Я тоже участвую в активности</b></span><small>Если включено, организатор появится в списке участников и займёт одно место из общего лимита.</small></label></div><div className="organizer-form-actions"><button className="button large" disabled={pending || sports.length === 0}>{pending ? 'Сохраняем…' : editing ? 'Сохранить изменения' : 'Опубликовать активность'}</button><button type="button" className="button ghost" onClick={backToList}>Отмена</button></div>{message && <div className={message.ok ? 'success-message' : 'form-error'}>{message.text}</div>}</form></section>}{view === 'list' && <section className="organizer-list"><div><span className="eyebrow">Ваши события</span><h2>{activities.length}</h2></div>{activities.map(item => <article key={item.id}><span className={`activity-status ${item.status.toLowerCase()}`}>{item.status}</span><h3>{item.title}</h3><p>{item.sport}{item.gameFormat ? ` · ${formatGameFormat(item.sportSlug, item.gameFormat)}` : ''} · {formatDate(item.startAt)} · {item.venue.name}</p><small>{item.participantsCount}/{item.capacity} участников · {item.organizerParticipates ? 'вы занимаете одно место' : 'вы только организатор'}</small><div>{item.status !== 'Cancelled' && item.status !== 'Completed' && <button className="button ghost" disabled={pending} onClick={() => startEditing(item)}>Редактировать</button>}{item.status === 'Draft' && <button className="button" disabled={pending} onClick={() => void action(item, 'publish')}>Опубликовать</button>}{item.status !== 'Cancelled' && item.status !== 'Completed' && <button className="button ghost" disabled={pending} onClick={() => void action(item, 'cancel')}>Отменить</button>}{['Draft', 'Cancelled'].includes(item.status) && (confirmDeleteId === item.id ? <><button type="button" className="button danger" disabled={pending} onClick={() => void deleteActivity(item)}>Подтвердить удаление</button><button type="button" className="button ghost" disabled={pending} onClick={() => setConfirmDeleteId(null)}>Не удалять</button></> : <button type="button" className="button ghost danger" disabled={pending} onClick={() => setConfirmDeleteId(item.id)}>Удалить</button>)}<button className="button ghost" disabled={participantLoading} onClick={() => void openParticipants(item)}>Участники</button><button className="button ghost" disabled={pending || sports.length === 0} onClick={() => startRepeating(item)}>Повторить</button><Link to={`/activities/${item.slug}`}>Карточка →</Link></div></article>)}{activities.length === 0 && <div className="nearby-empty"><b>Событий пока нет</b><p>Нажмите «+ Новое событие», чтобы создать первое.</p></div>}</section>}{view === 'participants' && participantActivity && <section className="participants-manager"><div className="participants-manager-head"><div><span className="eyebrow">Участники события</span><h2>{participantActivity.title}</h2></div><button type="button" className="button ghost" onClick={() => { setParticipantActivity(null); setParticipants(null); setConfirmRemovalId(null); setView('list') }}>← К списку</button></div>{participantLoading && !participants ? <p>Загружаем участников…</p> : participants && <><div className="participant-counts"><span><b>{participants.confirmedCount}</b> подтверждено</span><span><b>{Math.max(0, participants.capacity - participants.confirmedCount)}</b> свободно</span><span><b>{participants.cancelledCount}</b> отменено</span></div><form className="participant-add" onSubmit={addParticipant}><input name="name" required minLength={2} maxLength={80} placeholder="Имя участника" /><input name="contact" maxLength={120} placeholder="Телефон / email / Telegram (необязательно)" /><button className="button" disabled={participantLoading}>Добавить участника</button></form><div className="participant-list">{participants.items.map(participant => <ParticipantRosterRow key={participant.id} participant={participant} activityPast={new Date(participantActivity.endAt).getTime() < nowMs} participantLoading={participantLoading} confirmRemovalId={confirmRemovalId} onConfirmRemoval={setConfirmRemovalId} onRemove={removeParticipant} onFileReport={fileReport} onRetractReport={retractReport} loadReports={loadReports} />)}</div>{participants.items.length === 0 && <div className="nearby-empty"><b>Участников пока нет</b></div>}</>}</section>}</main></div>
}

function GuestSuccessPanel({ activity, result }: { activity: Activity; result: GuestJoinResult }) {
  const waitlisted = result.status === 'Waitlisted'
  return <div className="guest-success" role="status">
    <span className="guest-success-mark">{waitlisted ? '⏳' : '✓'}</span>
    <h2>{waitlisted ? `Вы в листе ожидания, ${result.name}` : `Вы в игре, ${result.name}!`}</h2>
    <p>{waitlisted
      ? 'Как только освободится место, вы станете подтверждённым участником. Если вы оставили email, придёт уведомление. Сохраните ссылку управления записью.'
      : 'Место подтверждено. Сохраните событие и ссылку управления записью.'}</p>
    <div className="guest-success-actions">
      <a className="button" href={calendarDataUrl(activity)} download={`kasanie-${activity.slug}.ics`}>Добавить в календарь</a>
      <a className="button ghost" href={yandexRouteUrl(activity.venue)} target="_blank" rel="noreferrer">Открыть маршрут</a>
      <Link className="guest-manage-link" to={result.managePath}>Управлять записью или отменить</Link>
    </div>
    <small>Сохраните ссылку управления: она позволяет отменить запись без аккаунта.</small>
  </div>
}

export function PublicActivityPage() {
  const { slug = '' } = useParams()
  const location = useLocation()
  const { user } = useAuth()
  const [activity, setActivity] = useState<Activity | null>(null)
  const [participation, setParticipation] = useState<Participation | null>(null)
  const [guestConfirmation, setGuestConfirmation] = useState<GuestJoinResult | null>(null)
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null)
  const [pending, setPending] = useState(false)
  const [showGuestJoin, setShowGuestJoin] = useState(false)

  const reloadActivity = useCallback(async () => {
    const nextActivity = await api<Activity>(`/api/public/activities/${slug}`)
    setActivity(nextActivity)
    if (!user) return setParticipation(null)
    try { setParticipation(await api<Participation>(`/api/activities/${nextActivity.id}/participation`)) }
    catch (error) { if (error instanceof ApiError && error.status === 404) setParticipation(null); else throw error }
  }, [slug, user])

  useEffect(() => {
    void reloadActivity().catch(error => setMessage({ text: error instanceof Error ? error.message : 'Событие не найдено.', ok: false }))
  }, [reloadActivity])
  useEffect(() => { if (activity) rememberActivityId(activity.id) }, [activity])

  const join = async () => {
    if (!activity) return
    setPending(true); setMessage(null)
    try {
      const result = await post<{ status: string }>(`/api/activities/${activity.id}/join`)
      setMessage({ text: result.status === 'Waitlisted' ? 'Вы добавлены в лист ожидания.' : 'Вы записаны. Организатор увидит ваше участие.', ok: true })
      await reloadActivity()
    } catch (error) {
      setMessage({ text: error instanceof Error ? error.message : 'Не удалось записаться.', ok: false })
    } finally { setPending(false) }
  }

  const leave = async () => {
    if (!activity) return
    setPending(true); setMessage(null)
    try {
      await post(`/api/activities/${activity.id}/leave`)
      setMessage({ text: 'Участие отменено.', ok: true })
      await reloadActivity()
    } catch (error) {
      setMessage({ text: error instanceof Error ? error.message : 'Не удалось отменить участие.', ok: false })
    } finally { setPending(false) }
  }

  const guestJoin = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!activity) return
    const form = event.currentTarget
    const values = new FormData(form)
    setPending(true); setMessage(null)
    try {
      const result = await post<GuestJoinResult>(`/api/public/activities/${activity.id}/guest-join`, {
        name: values.get('name'),
        contact: values.get('contact'),
        adultConfirmed: values.get('adultConfirmed') === 'on'
      })
      form.reset()
      setShowGuestJoin(false)
      setGuestConfirmation(result)
      await reloadActivity()
    } catch (error) {
      setMessage({ text: error instanceof Error ? error.message : 'Не удалось отметиться.', ok: false })
    } finally { setPending(false) }
  }

  if (!activity) return <div className="nearby-page"><PublicHeader /><main className="activity-detail-loading">{message?.text ?? 'Загружаем событие…'}</main></div>

  const canJoin = activity.availablePlaces > 0 || activity.waitlistAvailablePlaces > 0
  const canCancelParticipation = participation ? ['Pending', 'Confirmed', 'Waitlisted'].includes(participation.status) : false
  const canCreateParticipation = !participation || participation.status === 'Cancelled'

  return <div className="nearby-page">
    <PublicHeader />
    <main className="activity-detail">
      <Link className="back-link" to="/">← Все занятия</Link>
      <section className="activity-detail-hero">
        <div>
          <span className="activity-type">{eventLabels[activity.eventType] ?? activity.eventType}</span>
          <h1>{activity.title}</h1>
          <p>{activity.description}</p>
          <div className="activity-detail-meta">
            <span><small>Когда</small><b>{formatDate(activity.startAt)}</b></span>
            <span><small>Где</small><b>{activity.venue.name}</b></span>
            <span><small>Формат</small><b>{activity.sport}{activity.gameFormat ? ` · ${formatGameFormat(activity.sportSlug, activity.gameFormat)}` : ''}</b></span>
            <span><small>Стоимость</small><b>{formatPrice(activity.price)}</b></span>
          </div>
        </div>
        <aside>
          {guestConfirmation ? <GuestSuccessPanel activity={activity} result={guestConfirmation} /> : <>
            <span className="eyebrow">Запись</span>
            <strong>{activity.availablePlaces}</strong>
            <small>{activity.availablePlaces > 0 ? `свободных мест из ${activity.capacity}` : activity.waitlistAvailablePlaces > 0 ? `мест в листе ожидания: ${activity.waitlistAvailablePlaces}` : 'запись закрыта'}</small>
            {user ? activity.isCurrentUserOrganizer ? <>
              <span className="participation-badge status-confirmed">{activity.organizerParticipates ? 'Вы участвуете и занимаете одно место' : 'Вы только организатор и не занимаете место'}</span>
              <Link className="button large" to="/organizer/activities">Настроить участие</Link>
            </> : <>
              {participation && <span className={`participation-badge status-${participation.status.toLowerCase()}`}>{participationLabels[participation.status] ?? participation.status}</span>}
              {canCancelParticipation && <button className="button ghost danger" disabled={pending} onClick={() => void leave()}>{pending ? 'Отменяем…' : 'Отменить участие'}</button>}
              {canCreateParticipation && <button className="button large" disabled={pending || !canJoin} onClick={() => void join()}>{pending ? 'Записываем…' : participation?.status === 'Cancelled' ? 'Записаться снова' : activity.availablePlaces > 0 ? 'Я буду' : activity.waitlistAvailablePlaces > 0 ? 'Встать в лист ожидания' : 'Мест нет'}</button>}
            </> : showGuestJoin ? <form className="guest-join-form" onSubmit={guestJoin}>
              <label>Как вас зовут<input name="name" required minLength={2} maxLength={80} autoComplete="name" /></label>
              <label>Телефон, email или Telegram<input name="contact" required minLength={3} maxLength={120} autoComplete="tel" /></label>
              <label className="guest-consent"><input name="adultConfirmed" type="checkbox" required />Мне исполнилось 18 лет, согласен на передачу контакта организатору</label>
              <button className="button large" disabled={pending || !canJoin}>{pending ? 'Отмечаем…' : activity.availablePlaces > 0 ? 'Подтвердить участие' : 'Встать в лист ожидания'}</button>
              <button type="button" className="button ghost" onClick={() => setShowGuestJoin(false)}>Назад</button>
            </form> : <>
              <button className="button large" disabled={!canJoin} onClick={() => setShowGuestJoin(true)}>{activity.availablePlaces > 0 ? 'Я буду' : activity.waitlistAvailablePlaces > 0 ? 'Встать в лист ожидания' : 'Мест нет'}</button>
              <small className="guest-without-account">Запись без регистрации</small>
              <Link className="guest-login-link" to="/login" state={{ from: location.pathname }}>Уже есть аккаунт? Войти</Link>
            </>}
          </>}
          {message && <div className={message.ok ? 'success-message' : 'form-error'} role={message.ok ? 'status' : 'alert'}>{message.text}</div>}
        </aside>
      </section>
      <section className="activity-detail-grid">
        <article>
          <span className="eyebrow">Площадка</span>
          <h2>{activity.venue.name}</h2>
          <p>{activity.venue.city}{activity.venue.district ? `, ${activity.venue.district}` : ''}<br />{activity.venue.address}</p>
          <div className="venue-actions">
            <a className="button ghost" href={yandexRouteUrl(activity.venue)} target="_blank" rel="noreferrer">Построить маршрут</a>
            <a className="button ghost" href={calendarDataUrl(activity)} download={`kasanie-${activity.slug}.ics`}>Добавить в календарь</a>
          </div>
          <YandexActivitiesMap compact items={[{ activity }]} />
        </article>
        <article>
          <span className="eyebrow">Условия</span>
          <h2>Что важно знать</h2>
          <dl>
            <div><dt>Вид спорта</dt><dd>{activity.sport}{activity.gameFormat ? ` · ${formatGameFormat(activity.sportSlug, activity.gameFormat)}` : ''}</dd></div>
            <div><dt>Уровень</dt><dd>{activity.skillLevel}</dd></div>
            <div><dt>Возраст</dt><dd>от {activity.minimumAge} лет{activity.maximumAge ? ` до ${activity.maximumAge}` : ''}</dd></div>
            <div><dt>Инвентарь</dt><dd>{activity.equipmentRequirements ?? 'Уточните у организатора'}</dd></div>
            <div><dt>Правила</dt><dd>{activity.rules ?? 'Уважайте других участников и сообщайте об отмене заранее.'}</dd></div>
          </dl>
        </article>
      </section>
    </main>
  </div>
}

export function GuestParticipationPage() {
  const { token = '' } = useParams()
  const [data, setData] = useState<GuestParticipation | null>(null)
  const [error, setError] = useState('')
  const [pending, setPending] = useState(false)
  const [confirmCancel, setConfirmCancel] = useState(false)

  const reload = useCallback(async () => {
    setError('')
    try { setData(await api<GuestParticipation>(`/api/public/guest-participations/${token}`)) }
    catch (requestError) { setError(requestError instanceof Error ? requestError.message : 'Ссылка управления недействительна.') }
  }, [token])

  useEffect(() => { void reload() }, [reload])

  const cancel = async () => {
    setPending(true); setError('')
    try {
      await post(`/api/public/guest-participations/${token}/cancel`)
      setConfirmCancel(false)
      await reload()
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Не удалось отменить участие.')
    } finally { setPending(false) }
  }

  if (!data) return <div className="nearby-page"><PublicHeader /><main className="activity-detail-loading">{error || 'Проверяем запись…'}</main></div>
  const active = ['Pending', 'Confirmed', 'Waitlisted'].includes(data.status)
  return <div className="nearby-page"><PublicHeader /><main className="guest-manage-page">
    <section>
      <span className="eyebrow">Запись без аккаунта</span>
      <span className={`participation-badge status-${data.status.toLowerCase()}`}>{participationLabels[data.status] ?? data.status}</span>
      <h1>{data.guestName}, ваша запись</h1>
      <h2>{data.activity.title}</h2>
      <p>{formatDate(data.activity.startAt)}<br />{data.activity.venue.name}, {data.activity.venue.address}</p>
      <div className="guest-manage-actions">
        <Link className="button" to={`/activities/${data.activity.slug}`}>Открыть событие</Link>
        <a className="button ghost" href={calendarDataUrl(data.activity)} download={`kasanie-${data.activity.slug}.ics`}>Добавить в календарь</a>
        <a className="button ghost" href={yandexRouteUrl(data.activity.venue)} target="_blank" rel="noreferrer">Открыть маршрут</a>
      </div>
      {active && (confirmCancel ? <div className="guest-cancel-confirm"><p>Освободить ваше место для другого участника?</p><button className="button danger" disabled={pending} onClick={() => void cancel()}>{pending ? 'Отменяем…' : 'Да, отменить запись'}</button><button className="button ghost" onClick={() => setConfirmCancel(false)}>Оставить запись</button></div> : <button className="button ghost danger" onClick={() => setConfirmCancel(true)}>Отменить участие</button>)}
      {data.status === 'Cancelled' && <div className="success-message">Запись отменена, место освобождено.</div>}
      {error && <div className="form-error" role="alert">{error}</div>}
      <small>Эта персональная ссылка заменяет вход в аккаунт. Не пересылайте её другим людям.</small>
    </section>
  </main></div>
}

export function MyActivitiesPage() {
  const [items, setItems] = useState<ParticipantActivity[]>([])
  const [now] = useState(() => Date.now())
  const [tab, setTab] = useState<'upcoming' | 'waitlist' | 'history'>('upcoming')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [confirmLeaveId, setConfirmLeaveId] = useState<number | null>(null)
  const reload = useCallback(async () => {
    setLoading(true); setError('')
    try { setItems(await api<ParticipantActivity[]>('/api/activities/mine')) }
    catch (requestError) { setError(requestError instanceof Error ? requestError.message : 'Не удалось загрузить активности.') }
    finally { setLoading(false) }
  }, [])
  useEffect(() => { void reload() }, [reload])

  const leave = async (activityId: number) => {
    setLoading(true); setError('')
    try { await post(`/api/activities/${activityId}/leave`); setConfirmLeaveId(null); await reload() }
    catch (requestError) { setError(requestError instanceof Error ? requestError.message : 'Не удалось отменить участие.'); setLoading(false) }
  }

  const visibleItems = items.filter(item => {
    const active = !['Cancelled', 'Rejected', 'NoShow', 'Attended'].includes(item.participation.status)
    if (tab === 'waitlist') return item.participation.status === 'Waitlisted'
    if (tab === 'history') return !active || new Date(item.activity.endAt).getTime() <= now
    return active && item.participation.status !== 'Waitlisted' && new Date(item.activity.endAt).getTime() > now
  })

  return <div className="nearby-page"><PublicHeader /><main className="my-activities-page"><header><span className="eyebrow">Личный список</span><h1>Мои активности</h1><p>Все записи, ожидание и прошедшие события в одном месте.</p></header><nav className="my-activities-tabs" aria-label="Фильтр активностей"><button className={tab === 'upcoming' ? 'active' : ''} onClick={() => setTab('upcoming')}>Предстоящие</button><button className={tab === 'waitlist' ? 'active' : ''} onClick={() => setTab('waitlist')}>Лист ожидания</button><button className={tab === 'history' ? 'active' : ''} onClick={() => setTab('history')}>История</button></nav>{error && <div className="form-error" role="alert">{error}</div>}{loading && items.length === 0 ? <div className="nearby-empty"><b>Загружаем активности…</b></div> : <section className="my-activities-list">{visibleItems.map(({ activity, participation }) => <article key={activity.id}><div className="my-activity-date"><b>{new Intl.DateTimeFormat('ru-RU', { day: '2-digit' }).format(new Date(activity.startAt))}</b><span>{new Intl.DateTimeFormat('ru-RU', { month: 'short' }).format(new Date(activity.startAt))}</span></div><div className="my-activity-main"><span className={`participant-status status-${participation.status.toLowerCase()}`}>{participationLabels[participation.status] ?? participation.status}</span><h2>{activity.title}</h2><p>{formatDate(activity.startAt)} · {activity.venue.city}{activity.venue.district ? `, ${activity.venue.district}` : ''}</p><small>{activity.organizerName} · {formatPrice(activity.price)}</small></div><div className="my-activity-actions"><Link className="button ghost" to={`/activities/${activity.slug}`}>Открыть</Link>{['Pending', 'Confirmed', 'Waitlisted'].includes(participation.status) && (confirmLeaveId === activity.id ? <><button className="button danger" disabled={loading} onClick={() => void leave(activity.id)}>Подтвердить отмену</button><button className="button ghost" onClick={() => setConfirmLeaveId(null)}>Оставить запись</button></> : <button className="button ghost danger" onClick={() => setConfirmLeaveId(activity.id)}>Отменить участие</button>)}</div></article>)}{visibleItems.length === 0 && <div className="nearby-empty"><b>Здесь пока ничего нет</b><p>Найдите подходящую игру или тренировку рядом.</p><Link className="button" to="/">Найти активность</Link></div>}</section>}</main></div>
}
