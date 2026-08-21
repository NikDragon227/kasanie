import { useEffect, useState, type FormEvent } from 'react'
import { Link, Navigate, useLocation, useNavigate, useSearchParams } from 'react-router-dom'
import { ApiError, post } from '../api'
import { useAuth } from '../auth'
import { CityInput } from '../CityInput'

const roleHome: Record<string, string> = { Player: '/player', Coach: '/coach', Parent: '/parent', RegionalAnalyst: '/analytics', Admin: '/admin' }
const demoAccounts = [
  { role: 'Игрок', email: 'player@kasanie.local' },
  { role: 'Тренер', email: 'coach@kasanie.local' },
  { role: 'Родитель', email: 'parent@kasanie.local' },
  { role: 'Региональный аналитик', email: 'analyst@kasanie.local' },
  { role: 'Администратор', email: 'admin@kasanie.local' }
]
const demoPassword = 'Kasanie-Demo-2026!'

export function LandingPage() {
  const skills = [['Скорость', 76], ['Выносливость', 68], ['Контроль мяча', 81]] as const
  const journey = [
    ['01', 'Оцени себя', 'Короткие полевые тесты превращают ощущения в понятную точку отсчёта.'],
    ['02', 'Получи маршрут', 'План собирается вокруг навыков, которым сейчас нужен приоритет.'],
    ['03', 'Тренируйся в ритме', 'Каждая сессия понятна: цель, упражнения, нагрузка и обратная связь.'],
    ['04', 'Види рост', 'Динамика навыков показывает, что уже работает и куда двигаться дальше.'],
  ] as const
  return <div className="landing landing-v2">
    <header className="landing-nav landing-nav-v2">
      <Link className="brand" to="/"><span className="brand-mark">К</span><span><strong>КАСАНИЕ</strong><small>футбольное развитие</small></span></Link>
      <nav aria-label="Навигация по странице"><a href="#product">Платформа</a><a href="#how">Как работает</a><a href="#roles">Для кого</a><Link className="button ghost" to="/login">Войти</Link><Link className="button" to="/register">Начать</Link></nav>
    </header>
    <main>
      <section className="landing-hero-v2">
        <div className="landing-hero-copy">
          <span className="signal-pill"><i /> Персональная система развития игрока</span>
          <h1>Не просто тренируйся.<br /><em>Понимай, что меняет игру.</em></h1>
          <p>«Касание» соединяет оценку навыков, персональный план и обратную связь тренера в один понятный маршрут.</p>
          <div className="landing-hero-actions"><Link className="button large" to="/register">Начать с оценки <span>↗</span></Link><a className="text-action" href="#product">Посмотреть платформу <span>↓</span></a></div>
          <div className="landing-facts" aria-label="Возможности платформы"><span><strong>6</strong> ключевых навыков</span><span><strong>1</strong> персональный маршрут</span><span><strong>3</strong> связанных кабинета</span></div>
        </div>
        <div className="performance-stage" aria-label="Пример интерфейса игрока">
          <div className="stage-glow" />
          <article className="performance-board">
            <header><div><span className="live-dot" /> Карта игрока</div><small>Демо-профиль · сегодня</small></header>
            <div className="performance-main">
              <div className="score-dial"><svg viewBox="0 0 120 120" aria-hidden><circle cx="60" cy="60" r="51" /><circle className="score-progress" cx="60" cy="60" r="51" /></svg><span><strong>72</strong><small>уровень</small></span></div>
              <div className="skill-preview"><span className="eyebrow">Текущая форма</span>{skills.map(([name, value]) => <div className="skill-preview-row" key={name}><div><span>{name}</span><strong>{value}</strong></div><i><b style={{ width: `${value}%` }} /></i></div>)}</div>
            </div>
            <div className="performance-stats"><span><i>↗</i><b>+8</b><small>за 6 недель</small></span><span><i>✓</i><b>83%</b><small>плана выполнено</small></span><span><i>●</i><b>3</b><small>сессии в неделю</small></span></div>
          </article>
          <article className="next-session-card"><header><span>Следующая тренировка</span><b>48 мин</b></header><h3>Скорость и первый шаг</h3><div className="session-sequence"><span className="done">✓</span><i /><span>02</span><i /><span>03</span></div><small>3 упражнения · средняя нагрузка</small></article>
          <article className="coach-insight-card"><span>Комментарий тренера</span><p>«На старте держи корпус ниже — ускорение станет резче»</p><div><span className="avatar">А</span><small>Алексей · тренер</small></div></article>
        </div>
      </section>

      <section className="audience-rail" aria-label="Участники платформы"><span>Один контур развития</span><div><b>Игрок</b><i>→</i><b>Тренер</b><i>→</i><b>Родитель</b><i>→</i><b>Регион</b></div></section>

      <section id="how" className="journey-section">
        <div className="section-heading"><span className="eyebrow">Система вместо догадок</span><h2>Каждая тренировка отвечает на вопрос: <em>что делать дальше?</em></h2><p>Минимум лишних экранов. Максимум ясности на каждом этапе развития.</p></div>
        <div className="journey-grid">{journey.map(([number, title, description]) => <article key={number}><span>{number}</span><div><h3>{title}</h3><p>{description}</p></div></article>)}</div>
      </section>

      <section id="product" className="product-section">
        <div className="section-heading light"><span className="eyebrow">Продукт в действии</span><h2>Не набор цифр.<br /><em>Понятный следующий шаг.</em></h2></div>
        <div className="product-bento">
          <article className="bento-card bento-assessment"><div className="bento-copy"><span className="card-index">01 / Оценка</span><h3>Честная точка отсчёта</h3><p>Шесть футбольных навыков складываются в профиль, который легко прочитать без таблиц и терминов.</p></div><div className="profile-radar" aria-hidden><svg viewBox="0 0 220 190"><polygon points="110,16 189,61 189,136 110,180 31,136 31,61" /><polygon points="110,42 166,74 166,124 110,155 54,124 54,74" /><polygon className="radar-shape" points="110,31 172,78 154,122 110,164 47,130 62,70" />{[[110,31],[172,78],[154,122],[110,164],[47,130],[62,70]].map(([cx,cy]) => <circle key={`${cx}-${cy}`} cx={cx} cy={cy} r="4" />)}</svg><span className="radar-label speed">Скорость</span><span className="radar-label control">Контроль</span><span className="radar-label stamina">Выносливость</span></div></article>
          <article className="bento-card bento-plan"><span className="card-index">02 / План</span><h3>Неделя уже собрана</h3><div className="mini-calendar">{[['ПН','Скорость','done'],['СР','Контроль','active'],['СБ','Игра','']].map(([day,label,state]) => <div className={state} key={day}><b>{day}</b><span>{label}</span><i>{state === 'done' ? '✓' : state === 'active' ? '→' : '·'}</i></div>)}</div></article>
          <article className="bento-card bento-feedback"><span className="card-index">03 / Связь</span><h3>Обратная связь не теряется</h3><div className="feedback-bubble"><span className="avatar">А</span><p>Хороший темп. На следующей сессии добавим работу слабой ногой.</p></div><small>Комментарий привязан к тренировке</small></article>
          <article className="bento-card bento-progress"><span className="card-index">04 / Прогресс</span><div><h3>Рост виден в динамике</h3><p>Игрок понимает результат, тренер — корректирует нагрузку.</p></div><div className="trend-chart" aria-hidden><span className="trend-value">+11%</span><svg viewBox="0 0 340 130" preserveAspectRatio="none"><path className="trend-area" d="M0 115 C55 108 66 93 112 97 S174 73 212 78 S271 28 340 18 L340 130 L0 130 Z" /><path className="trend-line" d="M0 115 C55 108 66 93 112 97 S174 73 212 78 S271 28 340 18" /></svg></div></article>
        </div>
      </section>

      <section id="roles" className="roles-section-v2">
        <div className="section-heading"><span className="eyebrow">Все смотрят в одну сторону</span><h2>Одна система.<br />Разные уровни внимания.</h2><p>Каждый видит только то, что помогает ему принять следующее решение.</p></div>
        <div className="role-cards"><article><span>01</span><i>↗</i><h3>Игрок</h3><p>План на сегодня, понятные упражнения и собственная динамика.</p></article><article><span>02</span><i>◎</i><h3>Тренер</h3><p>Состояние игроков, обратная связь и корректировка программ.</p></article><article><span>03</span><i>○</i><h3>Родитель</h3><p>Прогресс ребёнка без лишней спортивной аналитики.</p></article><article><span>04</span><i>▦</i><h3>Регион</h3><p>Обезличенная картина вовлечённости и развития.</p></article></div>
      </section>

      <section className="landing-cta"><div><span className="eyebrow">Твоя точка отсчёта</span><h2>Сильная игра начинается<br />с понятного первого шага.</h2></div><div><p>Пройди оценку и получи персональный маршрут развития.</p><Link className="button large" to="/register">Начать бесплатно <span>↗</span></Link></div></section>
    </main>
    <footer className="landing-footer-v2"><Link className="brand" to="/"><span className="brand-mark">К</span><span><strong>КАСАНИЕ</strong><small>футбольное развитие</small></span></Link><div><a href="#how">Как работает</a><a href="#roles">Для кого</a><Link to="/login">Войти</Link></div><span>© 2026 · DEMO-нормы не являются научно валидированными</span></footer>
  </div>
}

