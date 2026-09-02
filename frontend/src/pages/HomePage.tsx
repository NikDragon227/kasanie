import { useState } from 'react'
import { Link } from 'react-router-dom'
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

function HomeIcon({ name }: { name: string }) {
  const common = { viewBox: '0 0 24 24', 'aria-hidden': true, focusable: false } as const
  if (name === 'ball') return <svg {...common}><circle cx="12" cy="12" r="9" /><path d="m12 8 3 2-1 4h-4l-1-4 3-2ZM9 10 5 9m10 1 4-1m-9 5-2 4m6-4 2 4M8 6l4 2 4-2" /></svg>
  if (name === 'calendar') return <svg {...common}><rect x="3" y="5" width="18" height="16" rx="3" /><path d="M7 3v4M17 3v4M3 10h18M8 14h3M8 17h7" /></svg>
  if (name === 'trophy') return <svg {...common}><path d="M8 4h8v5c0 3-1.7 5-4 5S8 12 8 9V4Zm4 10v4m-4 2h8M8 6H4v2c0 2 1 3 4 3m8-5h4v2c0 2-1 3-4 3" /></svg>
  if (name === 'trend') return <svg {...common}><path d="M4 19V5M4 19h16M7 15l4-5 3 3 5-7" /></svg>
  if (name === 'shield') return <svg {...common}><path d="M12 3 20 6v5c0 5-3 8-8 10-5-2-8-5-8-10V6Z" /><path d="m8.5 12 2.2 2.2 4.8-5" /></svg>
  if (name === 'people') return <svg {...common}><circle cx="8" cy="8" r="3" /><circle cx="17" cy="9" r="2.5" /><path d="M2.5 20c.4-5 2.2-7.5 5.5-7.5s5.1 2.5 5.5 7.5M14 14c3.8-.5 6 1.5 6.5 5" /></svg>
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
  const facts = [
    { icon: 'ball', value: '8', label: 'видов спорта' },
    { icon: 'people', value: '5', label: 'ролей платформы' },
    { icon: 'trophy', value: '6', label: 'рабочих сценариев' },
    { icon: 'shield', value: '24/7', label: 'поиск активностей' }
  ]
  return <section className="home-facts" aria-label="Возможности Касания">{facts.map(fact => <div key={fact.label}><i><HomeIcon name={fact.icon} /></i><span><strong>{fact.value}</strong><small>{fact.label}</small></span></div>)}</section>
}

function ProductStrip() {
  return <section className="home-product-strip" aria-labelledby="product-title">
    <div className="home-product-copy"><small>Всегда с вами</small><h2 id="product-title">Вся платформа<br />в одном кабинете</h2><p>Находите активности, следите за прогрессом и оставайтесь на связи со спортивным процессом.</p><div><Link to="/login">Войти</Link><Link to="/join">Выбрать роль →</Link></div></div>
    <div className="home-product-visual" aria-hidden="true"><img className="home-product-child" src={asset('kids')} alt="" loading="lazy" /><div className="home-mini-dashboard"><span>Мой прогресс</span><strong>72</strong><small>следующий шаг уже определён</small><img src={asset('chart')} alt="" /></div></div>
    <div className="home-product-action"><i><HomeIcon name="ball" /></i><strong>Найдите игру рядом</strong><span>Фильтры по городу, времени, спорту и формату.</span><Link className="home-button" to="/sports">Открыть поиск <b>↗</b></Link></div>
  </section>
}

function Footer() {
  return <footer className="home-footer"><p>Касание — твой путь в <strong>спорте</strong></p></footer>
}

export function HomePage() {
  return <div className="home-page"><div className="home-frame"><Header /><main><Hero /><Capabilities /><Audiences /><FactStrip /><ProductStrip /></main><Footer /></div></div>
}
