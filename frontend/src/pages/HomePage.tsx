import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api'
import '../home.css'

const asset = (name: string) => `/brand/home-reference/${name}-v2.png`
const icon = (name: string) => `/brand/icons/${name}.webp`

const spotlightCards = [
  { image: 'nearby', icon: 'activity-location', title: 'Найдите активность рядом', copy: 'Игры, тренировки, секции и события поблизости.', action: 'Найти игру рядом', link: '/sports', accent: 'blue' },
  { image: 'route', icon: 'sports-route', title: 'Создайте свой спортивный маршрут', copy: 'Постройте свой путь: от события к прогрессу. Планируйте, развивайтесь и достигайте новых целей вместе.', action: 'Выбрать роль', link: '/join', accent: 'violet' }
] as const

const audienceCards = [
  { image: 'child-parent', icon: 'children-parents', title: 'Дети и родители', copy: 'Секции и занятия для ребёнка. Получайте обратную связь и находите лучшие занятия рядом с вами.', link: '/register-parent' },
  { image: 'player', icon: 'players', title: 'Игроки', copy: 'Находите игры и тренировки, развивайте навыки и достигайте своих спортивных целей.', link: '/register' },
  { image: 'coach', icon: 'coaches', title: 'Тренеры', copy: 'Планируйте тренировки, управляйте командами и отслеживайте прогресс своих игроков.', link: '/register-coach' },
  { image: 'school', icon: 'schools-sections', title: 'Школы и секции', copy: 'Управляйте группами, расписанием и взаимодействием с игроками и их родителями.', link: '/login' }
] as const

type PlatformStats = { users: number; teams: number; tournaments: number; coaches: number; trustPercent: number | null }
const numberFormatter = new Intl.NumberFormat('ru-RU')

