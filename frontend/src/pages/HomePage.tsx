import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api'
import '../home.css'

type HomeActivity = {
  id: number
  slug: string
  sport: string
  gameFormat?: string
  eventType: string
  title: string
  startAt: string
  price: number
  availablePlaces: number
  venue: { city: string; district?: string; name: string }
}

type HomeSearchResult = { items: Array<{ activity: HomeActivity }> }

const audience = [
  { id: 'player', title: 'Игрок', copy: 'Находите активности, проходите оценку навыков и двигайтесь по персональному плану.', link: '/register', action: 'Создать профиль игрока' },
  { id: 'parent', title: 'Родитель', copy: 'Следите за прогрессом ребёнка, тренировками и обратной связью в одном кабинете.', link: '/register-parent', action: 'Создать кабинет родителя' },
  { id: 'coach', title: 'Тренер', copy: 'Ведите команды, планируйте занятия и наблюдайте динамику каждого игрока.', link: '/register-coach', action: 'Создать кабинет тренера' },
  { id: 'school', title: 'Школа / клуб', copy: 'Управляйте командами, тренерами и составами в едином защищённом рабочем пространстве.', link: '/login', action: 'Войти в кабинет' },
  { id: 'organizer', title: 'Организатор', copy: 'Публикуйте открытые игры, тренировки и турниры — участники найдут их в поиске.', link: '/register-organizer', action: 'Стать организатором' }
] as const

const features = [
  { icon: 'search', title: 'Активности рядом', copy: 'Игры и тренировки на карте с фильтрами по виду спорта, времени и формату.', link: '/sports', action: 'Открыть поиск' },
  { icon: 'route', title: 'Маршрут развития', copy: 'Оценка навыков превращается в понятный персональный план тренировок.', link: '/register', action: 'Начать развитие' },
  { icon: 'trend', title: 'Прогресс игрока', copy: 'Динамика навыков, история занятий и обратная связь без разрозненных таблиц.', link: '/login', action: 'Войти в кабинет' },
  { icon: 'team', title: 'Команды и тренеры', copy: 'Составы, командные занятия, журнал посещаемости и работа тренерского штаба.', link: '/register-coach', action: 'Для тренеров' },
  { icon: 'family', title: 'Семейный кабинет', copy: 'Родитель видит развитие ребёнка и сохраняет связь со спортивным процессом.', link: '/register-parent', action: 'Для родителей' },
  { icon: 'calendar', title: 'События и турниры', copy: 'Организаторы создают события, управляют участниками и точкой встречи.', link: '/register-organizer', action: 'Создать событие' }
] as const

const eventLabels: Record<string, string> = {
  Game: 'Игра', GroupTraining: 'Совместная тренировка', CoachTraining: 'С тренером',
  PlayerRecruitment: 'Ищу команду', Tournament: 'Турнир'
}

function HomeIcon({ name }: { name: string }) {
  const common = { viewBox: '0 0 24 24', 'aria-hidden': true, focusable: false } as const
  if (name === 'search') return <svg {...common}><circle cx="10.5" cy="10.5" r="6.5" /><path d="m15.5 15.5 5 5M8 10.5h5M10.5 8v5" /></svg>
  if (name === 'route') return <svg {...common}><circle cx="6" cy="18" r="2" /><circle cx="18" cy="6" r="2" /><path d="M8 18c7 0 1-12 8-12" /></svg>
  if (name === 'trend') return <svg {...common}><path d="M4 19V5M4 19h16M7 15l4-5 3 3 5-7" /></svg>
  if (name === 'team') return <svg {...common}><circle cx="8" cy="8" r="3" /><circle cx="17" cy="9" r="2.5" /><path d="M2.5 20c.4-5 2.2-7.5 5.5-7.5s5.1 2.5 5.5 7.5M14 14c3.8-.5 6 1.5 6.5 5" /></svg>
  if (name === 'family') return <svg {...common}><path d="M12 20s-7-4.4-7-10a4 4 0 0 1 7-2.6A4 4 0 0 1 19 10c0 5.6-7 10-7 10Z" /><circle cx="12" cy="11" r="2" /></svg>
  if (name === 'calendar') return <svg {...common}><rect x="3" y="5" width="18" height="16" rx="3" /><path d="M7 3v4M17 3v4M3 10h18M8 14h3M8 17h7" /></svg>
  if (name === 'shield') return <svg {...common}><path d="M12 3 20 6v5c0 5-3 8-8 10-5-2-8-5-8-10V6Z" /><path d="m8.5 12 2.2 2.2 4.8-5" /></svg>
  if (name === 'connection') return <svg {...common}><circle cx="6" cy="12" r="3" /><circle cx="18" cy="7" r="3" /><circle cx="18" cy="18" r="3" /><path d="m8.8 10.8 6.4-2.6M8.9 13.3l6.2 3.3" /></svg>
  return <svg {...common}><circle cx="12" cy="12" r="9" /><path d="m8 12 2.5 2.5L16 9" /></svg>
}

