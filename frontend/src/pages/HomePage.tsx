import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api'
import '../home.css'

const asset = (name: string) => `/brand/home-reference/${name}.webp`

const featureCards = [
  { icon: 'ball', image: 'ball', title: 'Найдите игру', copy: 'Матчи, сборы и спортивные активности рядом с вами.', link: '/sports' },
  { icon: 'calendar', image: 'board', title: 'Планируйте тренировки', copy: 'Персональные планы, упражнения и обратная связь тренера.', link: '/register' },
  { icon: 'trophy', image: 'trophy', title: 'Команды и турниры', copy: 'Создавайте события, собирайте участников и управляйте командой.', link: '/register-organizer' },
  { icon: 'trend', image: 'chart', title: 'Отслеживайте прогресс', copy: 'Оценка навыков, история занятий и динамика развития.', link: '/login' }
] as const

const audienceCards = [
  { image: 'child', title: 'Дети и родители', copy: 'Безопасная среда и понятный прогресс', link: '/register-parent' },
  { image: 'player', title: 'Игроки', copy: 'Активности, развитие и личный маршрут', link: '/register' },
  { image: 'coach', title: 'Тренеры', copy: 'Команды, занятия и обратная связь', link: '/register-coach' },
  { image: 'school', title: 'Школы и секции', copy: 'Тренеры, составы и единый процесс', link: '/login' }
] as const

type PlatformStats = {
  users: number
  teams: number
  tournaments: number
  coaches: number
  trustPercent: number | null
}

const numberFormatter = new Intl.NumberFormat('ru-RU')

function HomeIcon({ name }: { name: string }) {
  const common = { viewBox: '0 0 24 24', 'aria-hidden': true, focusable: false } as const
  if (name === 'ball') return <svg {...common}><circle cx="12" cy="12" r="9" /><path d="m12 8 3 2-1 4h-4l-1-4 3-2ZM9 10 5 9m10 1 4-1m-9 5-2 4m6-4 2 4M8 6l4 2 4-2" /></svg>
  if (name === 'calendar') return <svg {...common}><rect x="3" y="5" width="18" height="16" rx="3" /><path d="M7 3v4M17 3v4M3 10h18M8 14h3M8 17h7" /></svg>
  if (name === 'trophy') return <svg {...common}><path d="M8 4h8v5c0 3-1.7 5-4 5S8 12 8 9V4Zm4 10v4m-4 2h8M8 6H4v2c0 2 1 3 4 3m8-5h4v2c0 2-1 3-4 3" /></svg>
  if (name === 'trend') return <svg {...common}><path d="M4 19V5M4 19h16M7 15l4-5 3 3 5-7" /></svg>
  if (name === 'shield') return <svg {...common}><path d="M12 3 20 6v5c0 5-3 8-8 10-5-2-8-5-8-10V6Z" /><path d="m8.5 12 2.2 2.2 4.8-5" /></svg>
  if (name === 'people') return <svg {...common}><circle cx="8" cy="8" r="3" /><circle cx="17" cy="9" r="2.5" /><path d="M2.5 20c.4-5 2.2-7.5 5.5-7.5s5.1 2.5 5.5 7.5M14 14c3.8-.5 6 1.5 6.5 5" /></svg>
  if (name === 'coach') return <svg {...common}><circle cx="12" cy="7" r="3" /><path d="M6 21v-3c0-4 2-7 6-7s6 3 6 7v3M9 15h6M9 18h6" /></svg>
  if (name === 'chart') return <svg {...common}><path d="M4 19V8m5 11V4m5 15v-7m5 7V6" /></svg>
  return <svg {...common}><circle cx="12" cy="12" r="9" /><path d="m8 12 2.5 2.5L16 9" /></svg>
}

function Brand() {
  return <Link className="home-brand" to="/" aria-label="Касание — главная">
    <span className="home-brand-mark"><img src="/brand/kasanie-mark.webp" alt="" /></span>
    <strong>КАСАНИЕ</strong>
  </Link>
}

function Header() {
  const [open, setOpen] = useState(false)
  return <header className="home-header">
    <Brand />
    <button className="home-menu" type="button" aria-expanded={open} aria-controls="home-nav" onClick={() => setOpen(value => !value)}><span /><span /><span /><b>Меню</b></button>
    <nav id="home-nav" className={open ? 'open' : ''} aria-label="Навигация по главной странице">
      <a className="active" href="#top" onClick={() => setOpen(false)}>Главная</a>
      <a href="#capabilities" onClick={() => setOpen(false)}>Возможности</a>
      <a href="#families" onClick={() => setOpen(false)}>Для родителей</a>
      <a href="#coaches" onClick={() => setOpen(false)}>Для тренеров</a>
      <Link to="/sports">События</Link>
    </nav>
    <Link className="home-button small" to="/join">Начать</Link>
  </header>
}

function Hero() {
  return <section id="top" className="home-hero" aria-labelledby="home-title">
    <div className="home-hero-copy">
      <span className="home-kicker"><i>↗</i> Платформа для спорта и развития</span>
      <h1 id="home-title" aria-label="Спорт начинается с первого касания">Спорт начинается<br />с первого <em>касания</em></h1>
      <p>Найдите игру, тренера, команду и путь развития в одном месте. «Касание» — цифровая платформа для тех, кто живёт спортом.</p>
      <div className="home-actions"><Link className="home-button" to="/sports">Найти игру <span>↗</span></Link><Link className="home-button secondary" to="/join">Стать участником</Link></div>
      <div className="home-principles" aria-label="Принципы платформы">
        <span><i><HomeIcon name="shield" /></i><b>Безопасно</b><small>Разделение ролей и данных</small></span>
        <span><i><HomeIcon name="chart" /></i><b>Понятно</b><small>Динамика без лишних таблиц</small></span>
        <span><i><HomeIcon name="people" /></i><b>Для всех</b><small>Игроки, семьи и тренеры</small></span>
      </div>
    </div>
    <div className="home-hero-visual" aria-hidden="true"><img src={asset('hero')} alt="" fetchPriority="high" /></div>
  </section>
}

