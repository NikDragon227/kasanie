import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api'
import '../home.css'

const asset = (name: string) => `/brand/home-reference/${name}-v2.png`

const spotlightCards = [
  { image: 'nearby', icon: 'location', title: 'Найдите активность рядом', copy: 'Игры, тренировки, секции и события поблизости.', action: 'Найти игру рядом', link: '/sports', accent: 'blue' },
  { image: 'route', icon: 'route', title: 'Создайте свой спортивный маршрут', copy: 'Постройте свой путь: от события к прогрессу. Планируйте, развивайтесь и достигайте новых целей вместе.', action: 'Выбрать роль', link: '/join', accent: 'violet' }
] as const

const audienceCards = [
  { image: 'child-parent', icon: 'people', title: 'Дети и родители', copy: 'Секции и занятия для ребёнка. Получайте обратную связь и находите лучшие занятия рядом с вами.', link: '/register-parent' },
  { image: 'player', icon: 'player', title: 'Игроки', copy: 'Находите игры и тренировки, развивайте навыки и достигайте своих спортивных целей.', link: '/register' },
  { image: 'coach', icon: 'coach', title: 'Тренеры', copy: 'Планируйте тренировки, управляйте командами и отслеживайте прогресс своих игроков.', link: '/register-coach' },
  { image: 'school', icon: 'school', title: 'Школы и секции', copy: 'Управляйте группами, расписанием и взаимодействием с игроками и их родителями.', link: '/login' }
] as const

type PlatformStats = { users: number; teams: number; tournaments: number; coaches: number; trustPercent: number | null }
const numberFormatter = new Intl.NumberFormat('ru-RU')

function HomeIcon({ name }: { name: string }) {
  const common = { viewBox: '0 0 24 24', 'aria-hidden': true, focusable: false } as const
  if (name === 'location') return <svg {...common}><path d="M20 10c0 5-8 11-8 11S4 15 4 10a8 8 0 1 1 16 0Z" /><circle cx="12" cy="10" r="2.5" /></svg>
  if (name === 'route') return <svg {...common}><path d="M5 19c3-5 5-9 8-9 2.2 0 2.5 3 5.5 3 1.1 0 1.8-.4 2.5-1" /><path d="m16 9 3-3 3 3" /><circle cx="5" cy="19" r="2" /></svg>
  if (name === 'people') return <svg {...common}><circle cx="8" cy="8" r="3" /><circle cx="17" cy="9" r="2.5" /><path d="M2.5 20c.4-5 2.2-7.5 5.5-7.5s5.1 2.5 5.5 7.5M14 14c3.8-.5 6 1.5 6.5 5" /></svg>
  if (name === 'player') return <svg {...common}><circle cx="12" cy="6" r="3" /><path d="M6 21v-3c0-3.5 2.2-6 6-6s6 2.5 6 6v3M4 13l3 2m13-2-3 2" /></svg>
  if (name === 'coach') return <svg {...common}><circle cx="12" cy="7" r="3" /><path d="M6 21v-3c0-4 2-7 6-7s6 3 6 7v3M9 15h6M9 18h6" /></svg>
  if (name === 'school') return <svg {...common}><path d="m3 10 9-6 9 6M5 10v9h14v-9M9 19v-5h6v5M2 20h20" /></svg>
  if (name === 'mail') return <svg {...common}><rect x="3" y="5" width="18" height="14" rx="2" /><path d="m4 7 8 6 8-6" /></svg>
  if (name === 'phone') return <svg {...common}><path d="M7 3h3l1 5-2 1.5a15 15 0 0 0 5.5 5.5L16 13l5 1v3c0 1.7-1.3 3-3 3C10.3 20 4 13.7 4 6a3 3 0 0 1 3-3Z" /></svg>
  return <svg {...common}><circle cx="12" cy="12" r="9" /><path d="m8 12 2.5 2.5L16 9" /></svg>
}

function Brand() {
  return <Link className="home-brand" to="/" aria-label="Касание — главная"><span className="home-brand-mark"><img src="/brand/kasanie-mark.webp" alt="" /></span><strong>КАСАНИЕ</strong></Link>
}