export function LoginPage() {
  const { user } = useAuth()
  if (user) return <RoleRedirect roles={user.roles} />
  return <AuthFrame title="С возвращением" subtitle="Войдите — ваш план и прогресс уже на месте."><LoginForm /></AuthFrame>
}

export function LoginForm() {
  const { login } = useAuth(); const navigate = useNavigate(); const location = useLocation()
  const [email, setEmail] = useState(''); const [password, setPassword] = useState(''); const [show, setShow] = useState(false); const [error, setError] = useState(''); const [pending, setPending] = useState(false)
  const submit = async (event: FormEvent) => {
    event.preventDefault(); setError('')
    if (!email.includes('@')) return setError('Укажите корректный email.')
    if (!password) return setError('Введите пароль.')
    setPending(true)
    try { const next = await login(email, password); const from = (location.state as { from?: string } | null)?.from; navigate(from ?? roleHome[next.roles[0]] ?? '/') }
    catch (e) { setError(e instanceof ApiError && (e.status === 423 || e.status === 403) ? e.message : 'Неверный email или пароль.') }
    finally { setPending(false) }
  }
  const showDemoAccounts = ['localhost', '127.0.0.1'].includes(window.location.hostname)
  return <form className="auth-form" onSubmit={submit} noValidate><label>Email<input type="email" autoComplete="email" value={email} onChange={e => setEmail(e.target.value)} placeholder="you@example.ru" /></label><label>Пароль<span className="password-control"><input type={show ? 'text' : 'password'} autoComplete="current-password" value={password} onChange={e => setPassword(e.target.value)} /><button type="button" className="password-toggle" onClick={() => setShow(x => !x)} aria-pressed={show}>{show ? 'Скрыть' : 'Показать'}</button></span></label>{error && <div className="form-error" role="alert">{error}</div>}<button className="button large" disabled={pending}>{pending ? 'Входим…' : 'Войти'}</button><p><Link to="/forgot-password">Не помню пароль</Link></p><p>Нет аккаунта? <Link to="/register">Зарегистрироваться</Link></p>{showDemoAccounts && <div className="demo-credentials"><strong>Демо-вход</strong><small>Выберите роль — поля заполнятся автоматически.</small><div className="demo-account-list">{demoAccounts.map(account => <button type="button" key={account.email} onClick={() => { setEmail(account.email); setPassword(demoPassword) }}><b>{account.role}</b><span>{account.email}</span></button>)}</div><span className="demo-password">Пароль для всех: {demoPassword}</span></div>}</form>
}

