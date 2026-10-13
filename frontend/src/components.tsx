import { Fragment, type CSSProperties, type ReactNode } from 'react'
import { NavLink, Navigate, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from './auth'

export const roleHome: Record<string, string> = { Player: '/player', Coach: '/coach', Parent: '/parent', SchoolOwner: '/school', SchoolAdmin: '/school', Organizer: '/organizer/activities', Admin: '/admin' }
type NavigationItem = { to: string; label: string; icon: string; section?: string }

const sidebarMenuArtwork: Record<string, { image: string; scale: number }> = {
  '/coach': { image: 'overview', scale: 1.95 },
  '/coach/teams?tab=chat': { image: 'inbox', scale: 1.48 },
  '/coach/teams?tab=squad': { image: 'squad', scale: 1.32 },
  '/coach/teams?tab=squad&view=planning': { image: 'planning', scale: 1.61 },
  '/coach/teams?tab=tactics': { image: 'tactics', scale: 1.5 },
  '/coach/players': { image: 'development', scale: 1.87 },
  '/coach/trainings': { image: 'training', scale: 1.48 },
  '/coach/teams?tab=injuries': { image: 'medical', scale: 1.73 },
  '/coach/teams?tab=schedule': { image: 'schedule', scale: 1.64 },
  '/coach/teams?tab=tournaments': { image: 'tournaments', scale: 1.79 },
  '/school': { image: 'school', scale: 1.46 },
  '/school/teams': { image: 'school-teams', scale: 1.25 },
  '/school/coaches': { image: 'staff', scale: 1.5 },
  '/school/players': { image: 'school-players', scale: 1.3 },
  '/school/settings': { image: 'school-settings', scale: 1.32 },
  '/account/security': { image: 'security', scale: 1.6 },
}

function NavigationIcon({ item }: { item: NavigationItem }) {
  const artwork = sidebarMenuArtwork[item.to]
  if (!artwork) return <span aria-hidden>{item.icon}</span>
  return <span className="sidebar-menu-icon" aria-hidden="true" style={{ '--menu-icon-scale': artwork.scale } as CSSProperties}><img src={`/brand/navigation/${artwork.image}.webp`} alt="" width="26" height="26" decoding="async" /></span>
}

const navigation: Record<string, NavigationItem[]> = {
  Player: [
    { to: '/player', label: 'Главная', icon: '⌂' }, { to: '/player/assessment', label: 'Тестирование', icon: '◎' },
    { to: '/player/training', label: 'Тренировки', icon: '▶' }, { to: '/player/progress', label: 'Прогресс', icon: '↗' }, { to: '/player/profile', label: 'Профиль', icon: '◉' },
  ],
  Coach: [
    { to: '/coach', label: 'Обзор', icon: '⌂', section: 'Команда' },
    { to: '/coach/teams?tab=chat', label: 'Входящие', icon: '✉' },
    { to: '/coach/teams?tab=squad', label: 'Состав', icon: '◉' },
    { to: '/coach/teams?tab=squad&view=planning', label: 'Планировщик состава', icon: '▦' },
    { to: '/coach/teams?tab=tactics', label: 'Тактика', icon: '◫' },
    { to: '/coach/players', label: 'Центр развития', icon: '↗' },
    { to: '/coach/trainings', label: 'Тренировки', icon: '✓', section: 'Процесс' },
    { to: '/coach/teams?tab=injuries', label: 'Медштаб', icon: '✚' },
    { to: '/coach/teams?tab=schedule', label: 'Расписание', icon: '▤' },
    { to: '/coach/teams?tab=tournaments', label: 'Турниры', icon: '♜' },
    { to: '/school', label: 'Школа', icon: '⌂', section: 'Управление школой' },
    { to: '/school/teams', label: 'Все команды', icon: '▦' },
    { to: '/school/coaches', label: 'Персонал', icon: '◆' },
    { to: '/school/players', label: 'Все игроки', icon: '◉' },
    { to: '/school/settings', label: 'Настройки школы', icon: '◇' },
  ],
  Parent: [{ to: '/parent', label: 'Мои дети', icon: '⌂' }],
  SchoolOwner: [{ to: '/school', label: 'Школа', icon: '⌂' }, { to: '/school/teams', label: 'Команды', icon: '▦' }, { to: '/school/coaches', label: 'Тренеры', icon: '◆' }, { to: '/school/players', label: 'Игроки', icon: '◉' }, { to: '/school/settings', label: 'Настройки', icon: '◇' }],
  SchoolAdmin: [{ to: '/school', label: 'Школа', icon: '⌂' }, { to: '/school/teams', label: 'Команды', icon: '▦' }, { to: '/school/coaches', label: 'Тренеры', icon: '◆' }, { to: '/school/players', label: 'Игроки', icon: '◉' }, { to: '/school/settings', label: 'Настройки', icon: '◇' }],
  Admin: [{ to: '/admin', label: 'Статистика', icon: '↗' }, { to: '/admin/feedback', label: 'Обращения', icon: '✦' }, { to: '/admin/schools', label: 'Школы', icon: '▦' }, { to: '/admin/exercises', label: 'Упражнения', icon: '◆' }, { to: '/admin/assessments', label: 'Тесты', icon: '◎' }, { to: '/admin/programs', label: 'Программы', icon: '▤' }, { to: '/admin/municipalities', label: 'Города', icon: '⌖' }, { to: '/admin/users', label: 'Пользователи', icon: '◉' }],
}

const rolePriority = ['Admin', 'Coach', 'SchoolOwner', 'SchoolAdmin', 'Parent', 'Player', 'Organizer']
export const primaryRole = (roles: string[]) => rolePriority.find(x => roles.includes(x)) ?? roles[0] ?? ''

export function RoleGuard({ role }: { role: string | string[] }) {
  const { user, loading } = useAuth()
  const location = useLocation()
  if (loading) return <FullLoader />
  if (!user) return <Navigate to="/login" replace state={{ from: location.pathname }} />
  const allowed = Array.isArray(role) ? role : [role]
  if (!allowed.some(x => user.roles.includes(x))) return <Navigate to={roleHome[primaryRole(user.roles)] ?? '/'} replace />
  return <Outlet />
}

export function AuthenticatedGuard() {
  const { user, loading } = useAuth()
  const location = useLocation()
  if (loading) return <FullLoader />
  if (!user) return <Navigate to="/login" replace state={{ from: location.pathname }} />
  return <Outlet />
}

export function AppShell() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const role = primaryRole(user?.roles ?? [])
  const items = Array.from(new Map((user?.roles.flatMap(x => navigation[x] ?? []) ?? []).map(x => [x.to, x])).values()) as NavigationItem[]
  const navigationItems: NavigationItem[] = [...items, { to: '/account/security', label: 'Безопасность', icon: '◇', section: role === 'Coach' ? 'Профиль' : undefined }]
  const isCurrent = (to: string) => {
    const [pathname, search = ''] = to.split('?')
    if (location.pathname !== pathname) return false
    const expected = new URLSearchParams(search)
    const current = new URLSearchParams(location.search)
    if ([...expected].some(([key, value]) => current.get(key) !== value)) return false
    return [...current.keys()].every(key => key === 'team' || expected.has(key))
  }
  return <div className="app-shell">
    <aside className="sidebar">
      <NavLink className="brand" to={roleHome[role] ?? '/'}><span className="brand-emblem brand-emblem-outlined"><img src="/brand/kasanie-logo-official.png" alt="" /></span><span><strong>КАСАНИЕ</strong><small>спортивная платформа</small></span></NavLink>
      <nav aria-label="Основная навигация">{navigationItems.map((item, index) => <Fragment key={item.to}>{item.section && (index === 0 || navigationItems[index - 1].section !== item.section) && <span className="sidebar-nav-title">{item.section}</span>}<NavLink to={item.to} className={() => isCurrent(item.to) ? 'active' : ''}><NavigationIcon item={item} />{item.label}</NavLink></Fragment>)}</nav>
      <div className="sidebar-user"><span className="avatar">{user?.email[0].toUpperCase()}</span><div><small>{roleLabel(role)}</small><span>{user?.email}</span></div></div>
      <button className="link-button" onClick={async () => { await logout(); navigate('/login') }}>Выйти</button>
    </aside>
    <main className="app-main"><Outlet /></main>
  </div>
}