function Brand({ compact = false }: { compact?: boolean }) {
  return <Link className={`home-brand${compact ? ' compact' : ''}`} to="/" aria-label="Касание — главная">
    <span className="home-brand-mark"><img src="/brand/kasanie-logo.png" alt="" /></span>
    <span><strong>КАСАНИЕ</strong>{!compact && <small>спорт и развитие</small>}</span>
  </Link>
}

function HomeHeader() {
  const [open, setOpen] = useState(false)
  return <header className="home-header">
    <div className="home-header-inner">
      <Brand compact />
      <button className="home-menu-button" type="button" aria-expanded={open} aria-controls="home-navigation" onClick={() => setOpen(value => !value)}><span /><span /><span /><b>Меню</b></button>
      <nav id="home-navigation" className={open ? 'open' : ''} aria-label="Навигация по главной странице">
        <a href="#capabilities" onClick={() => setOpen(false)}>Возможности</a>
        <a href="#how" onClick={() => setOpen(false)}>Как это работает</a>
        <a href="#families" onClick={() => setOpen(false)}>Для семей</a>
        <a href="#coaches" onClick={() => setOpen(false)}>Для тренеров</a>
        <Link to="/sports">События</Link>
      </nav>
      <div className="home-header-actions"><Link className="home-login" to="/login">Войти</Link><Link className="home-button small" to="/join">Начать</Link></div>
    </div>
  </header>
}

function Hero() {
  return <section className="home-hero" aria-labelledby="home-title">
    <div className="home-hero-media" aria-hidden="true"><img src="/brand/home-hero-athlete.webp" alt="" fetchPriority="high" /><span className="touch-shape touch-shape-white" /><span className="touch-shape touch-shape-blue" /></div>
    <div className="home-hero-shade" />
    <div className="home-shell home-hero-content">
      <span className="home-kicker"><i>↗</i> Платформа для спорта и развития</span>
      <h1 id="home-title" aria-label="Спорт начинается с первого касания">Спорт начинается<br />с первого <em>касания</em></h1>
      <p>Находите игры рядом, тренируйтесь с командой и развивайтесь вместе с тренером. «Касание» соединяет людей и спортивный прогресс в одной платформе.</p>
      <div className="home-actions"><Link className="home-button" to="/sports">Найти активность <span>↗</span></Link><Link className="home-button secondary" to="/join">Выбрать свою роль</Link></div>
      <div className="home-principles" aria-label="Принципы платформы">
        <span><i><HomeIcon name="shield" /></i><b>Безопасно</b><small>Разделение ролей и данных</small></span>
        <span><i><HomeIcon name="trend" /></i><b>Понятно</b><small>Прогресс без лишних таблиц</small></span>
        <span><i><HomeIcon name="connection" /></i><b>Вместе</b><small>Игроки, семьи и тренеры</small></span>
      </div>
    </div>
  </section>
}

function AudienceSelector() {
  const [selected, setSelected] = useState<(typeof audience)[number]['id']>('player')
  const current = audience.find(item => item.id === selected) ?? audience[0]
  return <section className="home-section home-audience" aria-labelledby="audience-title">
    <div className="home-shell">
      <div className="home-section-heading compact"><span>Для кого</span><h2 id="audience-title">У каждого свой путь.<br />Платформа остаётся одной.</h2></div>
      <div className="home-audience-layout">
        <div className="home-role-tabs" role="tablist" aria-label="Выберите участника платформы">{audience.map(item => <button key={item.id} type="button" role="tab" aria-selected={selected === item.id} onClick={() => setSelected(item.id)}><span>{item.title}</span><i>↗</i></button>)}</div>
        <article className="home-role-detail" role="tabpanel"><small>Ваш сценарий</small><h3>{current.title}</h3><p>{current.copy}</p><Link to={current.link}>{current.action} <span>→</span></Link><div className="home-role-orbit" aria-hidden><i /><i /><i /></div></article>
      </div>
    </div>
  </section>
}