function Capabilities() {
  return <section id="capabilities" className="home-feature-grid" aria-label="Возможности платформы">
    {featureCards.map(card => <Link className={`home-feature-card visual-${card.image}`} to={card.link} key={card.title}>
      <i className="home-feature-icon"><HomeIcon name={card.icon} /></i>
      <div><h2>{card.title}</h2><p>{card.copy}</p><span>Подробнее →</span></div>
      <img src={asset(card.image)} alt="" loading="lazy" />
    </Link>)}
  </section>
}

function Audiences() {
  return <section className="home-audiences" aria-labelledby="audience-title">
    <div className="home-audience-intro"><h2 id="audience-title">Для кого Касание</h2><p>Платформа объединяет всех, кто вовлечён в спорт.</p></div>
    {audienceCards.map((card, index) => <Link id={index === 0 ? 'families' : index === 2 ? 'coaches' : undefined} className={`home-audience-card audience-${card.image}`} to={card.link} key={card.title} aria-label={`${card.title}: ${card.copy}`}>
      <div><h3>{card.title}</h3><p>{card.copy}</p></div><img src={asset(card.image)} alt="" loading="lazy" />
    </Link>)}
  </section>
}

function FactStrip() {
  const [stats, setStats] = useState<PlatformStats | null>(null)

  useEffect(() => {
    let active = true
    api<PlatformStats>('/api/public/platform-stats')
      .then(value => { if (active) setStats(value) })
      .catch(() => { /* Keep neutral placeholders when statistics are unavailable. */ })
    return () => { active = false }
  }, [])

  const facts = [
    { icon: 'people', value: stats ? numberFormatter.format(stats.users) : '—', label: 'пользователей' },
    { icon: 'ball', value: stats ? numberFormatter.format(stats.teams) : '—', label: 'команд' },
    { icon: 'trophy', value: stats ? numberFormatter.format(stats.tournaments) : '—', label: 'турниров организовано' },
    { icon: 'coach', value: stats ? numberFormatter.format(stats.coaches) : '—', label: 'тренеров' },
    { icon: 'shield', value: stats?.trustPercent == null ? '—' : `${numberFormatter.format(stats.trustPercent)}%`, label: 'доверие пользователей' }
  ]
  return <section className="home-facts" aria-label="Касание в цифрах">{facts.map(fact => <div key={fact.label}><i><HomeIcon name={fact.icon} /></i><span><strong>{fact.value}</strong><small>{fact.label}</small></span></div>)}</section>
}

function ProductStrip() {
  return <section className="home-product-strip" aria-labelledby="product-title">
    <div className="home-product-copy">
      <small>Всегда с вами</small>
      <h2 id="product-title">Приложение Касание</h2>
      <p>Управляйте тренировками, следите за прогрессом, общайтесь с командой и получайте уведомления в реальном времени.</p>
      <div className="home-store-badges" aria-label="Магазины приложений — скоро">
        <span><i>●</i><b><small>Загрузите в</small>App Store</b></span>
        <span><i>▶</i><b><small>Доступно в</small>Google Play</b></span>
        <span><i>◆</i><b><small>Откройте в</small>AppGallery</b></span>
      </div>
    </div>
    <div className="home-product-visual" role="img" aria-label="Макет мобильного приложения">
      <div className="home-phone" aria-hidden="true">
        <i className="home-phone-notch" />
        <div className="home-phone-head"><img src="/brand/kasanie-mark.webp" alt="" /><span>Привет, Артём!</span><b>•••</b></div>
        <small>У тебя 2 тренировки на сегодня</small>
        <div className="home-phone-session"><span>Ближайшая тренировка</span><strong>Сегодня, 18:00 – 19:30</strong><small>Стадион «Динамо»</small></div>
        <div className="home-phone-progress"><span>Мой прогресс</span><strong>845 <i>+62</i></strong><img src={asset('chart')} alt="" /></div>
      </div>
      <div className="home-progress-card" aria-hidden="true"><span>Мой прогресс</span><strong>845 <i>+62</i></strong><img src={asset('chart')} alt="" /></div>
    </div>
    <div className="home-qr-panel">
      <div className="home-qr-placeholder" role="img" aria-label="Место для QR-кода"><i /><i /><i /><span>QR</span></div>
      <div><strong>Сканируйте QR-код</strong><span>и установите приложение</span><small>QR-код будет добавлен перед запуском</small></div>
      <svg className="home-qr-arrow" viewBox="0 0 92 34" aria-hidden="true"><path d="M88 4C69 5 71 28 39 28H8m0 0 9-8M8 28l10 5" /></svg>
    </div>
  </section>
}

function Footer() {
  return <footer className="home-footer"><p>Касание — твой путь в <strong>спорте</strong></p></footer>
}

export function HomePage() {
  return <div className="home-page"><div className="home-frame"><Header /><main><Hero /><Capabilities /><Audiences /><FactStrip /><ProductStrip /></main><Footer /></div></div>
}
