import { type ReactNode } from 'react'
import { NavLink, Navigate, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from './auth'

const roleHome: Record<string, string> = { Player: '/player', Coach: '/coach', Parent: '/parent', RegionalAnalyst: '/analytics', Admin: '/admin' }
const navigation: Record<string, { to: string; label: string; icon: string }[]> = {
  Player: [
    { to: '/player', label: 'Главная', icon: '⌂' }, { to: '/player/assessment', label: 'Тестирование', icon: '◎' },
    { to: '/player/training', label: 'Тренировки', icon: '▶' }, { to: '/player/progress', label: 'Прогресс', icon: '↗' }, { to: '/player/profile', label: 'Профиль', icon: '◉' },
  ],
  Coach: [{ to: '/coach', label: 'Обзор', icon: '⌂' }, { to: '/coach/players', label: 'Игроки', icon: '◉' }],
  Parent: [{ to: '/parent', label: 'Мои дети', icon: '⌂' }],
  RegionalAnalyst: [{ to: '/analytics', label: 'Регион', icon: '▥' }],
  Admin: [{ to: '/admin', label: 'Обзор', icon: '⌂' }, { to: '/admin/exercises', label: 'Упражнения', icon: '◆' }, { to: '/admin/assessments', label: 'Тесты', icon: '◎' }, { to: '/admin/programs', label: 'Программы', icon: '▤' }, { to: '/admin/municipalities', label: 'Города', icon: '⌖' }, { to: '/admin/users', label: 'Пользователи', icon: '◉' }],
}

export function RoleGuard({ role }: { role: string }) {
  const { user, loading } = useAuth()
  const location = useLocation()
  if (loading) return <FullLoader />
  if (!user) return <Navigate to="/login" replace state={{ from: location.pathname }} />
  if (!user.roles.includes(role)) return <Navigate to={roleHome[user.roles[0]] ?? '/'} replace />
  return <Outlet />
}

export function AppShell() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const role = user?.roles[0] ?? ''
  return <div className="app-shell">
    <aside className="sidebar">
      <NavLink className="brand" to={roleHome[role] ?? '/'}><span className="brand-mark">К</span><span><strong>КАСАНИЕ</strong><small>футбольное развитие</small></span></NavLink>
      <nav aria-label="Основная навигация">{(navigation[role] ?? []).map(item => <NavLink key={item.to} to={item.to} end={item.to === roleHome[role]}><span aria-hidden>{item.icon}</span>{item.label}</NavLink>)}</nav>
      <div className="sidebar-user"><span className="avatar">{user?.email[0].toUpperCase()}</span><div><small>{roleLabel(role)}</small><span>{user?.email}</span></div></div>
      <button className="link-button" onClick={async () => { await logout(); navigate('/login') }}>Выйти</button>
    </aside>
    <main className="app-main"><Outlet /></main>
  </div>
}

function roleLabel(role: string) { return ({ Player: 'Игрок', Coach: 'Тренер', Parent: 'Родитель', RegionalAnalyst: 'Аналитик', Admin: 'Администратор' } as Record<string, string>)[role] ?? role }
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