function FeatureSection() {
  return <section id="capabilities" className="home-section home-features" aria-labelledby="capabilities-title">
    <div className="home-shell">
      <div className="home-section-heading"><span>Возможности</span><h2 id="capabilities-title">От первой игры<br />до понятного прогресса</h2><p>Шесть рабочих сценариев, которые уже связаны с реальными разделами платформы.</p></div>
      <div className="home-feature-grid">{features.map((feature, index) => <Link className="home-feature-card" to={feature.link} key={feature.title}><div><i><HomeIcon name={feature.icon} /></i><small>0{index + 1}</small></div><h3>{feature.title}</h3><p>{feature.copy}</p><span>{feature.action} <b>→</b></span></Link>)}</div>
    </div>
  </section>
}

function HowItWorks() {
  const steps = [
    ['01', 'Выберите сценарий', 'Найдите открытую активность без регистрации или создайте кабинет нужной роли.'],
    ['02', 'Подключитесь', 'Запишитесь на игру, присоединитесь к команде или получите назначение от школы.'],
    ['03', 'Двигайтесь дальше', 'Тренируйтесь, сохраняйте обратную связь и наблюдайте динамику развития.']
  ]
  return <section id="how" className="home-section home-how" aria-labelledby="how-title"><div className="home-shell"><div className="home-section-heading centered"><span>Как это работает</span><h2 id="how-title">Три шага до действия</h2></div><div className="home-steps">{steps.map(([number, title, copy]) => <article key={number}><strong>{number}</strong><div><h3>{title}</h3><p>{copy}</p></div></article>)}</div></div></section>
}

function ProductPreview() {
  return <section className="home-product" aria-labelledby="product-title"><div className="home-shell home-product-layout"><div className="home-product-copy"><span>Продукт в действии</span><h2 id="product-title">Прогресс, который<br />можно объяснить</h2><p>Игрок видит следующий шаг, тренер получает обратную связь, а родитель понимает общую динамику — каждый в своём защищённом кабинете.</p><Link className="home-button" to="/register">Создать профиль <span>↗</span></Link></div><div className="home-dashboard-preview" aria-label="Пример интерфейса прогресса игрока"><header><div><i /><span>Профиль игрока</span></div><small>Последние 6 недель</small></header><div className="home-dashboard-main"><div className="home-score"><svg viewBox="0 0 120 120" aria-hidden="true"><circle cx="60" cy="60" r="49" /><circle className="progress" cx="60" cy="60" r="49" /></svg><span><b>72</b><small>уровень</small></span></div><div className="home-skill-bars">{[['Скорость',76],['Контроль мяча',81],['Выносливость',68]].map(([name, value]) => <div key={name}><span><b>{name}</b><small>{value}</small></span><i><em style={{ width: `${value}%` }} /></i></div>)}</div></div><footer><span><b>83%</b><small>плана выполнено</small></span><span><b>3</b><small>тренировки в неделю</small></span><span><b>+8</b><small>динамика уровня</small></span></footer><div className="home-chart" aria-hidden><svg viewBox="0 0 500 120" preserveAspectRatio="none"><path d="M0 104C72 100 84 77 144 83s74-26 130-17 78-43 124-35 66-11 102-25" /><path className="area" d="M0 104C72 100 84 77 144 83s74-26 130-17 78-43 124-35 66-11 102-25V120H0Z" /></svg></div></div></div></section>
}