export function RegisterPage() {
  const [error, setError] = useState(''); const [done, setDone] = useState(false); const [show, setShow] = useState(false); const [pending, setPending] = useState(false)
  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault(); setError(''); const data = new FormData(event.currentTarget); const birth = String(data.get('dateOfBirth')); const age = new Date().getFullYear() - new Date(birth).getFullYear()
    if (!birth || age < 14) return setError('Игроку младше 14 лет профиль создаёт родитель из своего кабинета.')
    if (String(data.get('password')).length < 8) return setError('Пароль должен содержать не менее 8 символов.')
    setPending(true)
    try { await post('/api/auth/register', { email: data.get('email'), password: data.get('password'), dateOfBirth: birth, firstName: data.get('firstName'), lastName: data.get('lastName'), city: data.get('city'), preferredPosition: data.get('preferredPosition'), dominantFoot: data.get('dominantFoot'), experienceLevel: data.get('experienceLevel') }); setDone(true) }
    catch (e) { setError(e instanceof Error ? e.message : 'Регистрация не выполнена.') } finally { setPending(false) }
  }
  if (done) return <AuthFrame title="Профиль создан" subtitle="Подтвердите email по ссылке из письма, затем войдите."><Link className="button large" to="/login">Перейти ко входу</Link></AuthFrame>
  return <AuthFrame title="Начни свою траекторию" subtitle="Регистрация доступна игрокам от 14 лет"><form className="auth-form two-column" onSubmit={submit}><label>Имя<input name="firstName" required /></label><label>Фамилия<input name="lastName" required /></label><label>Дата рождения<input name="dateOfBirth" type="date" required /></label><label>Город<CityInput required /></label><label>Email<input name="email" type="email" required /></label><label>Пароль<span className="password-control"><input name="password" type={show ? 'text' : 'password'} autoComplete="new-password" minLength={8} required /><button type="button" className="password-toggle" onClick={() => setShow(x => !x)} aria-pressed={show}>{show ? 'Скрыть' : 'Показать'}</button></span><small>Не менее 8 символов: строчная и заглавная буквы, цифра и специальный знак.</small></label><label>Позиция<select name="preferredPosition"><option>Полузащитник</option><option>Нападающий</option><option>Защитник</option><option>Вратарь</option></select></label><label>Ведущая нога<select name="dominantFoot"><option>Правая</option><option>Левая</option><option>Обе</option></select></label><label>Опыт<select name="experienceLevel"><option>Начинающий</option><option>Любитель</option><option>Опытный</option></select></label>{error && <div className="form-error full" role="alert">{error}</div>}<button className="button large full" disabled={pending}>{pending ? 'Создаём…' : 'Создать аккаунт'}</button><p className="full">Уже есть аккаунт? <Link to="/login">Войти</Link></p></form></AuthFrame>
}