function HomeIcon({ name }: { name: string }) {
  const common = { viewBox: '0 0 24 24', 'aria-hidden': true, focusable: false } as const
  if (name === 'mail') return <svg {...common}><rect x="3" y="5" width="18" height="14" rx="2" /><path d="m4 7 8 6 8-6" /></svg>
  if (name === 'phone') return <svg {...common}><path d="M7 3h3l1 5-2 1.5a15 15 0 0 0 5.5 5.5L16 13l5 1v3c0 1.7-1.3 3-3 3C10.3 20 4 13.7 4 6a3 3 0 0 1 3-3Z" /></svg>
  if (name === 'vk') return <svg {...common} fill="currentColor" stroke="none"><path d="M13.02 17.36c-5.36 0-8.41-3.68-8.54-9.8h2.69c.09 4.5 2.07 6.4 3.64 6.79V7.56h2.53v3.88c1.55-.17 3.18-1.94 3.73-3.88h2.53a7.45 7.45 0 0 1-3.43 4.88 7.72 7.72 0 0 1 4.02 4.92h-2.79c-.53-1.66-2.02-2.95-4.06-3.15v3.15h-.3z" /></svg>
  if (name === 'telegram') return <svg {...common} fill="currentColor" stroke="none"><path d="M21.94 4.9 18.9 19.2c-.23 1.01-.83 1.26-1.68.79l-4.64-3.42-2.24 2.16c-.25.25-.46.46-.94.46l.33-4.73 8.62-7.79c.38-.33-.08-.52-.58-.19L7.42 13.2l-4.58-1.43c-1-.31-1.02-1 .21-1.48l17.9-6.9c.83-.31 1.56.19 1.29 1.51z" /></svg>
  if (name === 'instagram') return <svg {...common} fill="currentColor" stroke="none"><path d="M12 2.16c3.2 0 3.58.01 4.85.07 1.17.05 1.8.25 2.23.41.56.22.96.48 1.38.9.42.42.68.82.9 1.38.16.42.36 1.06.41 2.23.06 1.27.07 1.65.07 4.85s-.01 3.58-.07 4.85c-.05 1.17-.25 1.8-.41 2.23-.22.56-.48.96-.9 1.38-.42.42-.82.68-1.38.9-.42.16-1.06.36-2.23.41-1.27.06-1.65.07-4.85.07s-3.58-.01-4.85-.07c-1.17-.05-1.8-.25-2.23-.41a3.7 3.7 0 0 1-1.38-.9 3.7 3.7 0 0 1-.9-1.38c-.16-.42-.36-1.06-.41-2.23-.06-1.27-.07-1.65-.07-4.85s.01-3.58.07-4.85c.05-1.17.25-1.8.41-2.23.22-.56.48-.96.9-1.38.42-.42.82-.68 1.38-.9.42-.16 1.06-.36 2.23-.41 1.27-.06 1.65-.07 4.85-.07M12 0C8.74 0 8.33.01 7.05.07 5.78.13 4.9.33 4.14.63c-.79.3-1.46.72-2.13 1.38C1.35 2.68.93 3.35.63 4.14.33 4.9.13 5.78.07 7.05.01 8.33 0 8.74 0 12s.01 3.67.07 4.95c.06 1.27.26 2.15.56 2.91.3.79.72 1.46 1.38 2.13.67.66 1.34 1.08 2.13 1.38.76.3 1.64.5 2.91.56C8.33 23.99 8.74 24 12 24s3.67-.01 4.95-.07c1.27-.06 2.15-.26 2.91-.56.79-.3 1.46-.72 2.13-1.38.66-.67 1.08-1.34 1.38-2.13.3-.76.5-1.64.56-2.91.06-1.28.07-1.69.07-4.95s-.01-3.67-.07-4.95c-.06-1.27-.26-2.15-.56-2.91-.3-.79-.72-1.46-1.38-2.13C21.32 1.35 20.65.93 19.86.63 19.1.33 18.22.13 16.95.07 15.67.01 15.26 0 12 0zm0 5.84A6.16 6.16 0 1 0 18.16 12 6.16 6.16 0 0 0 12 5.84zM12 16a4 4 0 1 1 4-4 4 4 0 0 1-4 4zm6.41-10.85a1.44 1.44 0 1 0 1.44 1.44 1.44 1.44 0 0 0-1.44-1.44z" /></svg>
  return <svg {...common}><circle cx="12" cy="12" r="9" /><path d="m8 12 2.5 2.5L16 9" /></svg>
}

function Brand() {
  return <Link className="home-brand" to="/" aria-label="Касание — главная"><span className="home-brand-mark"><img src="/brand/kasanie-mark.webp" alt="" /></span><strong>КАСАНИЕ</strong></Link>
}

function Header() {
  const [open, setOpen] = useState(false)
  return <header className="home-header"><Brand /><button className="home-menu" type="button" aria-expanded={open} aria-controls="home-nav" onClick={() => setOpen(value => !value)}><span /><span /><span /><b>Меню</b></button><nav id="home-nav" className={open ? 'open' : ''} aria-label="Навигация по главной странице"><a href="#top" onClick={() => setOpen(false)}>Главная</a><a href="#capabilities" onClick={() => setOpen(false)}>Возможности</a><a href="#families" onClick={() => setOpen(false)}>Для родителей</a><a href="#coaches" onClick={() => setOpen(false)}>Для тренеров</a><a href="#events" onClick={() => setOpen(false)}>События</a></nav></header>
}