function Header() {
  const [open, setOpen] = useState(false)
  return <header className="home-header"><Brand /><button className="home-menu" type="button" aria-expanded={open} aria-controls="home-nav" onClick={() => setOpen(value => !value)}><span /><span /><span /><b>Меню</b></button><nav id="home-nav" className={open ? 'open' : ''} aria-label="Навигация по главной странице"><a href="#top" onClick={() => setOpen(false)}>Главная</a><a href="#capabilities" onClick={() => setOpen(false)}>Возможности</a><a href="#families" onClick={() => setOpen(false)}>Для родителей</a><a href="#coaches" onClick={() => setOpen(false)}>Для тренеров</a><a href="#events" onClick={() => setOpen(false)}>События</a></nav><Link className="home-button small" to="/join">Начать</Link></header>
}

function Hero() {
  return <section id="top" className="home-hero" aria-labelledby="home-title"><div className="home-hero-copy"><h1 id="home-title">Спорт начинается<br />с первого <em>касания</em></h1><div id="capabilities" className="home-spotlight-grid" aria-label="Основные сценарии">{spotlightCards.map(card => <Link className={`home-spotlight-card accent-${card.accent}`} to={card.link} key={card.title} aria-label={card.accent === 'blue' ? 'Найти игру рядом' : 'Стать участником — выбрать роль'}><i className="home-spotlight-icon"><HomeIcon name={card.icon} /></i><div className="home-spotlight-copy"><h2>{card.title}</h2><p>{card.copy}</p><span>{card.action} <b>→</b></span></div><img src={asset(card.image)} alt="" loading="eager" />{card.accent === 'violet' && <div className="home-role-pills"><span>Игрок</span><span>Родитель</span><span>Тренер</span></div>}</Link>)}</div></div><div className="home-hero-visual" aria-hidden="true"><img src={asset('hero')} alt="" fetchPriority="high" /></div></section>
}

function Audiences() {
  return <section id="events" className="home-audiences" aria-labelledby="audience-title"><h2 id="audience-title">У каждого своё касание</h2><div className="home-audience-grid">{audienceCards.map((card, index) => <Link id={index === 0 ? 'families' : index === 2 ? 'coaches' : undefined} className="home-audience-card" to={card.link} key={card.title} aria-label={`${card.title}: ${card.copy}`}><img src={asset(card.image)} alt="" loading="lazy" /><div className="home-audience-shade" /><i className="home-audience-icon"><HomeIcon name={card.icon} /></i><div className="home-audience-copy"><h3>{card.title}</h3><p>{card.copy}</p></div></Link>)}</div></section>
}

function LegacyStats() {
  const [stats, setStats] = useState<PlatformStats | null>(null)
  useEffect(() => { let active = true; api<PlatformStats>('/api/public/platform-stats').then(value => { if (active) setStats(value) }).catch(() => undefined); return () => { active = false } }, [])
  const values = stats ? [numberFormatter.format(stats.users), numberFormatter.format(stats.teams), numberFormatter.format(stats.tournaments), numberFormatter.format(stats.coaches), stats.trustPercent == null ? '—' : `${numberFormatter.format(stats.trustPercent)}%`] : ['—', '—', '—', '—', '—']
  return <div className="home-legacy-stats" aria-label="Касание в цифрах">{values.map((value, index) => <span key={`value-${index}`}>{value}</span>)}{['пользователей', 'команд', 'турниров организовано', 'тренеров', 'доверие пользователей'].map(label => <span key={label}>{label}</span>)}</div>
}

function Footer() {
  return <footer className="home-footer"><div className="home-footer-main"><div className="home-footer-brand"><Brand /><p>Платформа для спорта и развития.<br />Объединяем игроков, команды и возможности.</p></div><div className="home-footer-links"><a href="#top">Главная</a><a href="#capabilities">Возможности</a><a href="#families">Для родителей</a><a href="#coaches">Для тренеров</a></div><div className="home-footer-contact"><span>Свяжитесь с нами</span><a href="mailto:hello@kasanie.sport"><HomeIcon name="mail" />hello@kasanie.sport</a><a href="tel:+74951234567"><HomeIcon name="phone" />+7 (495) 123-45-67</a></div><div className="home-footer-social"><span>Мы в соцсетях</span><div><a href="#social-vk" aria-label="ВКонтакте">VK</a><a href="#social-telegram" aria-label="Telegram">↗</a><a href="#social-instagram" aria-label="Instagram">◎</a></div></div></div><div className="home-footer-bottom"><span>© 2026 КАСАНИЕ</span><div><a href="#privacy">Политика конфиденциальности</a><i /><a href="#terms">Пользовательское соглашение</a></div></div></footer>
}

export function HomePage() {
  return <div className="home-page"><div className="home-frame"><Header /><main><Hero /><Audiences /><LegacyStats /></main><Footer /></div></div>
}