export function roleLabel(role: string) { return ({ Player: 'Игрок', Coach: 'Тренер', Parent: 'Родитель', SchoolOwner: 'Владелец школы', SchoolAdmin: 'Администратор школы', Organizer: 'Организатор', Admin: 'Администратор' } as Record<string, string>)[role] ?? role }
export function FullLoader() { return <div className="full-loader" role="status"><span className="ball-loader" />Загружаем поле…</div> }
export function PageLoader() { return <div className="panel-state" role="status"><span className="ball-loader" />Загрузка данных…</div> }
export function ErrorState({ message, retry }: { message: string; retry?: () => void }) { return <div className="panel-state error-state"><strong>Не удалось загрузить данные</strong><span>{message}</span>{retry && <button onClick={retry}>Повторить</button>}</div> }
export function EmptyState({ title, children }: { title: string; children?: ReactNode }) { return <div className="panel-state"><span className="empty-icon">○</span><strong>{title}</strong>{children}</div> }
export function PageHeader({ eyebrow, title, actions }: { eyebrow?: string; title: string; actions?: ReactNode }) { return <header className="page-header"><div>{eyebrow && <span className="eyebrow">{eyebrow}</span>}<h1>{title}</h1></div>{actions}</header> }
export function StatCard({ label, value, detail, tone = 'default' }: { label: string; value: string | number; detail?: string; tone?: 'default' | 'accent' | 'coral' }) { return <article className={`stat-card ${tone}`}><span>{label}</span><strong>{value}</strong>{detail && <small>{detail}</small>}</article> }
export function ProgressBar({ value, label }: { value: number; label?: string }) { return <div className="progress-wrap">{label && <div><span>{label}</span><strong>{Math.round(value)}%</strong></div>}<div className="progress-track" role="progressbar" aria-valuenow={Math.round(value)} aria-valuemin={0} aria-valuemax={100}><span style={{ width: `${Math.max(0, Math.min(100, value))}%` }} /></div></div> }