function Hero() {
  return <section id="top" className="home-hero" aria-labelledby="home-title"><div className="home-hero-copy"><h1 id="home-title">Спорт начинается<br />с первого <em>касания</em></h1><div id="capabilities" className="home-spotlight-grid" aria-label="Основные сценарии">{spotlightCards.map(card => <Link className={`home-spotlight-card accent-${card.accent}`} to={card.link} key={card.title} aria-label={card.accent === 'blue' ? 'Найти игру рядом' : 'Стать участником — выбрать роль'}><i className="home-spotlight-icon"><img src={icon(card.icon)} alt="" width={46} height={46} /></i><div className="home-spotlight-copy"><h2>{card.title}</h2><p>{card.copy}</p><span className="home-spotlight-cta">{card.action} <b>→</b></span></div><img src={asset(card.image)} alt="" loading="eager" />{card.accent === 'violet' && <div className="home-role-pills"><span><img src={icon('role-player')} alt="" width={18} height={18} />Игрок</span><span><img src={icon('role-parent')} alt="" width={18} height={18} />Родитель</span><span><img src={icon('role-coach')} alt="" width={18} height={18} />Тренер</span></div>}</Link>)}</div></div><div className="home-hero-visual" aria-hidden="true"><img src={asset('hero')} alt="" fetchPriority="high" /></div></section>
}

function Audiences() {
  return <section id="events" className="home-audiences" aria-labelledby="audience-title"><h2 id="audience-title">У каждого своё касание</h2><div className="home-audience-grid">{audienceCards.map((card, index) => <Link id={index === 0 ? 'families' : index === 2 ? 'coaches' : undefined} className="home-audience-card" to={card.link} key={card.title} aria-label={`${card.title}: ${card.copy}`}><img src={asset(card.image)} alt="" loading="lazy" /><div className="home-audience-shade" /><i className="home-audience-icon"><img src={icon(card.icon)} alt="" width={46} height={46} /></i><div className="home-audience-copy"><h3>{card.title}</h3><p>{card.copy}</p></div></Link>)}</div></section>
}

function LegacyStats() {
  const [stats, setStats] = useState<PlatformStats | null>(null)
  useEffect(() => { let active = true; api<PlatformStats>('/api/public/platform-stats').then(value => { if (active) setStats(value) }).catch(() => undefined); return () => { active = false } }, [])
  const values = stats ? [numberFormatter.format(stats.users), numberFormatter.format(stats.teams), numberFormatter.format(stats.tournaments), numberFormatter.format(stats.coaches), stats.trustPercent == null ? '—' : `${numberFormatter.format(stats.trustPercent)}%`] : ['—', '—', '—', '—', '—']
  return <div className="home-legacy-stats" aria-label="Касание в цифрах">{values.map((value, index) => <span key={`value-${index}`}>{value}</span>)}{['пользователей', 'команд', 'турниров организовано', 'тренеров', 'доверие пользователей'].map(label => <span key={label}>{label}</span>)}</div>
}

function Footer() {
  return <footer className="home-footer"><div className="home-footer-main"><div className="home-footer-brand"><Brand /><p>Платформа для спорта и развития.<br />Объединяем игроков, команды и возможности.</p></div><div className="home-footer-links"><a href="#top">Главная</a><a href="#capabilities">Возможности</a><a href="#families">Для родителей</a><a href="#coaches">Для тренеров</a></div><div className="home-footer-contact"><span>Свяжитесь с нами</span><a href="mailto:hello@kasanie.sport"><HomeIcon name="mail" />hello@kasanie.sport</a><a href="tel:+74951234567"><HomeIcon name="phone" />+7 (495) 123-45-67</a></div><div className="home-footer-social"><span>Мы в соцсетях</span><div><a href="#social-vk" aria-label="ВКонтакте"><HomeIcon name="vk" /></a><a href="#social-telegram" aria-label="Telegram"><HomeIcon name="telegram" /></a><a href="#social-instagram" aria-label="Instagram"><HomeIcon name="instagram" /></a></div></div></div><div className="home-footer-bottom"><span>© 2026 КАСАНИЕ</span><div><a href="#privacy">Политика конфиденциальности</a><i /><a href="#terms">Пользовательское соглашение</a></div></div></footer>
}

export function HomePage() {
  return <div className="home-page"><div className="home-frame"><Header /><main><Hero /><Audiences /><LegacyStats /></main><Footer /></div></div>
}