export function ForgotPasswordPage() {
  const [message, setMessage] = useState(''); const [pending, setPending] = useState(false)
  const submit = async (event: FormEvent<HTMLFormElement>) => { event.preventDefault(); const email = String(new FormData(event.currentTarget).get('email')); setPending(true); try { const result = await post<{ message: string }>('/api/auth/forgot-password', { email }); setMessage(result.message) } catch { setMessage('Не удалось отправить запрос. Попробуйте позже.') } finally { setPending(false) } }
  return <AuthFrame title="Восстановить пароль" subtitle="Мы отправим ссылку на подтверждённый email."><form className="auth-form" onSubmit={submit}><label>Email<input name="email" type="email" autoComplete="email" required /></label><button className="button large" disabled={pending}>{pending ? 'Отправляем…' : 'Отправить ссылку'}</button>{message && <div className="success-message" role="status">{message}</div>}<p><Link to="/login">Вернуться ко входу</Link></p></form></AuthFrame>
}

export function ResetPasswordPage() {
  const [params] = useSearchParams(); const [status, setStatus] = useState<{ text: string; ok: boolean } | null>(null); const [passwordError, setPasswordError] = useState(''); const [show, setShow] = useState(false); const [pending, setPending] = useState(false)
  const email = params.get('email') ?? ''; const token = params.get('token') ?? ''
  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const password = String(new FormData(event.currentTarget).get('password'))
    setStatus(null)
    setPasswordError('')
    if (password.length < 8) return setPasswordError('Пароль должен содержать не менее 8 символов.')
    setPending(true)
    try {
      const result = await post<{ message: string }>('/api/auth/reset-password', { email, token, newPassword: password })
      setStatus({ text: result.message, ok: true })
    } catch (e) {
      const errors = e instanceof ApiError ? e.body.errors as Record<string, unknown> | undefined : undefined
      const fieldErrors = errors?.newPassword
      if (Array.isArray(fieldErrors) && fieldErrors.every(x => typeof x === 'string')) setPasswordError(fieldErrors.join(' '))
      else setStatus({ text: e instanceof Error ? e.message : 'Не удалось обновить пароль.', ok: false })
    } finally {
      setPending(false)
    }
  }
  if (!email || !token) return <AuthFrame title="Ссылка недействительна" subtitle="Запросите восстановление пароля ещё раз."><Link className="button large" to="/forgot-password">Восстановить пароль</Link></AuthFrame>
  return <AuthFrame title="Новый пароль" subtitle="Не менее 8 символов: строчная и заглавная буквы, цифра и специальный знак."><form className="auth-form" onSubmit={submit}><label>Новый пароль<span className="password-control"><input name="password" type={show ? 'text' : 'password'} autoComplete="new-password" minLength={8} aria-invalid={Boolean(passwordError)} aria-describedby={passwordError ? 'password-requirements password-error' : 'password-requirements'} required /><button type="button" className="password-toggle" onClick={() => setShow(x => !x)} aria-pressed={show}>{show ? 'Скрыть' : 'Показать'}</button></span><small id="password-requirements">Например: Kasanie-2026!</small>{passwordError && <span id="password-error" className="error-message" role="alert">{passwordError}</span>}</label><button className="button large" disabled={pending}>{pending ? 'Сохраняем…' : 'Сохранить пароль'}</button>{status && <div className={status.ok ? 'success-message' : 'form-error'} role={status.ok ? 'status' : 'alert'}>{status.text}</div>}<p><Link to="/login">Ко входу</Link></p></form></AuthFrame>
}

