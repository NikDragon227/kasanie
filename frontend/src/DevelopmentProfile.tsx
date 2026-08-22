import { EmptyState, ErrorState, PageLoader } from './components'
import { formatDate, useApiData } from './hooks'
import type { CSSProperties } from 'react'

export type DevelopmentSummary = {
  generatedAt: string
  completedTeamTrainings: number
  attended: number
  late: number
  absent: number
  excused: number
  attendanceRate: number
  completionRate: number
  understandingRate: number
  categories: { key: string; name: string; exercises: number; completionRate: number; understandingRate: number; attentionCount: number }[]
  focusAreas: { skillCategory: string; name: string; attentionCount: number; lastSeenAt: string }[]
  trend: { trainingId: number; date: string; completionRate: number; understandingRate: number }[]
  recentTrainings: { trainingId: number; title: string; team: string; school: string; scheduledAt: string; attendance: string; completionRate: number; understandingRate: number; exercises: { exerciseId: number; name: string; skillCategory: string; isCompleted: boolean; understood: boolean }[] }[]
}

const attendanceLabel: Record<string, string> = { Present: 'Был', Late: 'Опоздал', Absent: 'Не был', Excused: 'Уважительная причина' }

export function DevelopmentProfile({ endpoint, title = 'Профиль развития' }: { endpoint: string | null; title?: string }) {
  const state = useApiData<DevelopmentSummary>(endpoint)
  if (state.loading) return <section className="card development-shell"><PageLoader /></section>
  if (state.error || !state.data) return <section className="card development-shell"><ErrorState message={state.error} retry={state.reload} /></section>
  const data = state.data
  if (!data.completedTeamTrainings) return <section className="card development-shell"><div className="card-heading"><div><span className="eyebrow">Командные занятия</span><h2>{title}</h2></div></div><EmptyState title="Динамика появится после первой завершённой тренировки">Посещаемость и отметки «сделал / понял» сформируют понятную историю развития.</EmptyState></section>
  return <section className="development-profile">
    <div className="card development-overview">
      <div className="card-heading"><div><span className="eyebrow">Командные занятия</span><h2>{title}</h2><p>Фактическая работа на поле, а не ручная оценка задним числом.</p></div><span className="development-total">{data.completedTeamTrainings} занятий</span></div>
      <div className="development-score-grid">
        <ScoreRing label="Посещаемость" value={data.attendanceRate} detail={`${data.attended} посещено`} />
        <ScoreRing label="Выполнение" value={data.completionRate} detail="упражнения сделаны" tone="mint" />
        <ScoreRing label="Понимание" value={data.understandingRate} detail="материал понят" tone="blue" />
      </div>
      <div className="attendance-summary"><span><b>{data.late}</b> опозданий</span><span><b>{data.absent}</b> пропусков</span><span><b>{data.excused}</b> уважительных</span></div>
    </div>
    <div className="development-grid">
      <section className="card development-trend"><div className="card-heading"><div><span className="eyebrow">Последние занятия</span><h2>Динамика работы</h2></div><div className="trend-legend"><span className="done">Выполнение</span><span className="understood">Понимание</span></div></div><DevelopmentTrend data={data.trend} /></section>
      <section className="card focus-panel"><div className="card-heading"><div><span className="eyebrow">Следующий шаг</span><h2>Зоны внимания</h2></div></div>{data.focusAreas.length ? <div className="focus-list">{data.focusAreas.map(x => <article key={x.skillCategory}><span>{x.attentionCount}</span><div><strong>{x.name}</strong><small>Последняя отметка {formatDate(x.lastSeenAt)}</small></div></article>)}</div> : <div className="all-clear"><b>✓</b><div><strong>Всё усвоено</strong><span>На завершённых занятиях нет отметок, требующих повторения.</span></div></div>}</section>
    </div>
    <section className="card category-panel"><div className="card-heading"><div><span className="eyebrow">Структура подготовки</span><h2>Развитие по навыкам</h2></div></div><div className="category-development">{data.categories.map(x => <article key={x.key}><div><strong>{x.name}</strong><small>{x.exercises} упражнений{x.attentionCount ? ` · ${x.attentionCount} требуют внимания` : ''}</small></div><MetricBar label="Сделал" value={x.completionRate} /><MetricBar label="Понял" value={x.understandingRate} tone="understood" /></article>)}</div></section>
    <section className="card development-history"><div className="card-heading"><div><span className="eyebrow">История</span><h2>Последние тренировки</h2></div></div><div className="development-timeline">{data.recentTrainings.map(x => <article key={x.trainingId}><div className="timeline-date"><strong>{new Date(x.scheduledAt).getDate()}</strong><span>{new Intl.DateTimeFormat('ru-RU', { month: 'short' }).format(new Date(x.scheduledAt))}</span></div><div className="timeline-main"><div><span className={`attendance-pill ${x.attendance.toLowerCase()}`}>{attendanceLabel[x.attendance] ?? x.attendance}</span><h3>{x.title}</h3><small>{x.school} · {x.team}</small></div>{x.exercises.length > 0 && <div className="exercise-result-list">{x.exercises.map(item => <span className={!item.isCompleted || !item.understood ? 'attention' : ''} key={item.exerciseId}>{!item.isCompleted ? 'Не сделал' : !item.understood ? 'Повторить' : 'Усвоено'} · {item.name}</span>)}</div>}</div><div className="timeline-scores"><span><b>{x.completionRate}%</b> сделал</span><span><b>{x.understandingRate}%</b> понял</span></div></article>)}</div></section>
  </section>
}

function ScoreRing({ label, value, detail, tone = '' }: { label: string; value: number; detail: string; tone?: string }) {
  return <article className={`development-score ${tone}`}><div style={{ '--score': `${value * 3.6}deg` } as CSSProperties}><span>{value}<small>%</small></span></div><strong>{label}</strong><small>{detail}</small></article>
}

function MetricBar({ label, value, tone = '' }: { label: string; value: number; tone?: string }) {
  return <div className={`development-bar ${tone}`}><div><span>{label}</span><b>{value}%</b></div><i><span style={{ width: `${value}%` }} /></i></div>
}

function DevelopmentTrend({ data }: { data: DevelopmentSummary['trend'] }) {
  if (!data.length) return <EmptyState title="Пока недостаточно данных" />
  const width = 600, height = 210, inset = 24
  const x = (index: number) => data.length === 1 ? width / 2 : inset + index * (width - inset * 2) / (data.length - 1)
  const y = (value: number) => height - inset - value * (height - inset * 2) / 100
  const points = (key: 'completionRate' | 'understandingRate') => data.map((item, index) => `${x(index)},${y(item[key])}`).join(' ')
  return <div className="development-chart"><svg viewBox={`0 0 ${width} ${height}`} role="img" aria-label="Динамика выполнения и понимания упражнений">{[0, 25, 50, 75, 100].map(value => <g key={value}><line x1={inset} y1={y(value)} x2={width - inset} y2={y(value)} /><text x={inset} y={y(value) - 5}>{value}%</text></g>)}<polyline className="completion-line" points={points('completionRate')} /><polyline className="understanding-line" points={points('understandingRate')} />{data.map((item, index) => <g key={item.trainingId}><circle className="completion-dot" cx={x(index)} cy={y(item.completionRate)} r="4" /><circle className="understanding-dot" cx={x(index)} cy={y(item.understandingRate)} r="4" /></g>)}</svg><div className="chart-dates">{data.map(x => <span key={x.trainingId}>{new Intl.DateTimeFormat('ru-RU', { day: '2-digit', month: '2-digit' }).format(new Date(x.date))}</span>)}</div></div>
}