export type Skills = { speed: number; endurance: number; ballControl: number; passing: number; shooting: number; agility: number }
const skillLabels: [keyof Skills, string][] = [['speed', 'Скорость'], ['endurance', 'Выносливость'], ['ballControl', 'Контроль'], ['passing', 'Передачи'], ['shooting', 'Удары'], ['agility', 'Ловкость']]
export function RadarChart({ values, previous }: { values: Skills; previous?: Skills }) {
  const centerX = 180, centerY = 145, radius = 88
  const point = (index: number, value: number): [number, number] => {
    const angle = -Math.PI / 2 + index * Math.PI / 3
    const scaledRadius = radius * value / 100
    return [centerX + Math.cos(angle) * scaledRadius, centerY + Math.sin(angle) * scaledRadius]
  }
  const pointText = (index: number, value: number) => point(index, value).join(',')
  const polygon = (skills: Skills) => skillLabels.map(([key], index) => pointText(index, skills[key])).join(' ')
  const labelAnchor = (index: number): 'start' | 'end' | 'middle' => index === 1 || index === 2 ? 'start' : index === 4 || index === 5 ? 'end' : 'middle'
  return <div className="radar-wrap"><svg className="radar" viewBox="0 0 360 290" role="img" aria-label="Диаграмма шести футбольных навыков">
    {[25, 50, 75, 100].map(level => <polygon key={level} className="radar-grid" points={skillLabels.map((_, i) => pointText(i, level)).join(' ')} />)}
    {skillLabels.map(([, label], i) => { const [labelX, labelY] = point(i, 127); const [axisX, axisY] = point(i, 100); return <g key={label}><line className="radar-axis" x1={centerX} y1={centerY} x2={axisX} y2={axisY} /><text x={labelX} y={labelY} textAnchor={labelAnchor(i)} dominantBaseline="middle">{label}</text></g> })}
    {previous && <polygon className="radar-previous" points={polygon(previous)} />}
    <polygon className="radar-current" points={polygon(values)} />
    {skillLabels.map(([key], i) => { const [x, y] = point(i, values[key]); return <circle key={key} cx={x} cy={y} r="4" /> })}
  </svg><div className="radar-legend"><span><i className="current" />Сейчас</span>{previous && <span><i className="previous" />Раньше</span>}</div></div>
}

export function FieldGraphic() { return <div className="field-graphic" aria-hidden><div className="field-circle" /><span className="field-center-mark" /><div className="field-box left" /><div className="field-box right" /><span className="field-route route-pass" /><span className="field-player one"><b>7</b></span><span className="field-player two"><b>10</b></span><span className="field-player three"><b>4</b></span><span className="field-ball" /></div> }