export function ConfirmEmailPage() {
  const [params] = useSearchParams(); const [message, setMessage] = useState('Подтверждаем email…'); const [done, setDone] = useState(false); const userId = params.get('userId') ?? ''; const token = params.get('token') ?? ''
  useEffect(() => { if (!userId || !token) { setMessage('Ссылка недействительна или устарела.'); return } void post<{ message: string }>('/api/auth/confirm-email', { userId, token }).then(x => { setMessage(x.message); setDone(true) }).catch(e => setMessage(e instanceof Error ? e.message : 'Ссылка недействительна или устарела.')) }, [userId, token])
  return <AuthFrame title="Подтверждение email" subtitle={message}>{done && <Link className="button large" to="/login">Войти</Link>}</AuthFrame>
}

function AuthFrame({ title, subtitle, children }: { title: string; subtitle: string; children: React.ReactNode }) {
  const isLogin = title === 'С возвращением'
  return <div className={`auth-page auth-page-v2${isLogin ? ' auth-page-login' : ''}`}>
    <section className="auth-story">
      <Link className="brand" to="/"><span className="brand-mark">К</span><span><strong>КАСАНИЕ</strong><small>футбольное развитие</small></span></Link>
      <div className="auth-story-copy"><span className="signal-pill"><i /> Система развития игрока</span><h2>Твой прогресс.<br /><em>В одном маршруте.</em></h2><p>Оценка навыков, персональные тренировки и связь с тренером — без разрозненных таблиц и чатов.</p></div>
      <div className="auth-preview" aria-label="Пример прогресса игрока">
        <header><div><span className="live-dot" /> Форма игрока</div><small>Последние 6 недель</small></header>
        <div className="auth-preview-main"><div className="auth-score"><strong>72</strong><span>общий уровень</span><small>↗ +8</small></div><div className="auth-skills"><div><span>Скорость</span><b>76</b><i><em style={{ width: '76%' }} /></i></div><div><span>Контроль мяча</span><b>81</b><i><em style={{ width: '81%' }} /></i></div><div><span>Выносливость</span><b>68</b><i><em style={{ width: '68%' }} /></i></div></div></div>
        <div className="auth-preview-footer"><span><b>83%</b><small>плана выполнено</small></span><span><b>3</b><small>тренировки в неделю</small></span><span><b>1</b><small>следующий шаг</small></span></div>
      </div>
      <div className="auth-route"><span><b>01</b> Оценка</span><i>→</i><span><b>02</b> План</span><i>→</i><span><b>03</b> Прогресс</span></div>
    </section>
    <section className="auth-panel auth-panel-v2">
      <Link className="auth-home-link" to="/">← На главную</Link>
      <div className="auth-inner"><span className="eyebrow">Личный кабинет</span><h1 className={isLogin ? 'auth-title-login' : undefined}>{title}</h1><p>{subtitle}</p>{children}</div>
      <small className="auth-legal">Продолжая, вы соглашаетесь с правилами обработки данных.</small>
    </section>
  </div>
}
function RoleRedirect({ roles }: { roles: string[] }) { return <Navigate to={roleHome[roles[0]] ?? '/'} replace /> }