function PeopleSections() {
  return <section className="home-section home-people"><div className="home-shell home-people-grid"><article id="families" className="home-people-card family"><span>Для семей</span><h2>Быть рядом —<br />не значит контролировать</h2><p>Семейный кабинет показывает тренировки и прогресс ребёнка понятным языком. Связь с профилем создаётся безопасно и отдельно от публичного поиска.</p><ul><li>Профиль ребёнка до 14 лет</li><li>История тренировок и динамика</li><li>Управление согласием на обработку данных</li></ul><Link to="/register-parent">Кабинет родителя <span>→</span></Link><div className="people-symbol" aria-hidden><HomeIcon name="family" /></div></article><article id="coaches" className="home-people-card coach"><span>Для тренеров</span><h2>Команда в фокусе.<br />Данные — по делу.</h2><p>Тренер ведёт состав, журнал занятий, упражнения и обратную связь. Доступ к игрокам появляется только после назначения школой.</p><ul><li>Команды и составы</li><li>План и журнал тренировок</li><li>Прогресс и самооценка игроков</li></ul><Link to="/register-coach">Кабинет тренера <span>→</span></Link><div className="people-symbol" aria-hidden><HomeIcon name="team" /></div></article></div></section>
}

function NearbyEvents() {
  const [activities, setActivities] = useState<HomeActivity[]>([])
  const [loading, setLoading] = useState(true)
  useEffect(() => { void api<HomeSearchResult>('/api/public/activities?sort=date').then(result => setActivities(result.items.slice(0, 3).map(item => item.activity))).catch(() => setActivities([])).finally(() => setLoading(false)) }, [])
  const formatDate = (value: string) => new Intl.DateTimeFormat('ru-RU', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' }).format(new Date(value))
  const formatPrice = (value: number) => value ? `${new Intl.NumberFormat('ru-RU').format(value)} ₽` : 'Бесплатно'
  return <section className="home-section home-events" aria-labelledby="events-title"><div className="home-shell"><div className="home-section-heading row"><div><span>Спорт рядом</span><h2 id="events-title">Следующая активность<br />может быть совсем близко</h2></div><Link className="home-button secondary dark" to="/sports">Все события <span>→</span></Link></div>{loading ? <div className="home-events-state">Ищем ближайшие активности…</div> : activities.length > 0 ? <div className="home-event-grid">{activities.map(activity => <Link to={`/activities/${activity.slug}`} className="home-event-card" key={activity.id}><div><span>{eventLabels[activity.eventType] ?? activity.eventType}</span><b>{formatDate(activity.startAt)}</b></div><h3>{activity.title}</h3><p>{activity.venue.city}{activity.venue.district ? ` · ${activity.venue.district}` : ''}<br />{activity.sport}{activity.gameFormat ? ` · ${activity.gameFormat}` : ''}</p><footer><strong>{formatPrice(activity.price)}</strong><span>{activity.availablePlaces > 0 ? `${activity.availablePlaces} мест` : 'Мест нет'} <b>↗</b></span></footer></Link>)}</div> : <div className="home-events-state"><b>Новых событий пока нет</b><p>Измените фильтры поиска или опубликуйте свою активность.</p><Link to="/register-organizer">Стать организатором →</Link></div>}</div></section>
}

function FinalCta() {
  return <section className="home-final"><div className="home-shell home-final-inner"><span className="touch-mini" aria-hidden><i /><b /></span><div><small>Первое касание</small><h2>Найдите свой следующий<br />шаг в спорте</h2></div><Link className="home-button light" to="/join">Начать <span>↗</span></Link></div></section>
}

function HomeFooter() {
  return <footer className="home-footer"><div className="home-shell home-footer-grid"><div><Brand /><p>Цифровая платформа для спорта, людей и развития.</p></div><nav aria-label="Разделы платформы"><strong>Платформа</strong><a href="#capabilities">Возможности</a><a href="#how">Как это работает</a><Link to="/sports">Активности</Link></nav><nav aria-label="Кабинеты"><strong>Участникам</strong><Link to="/register">Игрокам</Link><Link to="/register-parent">Родителям</Link><Link to="/register-coach">Тренерам</Link><Link to="/register-organizer">Организаторам</Link></nav><nav aria-label="Доступ"><strong>Аккаунт</strong><Link to="/login">Войти</Link><Link to="/join">Выбрать роль</Link></nav></div><div className="home-shell home-footer-bottom"><span>© 2026 Касание</span><span>DEMO-нормы не являются научно валидированными</span></div></footer>
}

export function HomePage() {
  return <div className="home-page"><HomeHeader /><main><Hero /><AudienceSelector /><FeatureSection /><HowItWorks /><ProductPreview /><PeopleSections /><NearbyEvents /><FinalCta /></main><HomeFooter /></div>
}
