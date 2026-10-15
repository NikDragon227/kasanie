import { useEffect, useEffectEvent, useState, type CSSProperties, type ReactNode } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { put } from '../api'
import { EmptyState, ErrorState, PageHeader, PageLoader } from '../components'
import { CoachScopeBar, useCoachScope, useOptionalCoachScope } from '../CoachScope'
import { useApiData } from '../hooks'
import {
  addDays, coachTasks, dateFromKey, dateKey, dayIntensity, defaultPlayerSettings, defaultTrainingFormation, individualFocus,
  intensities, monthWeeks, normalizeTrainingState, playerAge, positionGroups, slotNames,
  trainingCategories, trainingTabs, weekDates,
  type CalendarEntry, type CalendarLock, type PlayerSettings, type TrainingPlayer, type TrainingResponse, type TrainingState, type TrainingTeamData
} from './trainingModel'
import './trainingWorkspace.css'

type TrainingView = typeof trainingTabs[number][0]
type EditorProps = { data: TrainingTeamData; response: TrainingResponse; state: TrainingState; update: (patch: Partial<TrainingState>) => void }
const shortDate = (value: Date) => new Intl.DateTimeFormat('ru-RU', { day: 'numeric', month: 'short' }).format(value)
const fullName = (player: TrainingPlayer) => `${player.firstName} ${player.lastName}`
const format = (value?: string) => value === '5v5' ? 'Футзал · 5 × 5' : (value ?? '11v11').replace('v', ' × ')

export function TrainingWorkspacePage() {
  const scope = useCoachScope()
  const teamId = !scope.loading && !scope.error ? scope.teamId : 0
  const center = useApiData<TrainingTeamData>(teamId ? `/api/coach/teams/${teamId}/command-center` : null)
  if (scope.loading) return <PageLoader />
  if (scope.error) return <ErrorState message={scope.error} retry={scope.reload} />
  return <><PageHeader title="Тренировки" actions={<Link className="button ghost" to={scope.link('/coach/trainings/journal')}>Журнал занятий →</Link>} />
    <CoachScopeBar>{center.data?.team.id === teamId && <div className="team-context-meta"><span><small>Формат</small><strong>{format(center.data.team.matchFormat)}</strong></span><span><small>Игроков</small><strong>{center.data.players.length}</strong></span><span><small>Текущий цикл</small><strong>{center.data.team.trainingCycleStage}</strong></span><span><small>Сезон</small><strong>{center.data.team.season || '—'}</strong></span></div>}</CoachScopeBar>
    {!teamId ? <section className="card"><EmptyState title="В этой школе состав не назначен">Для доступа к тренировкам нужно назначение в команду этой школы.</EmptyState></section> : center.loading || center.data?.team.id !== teamId ? center.error ? <ErrorState message={center.error} retry={center.reload} /> : <PageLoader /> : <TrainingWorkspace key={teamId} data={center.data} />}
  </>
}

export function TrainingWorkspace({ data }: { data: TrainingTeamData }) {
  const [calendarDate, setCalendarDate] = useState(dateKey(new Date()))
  const year = dateFromKey(calendarDate).getFullYear()
  const response = useApiData<TrainingResponse>(`/api/coach/teams/${data.team.id}/training-workspace?year=${year}&utcOffsetMinutes=${-new Date().getTimezoneOffset()}`)
  if (response.loading) return <PageLoader />
  if (response.error || !response.data) return <ErrorState message={response.error} retry={response.reload} />
  return <TrainingBoard key={`${data.team.id}-${year}`} data={data} response={response.data} calendarDate={calendarDate} onCalendarDate={setCalendarDate} />
}

export function TrainingBoard({ data, response, calendarDate, onCalendarDate }: { data: TrainingTeamData; response: TrainingResponse; calendarDate: string; onCalendarDate: (value: string) => void }) {
  const [params, setParams] = useSearchParams()
  const view = (trainingTabs.some(([id]) => id === params.get('trainingView')) ? params.get('trainingView') : 'overview') as TrainingView
  const [state, setState] = useState(() => normalizeTrainingState(response, data))
  const [saved, setSaved] = useState(() => JSON.stringify(normalizeTrainingState(response, data)))
  const [version, setVersion] = useState(response.version)
  const [pending, setPending] = useState(false)
  const [notice, setNotice] = useState<{ ok: boolean; text: string } | null>(null)
  const [editingSlot, setEditingSlot] = useState<CalendarEntry | null>(null)
  const [tasksOpen, setTasksOpen] = useState(false)
  const dirty = JSON.stringify(state) !== saved
  const scope = useOptionalCoachScope()
  const registerGuard = scope?.registerGuard
  useEffect(() => registerGuard?.(() => !pending && (!dirty || confirm('Есть несохранённые изменения плана. Сменить школу или состав без сохранения?'))), [registerGuard, dirty, pending])
  const update = (patch: Partial<TrainingState>) => { setState(previous => ({ ...previous, ...patch })); setNotice(null) }
  const setView = (nextView: TrainingView) => { const next = new URLSearchParams(params); next.set('trainingView', nextView); setParams(next, { replace: true }) }
  useEffect(() => {
    const beforeUnload = (event: BeforeUnloadEvent) => { if (dirty) { event.preventDefault(); event.returnValue = '' } }
    window.addEventListener('beforeunload', beforeUnload)
    return () => window.removeEventListener('beforeunload', beforeUnload)
  }, [dirty])
  const save = async () => {
    setPending(true); setNotice(null)
    try {
      const result = await put<{ version: number }>(`/api/coach/teams/${data.team.id}/training-workspace`, { version, utcOffsetMinutes: -new Date().getTimezoneOffset(), state })
      setVersion(result.version); setSaved(JSON.stringify(state)); setNotice({ ok: true, text: 'План тренировок сохранён.' })
    } catch (error) { setNotice({ ok: false, text: error instanceof Error ? error.message : 'Не удалось сохранить план.' }) }
    finally { setPending(false) }
  }
  const changeDate = (value: string) => {
    if (!/^\d{4}-\d{2}-\d{2}$/.test(value) || Number(value.slice(0, 4)) < 2000 || Number(value.slice(0, 4)) > 2100) return
    if (value.slice(0, 4) !== calendarDate.slice(0, 4) && dirty && !confirm('В этом плане есть несохранённые изменения. Перейти в другой год без сохранения?')) return
    onCalendarDate(value)
  }
  const editor = { data, response, state, update }
  const editSlot = (date: string, slot: number) => setEditingSlot(state.calendar.find(entry => entry.date === date && entry.slot === slot) ?? { date, slot, session: 'Отдых', category: 'Rest', intensity: 0, minutes: 0, group: 'Team', notes: null })
  return <section className="training-workspace">
    <div className="training-workspace-toolbar"><nav className="training-subtabs" aria-label="Разделы тренировок">{trainingTabs.map(([id, label]) => <button type="button" key={id} className={view === id ? 'active' : ''} aria-current={view === id ? 'page' : undefined} onClick={() => setView(id)}>{label}</button>)}</nav><div className="training-save"><span role="status">{dirty ? 'Есть несохранённые изменения' : 'План сохранён'}</span><button type="button" className="button" disabled={pending || !dirty} onClick={() => void save()}>{pending ? 'Сохраняем…' : 'Сохранить план'}</button></div></div>
    {notice && <p role={notice.ok ? 'status' : 'alert'} className={notice.ok ? 'success-message' : 'error-message'}>{notice.text}</p>}
    <fieldset disabled={pending} className="training-workspace-body">
      {view === 'overview' && <TrainingOverview {...editor} openCalendar={() => { changeDate(dateKey(new Date())); setView('calendar') }} editSlot={editSlot} />}
      {view === 'calendar' && <TrainingCalendar {...editor} selectedDate={calendarDate} onDate={changeDate} editSlot={editSlot} />}
      {view === 'groups' && <TrainingGroups {...editor} />}
      {view === 'mentoring' && <TrainingMentoring {...editor} />}
      {view === 'individual' && <IndividualTraining {...editor} />}
      {view === 'rest' && <TrainingRest {...editor} />}
      {view === 'coaches' && <TrainingCoaches {...editor} openTasks={() => setTasksOpen(true)} />}
    </fieldset>
    {editingSlot && <SessionDialog key={`${editingSlot.date}-${editingSlot.slot}`} entry={editingSlot} teamId={data.team.id} close={() => setEditingSlot(null)} apply={entry => { update({ calendar: [...state.calendar.filter(c => c.date !== entry.date || c.slot !== entry.slot), entry] }); setEditingSlot(null) }} remove={() => { update({ calendar: state.calendar.filter(c => c.date !== editingSlot.date || c.slot !== editingSlot.slot) }); setEditingSlot(null) }} />}
    {tasksOpen && <TaskDialog {...editor} close={() => setTasksOpen(false)} />}
  </section>
}

function TrainingOverview(props: EditorProps & { openCalendar: () => void; editSlot: (date: string, slot: number) => void }) {
  const { data, state, response, update } = props
  const [settingsOpen, setSettingsOpen] = useState(false)
  const today = dateKey(new Date()), week = weekDates(new Date())
  const nextMatches = data.matches.filter(match => match.scheduledAt.slice(0, 10) >= today && !/отмен/i.test(match.status)).slice(0, 4)
  const lastMatch = [...data.matches].filter(match => match.scheduledAt.slice(0, 10) < today && !/отмен/i.test(match.status)).sort((a, b) => b.scheduledAt.localeCompare(a.scheduledAt))[0]
  return <>
    <section className="training-control-strip card"><label>Ответственный за тренировки<select aria-label="Ответственный за тренировки" value={state.responsibleCoachId ?? ''} onChange={e => update({ responsibleCoachId: Number(e.target.value) || null })}><option value="">{data.team.coachName ? `${data.team.coachName} · по умолчанию` : 'Главный тренер по умолчанию'}</option>{response.coaches.map(coach => <option key={coach.id} value={coach.id}>{coach.name}{coach.isHeadCoach ? ' · главный тренер' : ''}</option>)}</select><small>Назначение не меняет права доступа к команде.</small></label><div><span className="eyebrow">Основная тренируемая тактика</span><strong>{state.tactics[0]?.name ?? 'Тактика команды'} · {state.tactics[0]?.formation ?? data.team.tacticFormation ?? 'схема не задана'}</strong><Link to={`/coach/teams?team=${data.team.id}&tab=tactics`}>Открыть тактическую доску →</Link></div><div><span className="eyebrow">Другие тактики</span><strong>{state.tactics.slice(1).map(tactic => `${tactic.name} · ${tactic.formation}`).join(', ') || 'Пока не назначены'}</strong><small>Можно одновременно готовить до трёх вариантов.</small><a href="#training-tactics-settings" onClick={() => setSettingsOpen(true)}>Настроить тренируемые тактики →</a></div></section>
    <section className="card training-week-card"><div className="card-heading"><div><span className="eyebrow">Эта неделя · понедельник — воскресенье</span><h2>{shortDate(week[0])} — {shortDate(week[6])}</h2></div><button className="button ghost" onClick={props.openCalendar}>Тренировочный календарь →</button></div><WeekGrid dates={week} state={state} locks={response.lockedSlots} editSlot={props.editSlot} /></section>
    <div className="training-dashboard-grid"><article className="card"><span className="eyebrow">Наблюдение тренера</span><h2>Удовлетворённость тренировками</h2><label>Комментарий о процессе<textarea maxLength={2000} placeholder="Например, обратная связь игроков после занятий…" value={state.satisfaction ?? ''} onChange={e => update({ satisfaction: e.target.value || null })} /></label><small>Заполняется тренером. Результаты и рейтинги здесь не отображаются.</small></article><TrainingDistribution state={state} locks={response.lockedSlots} dates={week} /><MedicalWidget data={data} /></div>
    <div className="training-dashboard-bottom"><article className="card"><span className="eyebrow">Подготовка к матчу</span><h2>{nextMatches[0]?.opponent ?? 'Матч пока не запланирован'}</h2><UpcomingMatches data={data} /><label>Акценты подготовки<textarea maxLength={4000} value={state.matchPreparation ?? ''} placeholder="Что отрабатываем перед ближайшей встречей…" onChange={e => update({ matchPreparation: e.target.value || null })} /></label></article><article className="card"><span className="eyebrow">Комментарии по последнему матчу</span><h2>{lastMatch ? `${lastMatch.opponent} · ${shortDate(dateFromKey(lastMatch.scheduledAt.slice(0, 10)))}` : 'Матчей пока нет'}</h2><label>Что учесть в тренировках<textarea maxLength={4000} value={state.lastMatchComment ?? ''} placeholder="Игровые эпизоды, сильные стороны и задачи на неделю…" onChange={e => update({ lastMatchComment: e.target.value || null })} /></label></article></div>
    <details id="training-tactics-settings" className="card training-settings" open={settingsOpen} onToggle={e => setSettingsOpen(e.currentTarget.open)}><summary>Тренируемые тактики и тренировочный цикл</summary><TacticSettings {...props} /><CycleSettings data={data} /></details>
  </>
}

function WeekGrid({ dates, state, locks, editSlot }: { dates: Date[]; state: TrainingState; locks: CalendarLock[]; editSlot: (date: string, slot: number) => void }) {
  const today = dateKey(new Date())
  return <div className="training-week-scroll"><table className="training-week-table"><thead><tr><th scope="col">Сессия</th>{dates.map(date => <th className={dateKey(date) === today ? 'today' : ''} scope="col" key={dateKey(date)}><b>{new Intl.DateTimeFormat('ru-RU', { weekday: 'short' }).format(date)}</b><span>{shortDate(date)}</span></th>)}</tr></thead><tbody>{slotNames.map((label, slot) => <tr key={slot}><th scope="row">{label}</th>{dates.map(date => {
    const key = dateKey(date), lock = locks.find(item => item.date === key && item.slot === slot), entry = state.calendar.find(item => item.date === key && item.slot === slot)
    const category = lock ? lock.kind === 'Match' ? 'Match' : 'General' : entry?.category ?? 'Rest'
    const content = <><strong>{lock ? lock.kind === 'Match' ? `Матч · ${lock.title}` : lock.title : entry?.session ?? 'Отдых'}</strong><small>{lock ? lock.kind === 'Match' ? 'Слот занят матчем · 🔒' : `${lock.status === 'Completed' ? 'Завершена' : 'Из журнала'} · 🔒` : entry?.category === 'Rest' || !entry ? 'Без нагрузки' : `${entry.minutes} мин · ${entry.group === 'Team' ? 'Команда' : positionGroups.find(group => group.id === entry.group)?.label}`}</small></>
    return <td key={key} className={`training-cell category-${category.toLowerCase()}`}>{lock?.kind === 'Training' ? <Link to={`/coach/trainings/${lock.id}`} aria-label={`${label}, ${shortDate(date)}: ${lock.title}, открыть журнал`}>{content}</Link> : lock ? <button disabled aria-label={`${label}, ${shortDate(date)}: матч ${lock.title}, слот заблокирован`}>{content}</button> : <button aria-label={`${label}, ${shortDate(date)}: ${entry?.session ?? 'Отдых'}`} onClick={() => editSlot(key, slot)}>{content}</button>}</td>
  })}</tr>)}<tr className="training-intensity-row"><th scope="row">Интенсивность<small>плановая оценка</small></th>{dates.map(date => { const value = dayIntensity(dateKey(date), state, locks); return <td key={dateKey(date)}><div className={`training-load-bar ${value > 75 ? 'high' : ''}`} role="meter" aria-label={`Интенсивность ${shortDate(date)}`} aria-valuenow={value} aria-valuemin={0} aria-valuemax={100}><span style={{ width: `${value}%` }} /></div><small>{value}%</small></td> })}</tr></tbody></table>{locks.filter(lock => lock.slot > 2 && dates.some(date => dateKey(date) === lock.date)).map(lock => <p className="readonly-note" key={`${lock.kind}-${lock.id}`}>{shortDate(dateFromKey(lock.date))} · дополнительное занятие из журнала: <Link to={`/coach/trainings/${lock.id}`}>{lock.title}</Link></p>)}</div>
}

function TrainingCalendar({ selectedDate, onDate, editSlot, ...props }: EditorProps & { selectedDate: string; onDate: (date: string) => void; editSlot: (date: string, slot: number) => void }) {
  const [mode, setMode] = useState<'month' | 'week'>('month')
  const date = dateFromKey(selectedDate), weeks = mode === 'week' ? [weekDates(date)] : monthWeeks(date)
  const navigate = (delta: number) => { const next = mode === 'week' ? addDays(date, delta * 7) : new Date(date.getFullYear(), date.getMonth() + delta, 1, 12); onDate(dateKey(next)) }
  return <section className="card training-calendar-card"><div className="training-calendar-toolbar"><div><span className="eyebrow">Планирование с января по декабрь</span><h2>Календарь тренировок</h2></div><div className="training-calendar-controls"><button className="button ghost" aria-label="Предыдущий период" onClick={() => navigate(-1)}>←</button><label>Месяц<input type="month" min="2000-01" max="2100-12" value={selectedDate.slice(0, 7)} onChange={e => e.target.value && onDate(`${e.target.value}-01`)} /></label><button className="button ghost" aria-label="Следующий период" onClick={() => navigate(1)}>→</button><button className="button ghost" onClick={() => onDate(dateKey(new Date()))}>Сегодня</button><label>Вид<select value={mode} onChange={e => setMode(e.target.value as typeof mode)}><option value="month">Месяц</option><option value="week">Неделя</option></select></label>{mode === 'week' && <label>Дата недели<input type="date" min="2000-01-01" max="2100-12-31" value={selectedDate} onChange={e => e.target.value && onDate(e.target.value)} /></label>}</div></div><p className="readonly-note">Три слота на каждый день. Матч занимает второй слот и не заменяется тренировкой; занятия из журнала открываются отдельно.</p>{weeks.map(dates => <section className="training-calendar-week" key={dateKey(dates[0])}><h3>{shortDate(dates[0])} — {shortDate(dates[6])}</h3><WeekGrid dates={dates} state={props.state} locks={props.response.lockedSlots} editSlot={editSlot} /></section>)}<small>Интенсивность — ориентир плана относительно 90 минут матча, не медицинская оценка фактической нагрузки.</small></section>
}

function TrainingDistribution({ state, locks, dates }: { state: TrainingState; locks: CalendarLock[]; dates: Date[] }) {
  const keys = new Set(dates.map(dateKey)), minutes: Record<string, number> = {}
  state.calendar.filter(entry => keys.has(entry.date) && !locks.some(lock => lock.date === entry.date && lock.slot === entry.slot) && entry.category !== 'Rest').forEach(entry => { minutes[entry.category] = (minutes[entry.category] ?? 0) + entry.minutes })
  locks.filter(lock => keys.has(lock.date) && lock.kind === 'Training').forEach(lock => { minutes.General = (minutes.General ?? 0) + lock.minutes })
  const total = Object.values(minutes).reduce((sum, value) => sum + value, 0)
  const categories = trainingCategories.filter(category => minutes[category.id] > 0)
  const portions = categories.map((category, index) => { const start = categories.slice(0, index).reduce((sum, c) => sum + minutes[c.id], 0) / total * 360; return { ...category, start, end: start + minutes[category.id] / total * 360, minutes: minutes[category.id] } })
  return <article className="card"><span className="eyebrow">По минутам · текущая неделя</span><h2>Разбивка тренировочных сессий</h2>{total ? <div className="training-distribution"><div className="training-donut" role="img" aria-label={portions.map(p => `${p.label}: ${p.minutes} минут`).join(', ')} style={{ background: `conic-gradient(${portions.map(p => `${p.color} ${p.start}deg ${p.end}deg`).join(', ')})` }}><span><strong>{total}</strong><small>минут</small></span></div><ul>{portions.map(p => <li key={p.id}><i style={{ background: p.color }} /><span>{p.label}</span><b>{Math.round(p.minutes / total * 100)}%</b></li>)}</ul></div> : <EmptyState title="Неделя ещё не заполнена">Добавьте занятия в календаре. Матчи и отдых в разбивку не входят.</EmptyState>}</article>
}

function MedicalWidget({ data }: { data: TrainingTeamData }) {
  const injuries = data.injuries.filter(injury => injury.status !== 'Закрыта')
  const unique = data.players.map(player => ({ player, injury: injuries.filter(i => i.playerId === player.playerId).sort((a, b) => b.riskLevel - a.riskLevel)[0] })).filter(row => row.injury)
  return <article className="card"><span className="eyebrow">Данные медцентра</span><h2>Нагрузка и риск травмы</h2><div className="training-medical-stats"><span><b>{unique.length}</b> с активными записями</span><span><b>{unique.filter(row => row.injury.riskLevel >= 70).length}</b> высокий риск</span></div>{unique.length ? <div className="training-medical-list">{unique.sort((a, b) => b.injury.riskLevel - a.injury.riskLevel).slice(0, 8).map(({ player, injury }) => <div key={player.playerId}><span><strong>{fullName(player)}</strong><small>{injury.type} · {injury.status}</small></span><b className={injury.riskLevel >= 70 ? 'training-risk high' : 'training-risk'}>{injury.riskLevel}%</b></div>)}</div> : <p className="readonly-note">Активных записей нет. Это не подтверждение готовности игроков к нагрузке.</p>}<Link to={`/coach/teams?team=${data.team.id}&tab=injuries`}>Открыть медцентр →</Link></article>
}

function TacticSettings({ data, state, update }: EditorProps) {
  const defaultFormation = data.team.tacticFormation ?? defaultTrainingFormation(data.team.matchFormat)
  const tactics = state.tactics.length ? state.tactics : [{ name: 'Основная тактика', formation: defaultFormation, notes: null }]
  const change = (index: number, patch: Partial<typeof tactics[number]>) => update({ tactics: tactics.map((tactic, i) => i === index ? { ...tactic, ...patch } : tactic) })
  return <div className="training-tactic-editor"><h3>Тренируемые тактики</h3>{tactics.map((tactic, index) => <div key={index}><label>{index === 0 ? 'Основная тактика' : `Другой вариант ${index}`}<input maxLength={100} value={tactic.name} onChange={e => change(index, { name: e.target.value })} /></label><label>Схема<input maxLength={80} value={tactic.formation} onChange={e => change(index, { formation: e.target.value })} /></label><label>Акценты<input maxLength={2000} value={tactic.notes ?? ''} onChange={e => change(index, { notes: e.target.value || null })} /></label>{index > 0 && <button className="button ghost" aria-label={`Убрать тактику ${tactic.name}`} onClick={() => update({ tactics: tactics.filter((_, i) => i !== index) })}>×</button>}</div>)}{tactics.length < 3 && <button className="button ghost" onClick={() => update({ tactics: [...tactics, { name: `Вариант ${tactics.length + 1}`, formation: defaultFormation, notes: null }] })}>+ Добавить тактику</button>}</div>
}

function CycleSettings({ data }: { data: TrainingTeamData }) {
  const [notice, setNotice] = useState('')
  const [pending, setPending] = useState(false)
  return <form className="profile-form training-cycle-form" onSubmit={async e => { e.preventDefault(); const form = new FormData(e.currentTarget); setPending(true); try { await put(`/api/coach/teams/${data.team.id}/cycle`, { stage: form.get('stage'), startsOn: form.get('startsOn') || null, endsOn: form.get('endsOn') || null }); setNotice('Цикл сохранён.') } catch (error) { setNotice(error instanceof Error ? error.message : 'Не удалось сохранить цикл.') } finally { setPending(false) } }}><label>Тренировочный цикл<select name="stage" defaultValue={data.team.trainingCycleStage}>{['Подготовительный этап', 'Базовый этап', 'Соревновательный этап', 'Восстановительный этап', 'Переходный этап'].map(stage => <option key={stage}>{stage}</option>)}</select></label><label>Начало<input name="startsOn" type="date" defaultValue={data.team.cycleStart} /></label><label>Окончание<input name="endsOn" type="date" defaultValue={data.team.cycleEnd} /></label><button disabled={pending} className="button ghost">Сохранить цикл</button>{notice && <p role="status">{notice}</p>}</form>
}

function settingsFor(player: TrainingPlayer, state: TrainingState) { return state.players.find(item => item.playerId === player.playerId) ?? defaultPlayerSettings(player) }
function updatePlayer(player: TrainingPlayer, patch: Partial<PlayerSettings>, state: TrainingState, update: EditorProps['update']) { update({ players: [...state.players.filter(item => item.playerId !== player.playerId), { ...settingsFor(player, state), ...patch }] }) }

function TrainingGroups({ data, state, update }: EditorProps) {
  return <><div className="training-section-heading"><div><span className="eyebrow">Позиционные группы</span><h2>Распределение игроков</h2><p>Полузащитники по умолчанию в нападении. Группу можно изменить для любого игрока.</p></div></div><div className="training-groups-grid">{positionGroups.map(group => <article className={`card training-position-group group-${group.id.toLowerCase()}`} key={group.id}><div className="card-heading"><h2>{group.label}</h2><span className="training-count">{data.players.filter(player => settingsFor(player, state).group === group.id).length}</span></div>{data.players.filter(player => settingsFor(player, state).group === group.id).map(player => <div className="training-group-player" key={player.playerId}><span><strong>{fullName(player)}</strong><small>{playerAge(player.dateOfBirth)} лет · {player.preferredPosition}</small></span><label>Группа<select aria-label={`Группа ${fullName(player)}`} value={group.id} onChange={e => updatePlayer(player, { group: e.target.value }, state, update)}>{positionGroups.map(item => <option value={item.id} key={item.id}>{item.label}</option>)}</select></label></div>)}{!data.players.some(player => settingsFor(player, state).group === group.id) && <EmptyState title="Группа пока пуста" />}</article>)}</div></>
}

function TrainingMentoring({ data, state, update }: EditorProps) {
  const changeGroup = (id: string, patch: Partial<TrainingState['mentoring'][number]>) => update({ mentoring: state.mentoring.map(group => group.id === id ? { ...group, ...patch } : group) })
  return <><div className="training-section-heading"><div><span className="eyebrow">Совместная работа</span><h2>Группы наставничества</h2><p>Выберите наставника из состава группы. Каждый игрок участвует только в одной группе.</p></div><button className="button ghost" disabled={state.mentoring.length >= 20} onClick={() => update({ mentoring: [...state.mentoring, { id: crypto.randomUUID(), name: `Группа ${state.mentoring.length + 1}`, mentorId: null, playerIds: [] }] })}>+ Добавить группу</button></div><div className="training-mentoring-list">{state.mentoring.map(group => <article className="card" key={group.id}><div className="training-mentoring-heading"><label>Название<input aria-label={`Название ${group.name}`} maxLength={80} value={group.name} onChange={e => changeGroup(group.id, { name: e.target.value })} /></label><label>Наставник<select value={group.mentorId ?? ''} onChange={e => changeGroup(group.id, { mentorId: Number(e.target.value) || null })}><option value="">Не назначен</option>{data.players.filter(player => group.playerIds.includes(player.playerId)).map(player => <option key={player.playerId} value={player.playerId}>{fullName(player)}</option>)}</select></label><button className="button ghost" aria-label={`Удалить ${group.name}`} onClick={() => { if (confirm(`Убрать ${group.name} из плана наставничества?`)) update({ mentoring: state.mentoring.filter(item => item.id !== group.id) }) }}>Убрать группу</button></div><div className="training-mentoring-members">{group.playerIds.map(id => { const player = data.players.find(item => item.playerId === id); return player && <div key={id}><strong>{fullName(player)}{group.mentorId === id ? ' · наставник' : ''}</strong><small>{playerAge(player.dateOfBirth)} лет · {player.preferredPosition} · {player.experienceLevel || 'опыт не указан'}</small><button aria-label={`Убрать ${fullName(player)} из ${group.name}`} onClick={() => changeGroup(group.id, { playerIds: group.playerIds.filter(item => item !== id), mentorId: group.mentorId === id ? null : group.mentorId })}>×</button></div> })}</div><label>Добавить игрока<select value="" onChange={e => e.target.value && changeGroup(group.id, { playerIds: [...group.playerIds, Number(e.target.value)] })}><option value="">Выберите игрока</option>{data.players.filter(player => !state.mentoring.some(item => item.playerIds.includes(player.playerId))).map(player => <option value={player.playerId} key={player.playerId}>{fullName(player)} · {playerAge(player.dateOfBirth)} лет · {player.preferredPosition}</option>)}</select></label></article>)}</div></>
}

function IndividualTraining({ data, state, update }: EditorProps) {
  const [params] = useSearchParams()
  const [selected, setSelected] = useState(data.players.find(player => player.playerId === Number(params.get('player')))?.playerId ?? data.players[0]?.playerId ?? 0), [search, setSearch] = useState('')
  const player = data.players.find(item => item.playerId === selected)
  const settings = player ? settingsFor(player, state) : null
  const injury = player && data.injuries.filter(item => item.playerId === player.playerId && item.status !== 'Закрыта').sort((a, b) => b.riskLevel - a.riskLevel)[0]
  const patch = (value: Partial<PlayerSettings>) => player && updatePlayer(player, value, state, update)
  if (!data.players.length) return <section className="card"><EmptyState title="В составе пока нет игроков" /></section>
  return <><div className="training-section-heading"><div><span className="eyebrow">Персональные задачи</span><h2>Индивидуальные тренировки</h2></div></div><div className="training-individual-layout"><aside className="card training-player-browser"><label>Поиск игрока<input type="search" value={search} onChange={e => setSearch(e.target.value)} /></label>{data.players.filter(item => fullName(item).toLowerCase().includes(search.toLowerCase())).map(item => <button className={selected === item.playerId ? 'selected' : ''} onClick={() => setSelected(item.playerId)} key={item.playerId}><b>{item.shirtNumber ?? '—'}</b><span><strong>{fullName(item)}</strong><small>{playerAge(item.dateOfBirth)} лет · {item.preferredPosition}</small></span><small>{settingsFor(item, state).focus}</small></button>)}</aside>{player && settings && <article className="card training-individual-detail"><div className="card-heading"><div><span className="eyebrow">{playerAge(player.dateOfBirth)} лет · {player.experienceLevel || 'Опыт не указан'}</span><h2>{fullName(player)}</h2><p>{player.preferredPosition} · ведущая нога: {player.dominantFoot || 'не указана'}</p></div></div><div className="training-individual-fields"><label>Тренируемая позиция<input value={settings.position} maxLength={80} onChange={e => patch({ position: e.target.value })} required /></label><label>Роль / обязанность<input value={settings.role} maxLength={100} onChange={e => patch({ role: e.target.value })} /></label><label>Тренировочная группа<select value={settings.group} onChange={e => patch({ group: e.target.value })}>{positionGroups.map(group => <option key={group.id} value={group.id}>{group.label}</option>)}</select></label><label>Дополнительное направление<select value={settings.focus} onChange={e => patch({ focus: e.target.value })}>{individualFocus.map(focus => <option key={focus}>{focus}</option>)}</select></label><label>Интенсивность<select value={settings.intensity} onChange={e => patch({ intensity: e.target.value })}>{intensities.map(item => <option key={item.id} value={item.id}>{item.label}</option>)}</select></label><label className="training-check"><input type="checkbox" checked={settings.weakFoot} onChange={e => patch({ weakFoot: e.target.checked })} />Работа над слабой ногой</label></div><label>Задача и комментарий тренера<textarea maxLength={2000} value={settings.notes ?? ''} onChange={e => patch({ notes: e.target.value || null })} /></label><div className="training-individual-notes"><section><h3>Тренерская заметка из профиля</h3><p>{player.coachNote || 'Пока не добавлена.'}</p><Link to={`/coach/players/${player.playerId}`}>Профиль и план развития →</Link></section><section><h3>Медицинская запись</h3><p>{injury ? `${injury.type} · ${injury.status} · риск ${injury.riskLevel}%` : 'Активных записей нет. Готовность к нагрузке не подтверждена.'}</p>{injury?.notes && <p>{injury.notes}</p>}<small>Изменение интенсивности — решение тренера с учётом рекомендаций специалиста.</small></section></div></article>}</div></>
}

function UpcomingMatches({ data }: { data: TrainingTeamData }) {
  const today = dateFromKey(dateKey(new Date()))
  const matches = data.matches.filter(match => dateFromKey(match.scheduledAt.slice(0, 10)) >= today && !/отмен/i.test(match.status)).sort((a, b) => a.scheduledAt.localeCompare(b.scheduledAt)).slice(0, 4)
  return <div className="training-upcoming">{matches.length ? matches.map(match => <div key={match.id}><strong>{match.opponent}</strong><small>{shortDate(dateFromKey(match.scheduledAt.slice(0, 10)))} · через {Math.round((dateFromKey(match.scheduledAt.slice(0, 10)).getTime() - today.getTime()) / 86400000)} дн.</small></div>) : <p className="readonly-note">Предстоящие матчи не добавлены в расписание команды.</p>}</div>
}

function TrainingRest({ data, state, update }: EditorProps) {
  const today = dateKey(new Date())
  const resting = data.players.filter(player => { const value = settingsFor(player, state); return value.intensity === 'Rest' || value.restUntil && value.restUntil >= today })
  const medical = data.players.filter(player => data.injuries.some(item => item.playerId === player.playerId && item.status !== 'Закрыта'))
  return <><div className="training-section-heading"><div><span className="eyebrow">Управление восстановлением</span><h2>Отдых и интенсивность</h2><p>Медцентр даёт контекст, но не заменяет решение специалиста о допуске.</p></div></div><section className="card"><span className="eyebrow">Предстоящие матчи</span><UpcomingMatches data={data} /></section><div className="training-rest-summary"><article className="card"><h3>Сегодня на отдыхе · {resting.length}</h3><p>{resting.length ? resting.map(fullName).join(', ') : 'Индивидуальный отдых не назначен.'}</p><small>Учитываются постоянная интенсивность «Отдых» и дата отдыха включительно.</small></article><article className="card"><h3>Нужна проверка нагрузки · {medical.length}</h3><p>{medical.length ? medical.map(fullName).join(', ') : 'Активных записей медцентра нет.'}</p><small>Игроки с травмами и восстановлением: согласуйте режим со специалистом.</small></article></div><section className="card training-rest-table"><div className="training-table-scroll"><table><thead><tr><th>Игрок</th><th>Позиция</th><th>Рекомендация / медцентр</th><th>Интенсивность</th><th>Отдых по дату</th></tr></thead><tbody>{resting.map(player => { const settings = settingsFor(player, state), injury = data.injuries.filter(item => item.playerId === player.playerId && item.status !== 'Закрыта').sort((a, b) => b.riskLevel - a.riskLevel)[0]; return <tr key={player.playerId}><td><strong>{fullName(player)}</strong><small>{playerAge(player.dateOfBirth)} лет</small></td><td>{player.preferredPosition}</td><td>{injury ? <><b className={injury.riskLevel >= 70 ? 'training-risk high' : 'training-risk'}>{injury.riskLevel}% · {injury.status}</b><p>{injury.notes || 'Обсудите отдых и допуск с медштабом.'}</p></> : <small>Рекомендация не внесена</small>}</td><td><select aria-label={`Интенсивность ${fullName(player)}`} value={settings.intensity} onChange={e => updatePlayer(player, { intensity: e.target.value }, state, update)}>{intensities.map(item => <option value={item.id} key={item.id}>{item.label}</option>)}</select></td><td><input aria-label={`Отдых по дату ${fullName(player)}`} type="date" min="2000-01-01" max="2100-12-31" value={settings.restUntil ?? ''} onChange={e => updatePlayer(player, { restUntil: e.target.value || null }, state, update)} /></td></tr> })}</tbody></table></div>{!resting.length && <EmptyState title="Сегодня никто не на отдыхе">Назначьте отдых из меню игрока в составе или в индивидуальных тренировках.</EmptyState>}</section></>
}

function TrainingCoaches({ data, response, state, openTasks }: EditorProps & { openTasks: () => void }) {
  return <><div className="training-section-heading"><div><span className="eyebrow">Штаб · {response.coaches.length} тренеров</span><h2>Тренеры и направления</h2><p>Распределите ответственность и проверьте, какие направления ещё не покрыты.</p></div><button className="button" onClick={openTasks}>Изменить тренерские задачи</button></div><p className="readonly-note">Нагрузка здесь организационная: число направлений на одного тренера и размер состава ({data.players.length} игроков). Это не оценка качества или медицинской нагрузки.</p><div className="training-coach-cards">{coachTasks.map(task => { const assigned = state.coaches.filter(coach => coach.tasks.includes(task.id)); return <article className="card" key={task.id}><span className="eyebrow">{task.label.split(' · ')[0]}</span><h3>{task.label.split(' · ')[1]}</h3><div className="training-assigned-coaches">{assigned.length ? assigned.map(item => { const coach = response.coaches.find(c => c.id === item.coachId); return <div key={item.coachId}><strong>{coach?.name}</strong><small>{coach?.isHeadCoach ? 'Главный тренер' : 'Тренер школы'} · {item.tasks.length} направлений</small><span className={`training-coach-load ${item.tasks.length >= 5 ? 'heavy' : ''}`}>{item.tasks.length >= 5 ? 'Высокая' : item.tasks.length >= 3 ? 'Средняя' : 'Лёгкая'} нагрузка</span></div> }) : <p className="training-unassigned">Ответственный не назначен</p>}</div></article> })}</div></>
}

function TaskDialog({ response, state, update, close }: EditorProps & { close: () => void }) {
  return <TrainingDialog title="Ответственность тренеров" close={close} wide><p>Отметьте направления для каждого тренера. Изменения попадут в общий план; затем нажмите «Сохранить план».</p><div className="training-table-scroll"><table className="training-coach-task-table"><thead><tr><th>Тренер</th>{coachTasks.map(task => <th key={task.id}>{task.label}</th>)}<th>Направлений</th></tr></thead><tbody>{response.coaches.map(coach => { const assigned = state.coaches.find(item => item.coachId === coach.id)?.tasks ?? []; return <tr key={coach.id}><th scope="row">{coach.name}<small>{coach.isHeadCoach ? 'Главный тренер' : 'Тренер школы'}</small></th>{coachTasks.map(task => <td key={task.id}><input type="checkbox" aria-label={`${coach.name}: ${task.label}`} checked={assigned.includes(task.id)} onChange={e => update({ coaches: [...state.coaches.filter(item => item.coachId !== coach.id), { coachId: coach.id, tasks: e.target.checked ? [...assigned, task.id] : assigned.filter(id => id !== task.id) }] })} /></td>)}<td><b>{assigned.length}</b></td></tr> })}</tbody><tfoot><tr><th>Тренеров на направлении</th>{coachTasks.map(task => <td key={task.id}>{state.coaches.filter(coach => coach.tasks.includes(task.id)).length}</td>)}<td>—</td></tr></tfoot></table></div><div className="training-dialog-actions"><button className="button ghost" onClick={() => { if (confirm('Убрать все назначения тренерских задач из текущего плана?')) update({ coaches: [] }) }}>Очистить назначения</button><button className="button" onClick={close}>Готово</button></div></TrainingDialog>
}

function SessionDialog({ entry, teamId, close, apply, remove }: { entry: CalendarEntry; teamId: number; close: () => void; apply: (entry: CalendarEntry) => void; remove: () => void }) {
  const [draft, setDraft] = useState(entry)
  const category = trainingCategories.find(item => item.id === draft.category)!
  return <TrainingDialog title={`${slotNames[entry.slot]} · ${shortDate(dateFromKey(entry.date))}`} close={close}><form onSubmit={e => { e.preventDefault(); apply(draft) }}><div className="training-session-fields"><label>Направление<select value={draft.category} onChange={e => { const next = trainingCategories.find(c => c.id === e.target.value)!; setDraft({ ...draft, category: next.id, session: next.sessions[0], intensity: next.id === 'Rest' ? 0 : next.id === 'Recovery' ? 20 : 50, minutes: next.id === 'Rest' ? 0 : 60 }) }}>{trainingCategories.map(item => <option key={item.id} value={item.id}>{item.label}</option>)}</select></label><label>Сессия<select value={draft.session} onChange={e => setDraft({ ...draft, session: e.target.value })}>{[...new Set([...category.sessions, draft.session])].map(session => <option key={session}>{session}</option>)}</select></label><label>Участники<select value={draft.group} onChange={e => setDraft({ ...draft, group: e.target.value })}><option value="Team">Вся команда</option>{positionGroups.map(group => <option key={group.id} value={group.id}>{group.label}</option>)}</select></label><label>Продолжительность, минут<input type="number" min={0} max={240} value={draft.minutes} disabled={draft.category === 'Rest'} onChange={e => setDraft({ ...draft, minutes: Number(e.target.value) })} required /></label><label className="training-session-intensity">Примерная интенсивность · {draft.intensity}%<input type="range" min={0} max={100} step={5} value={draft.intensity} disabled={draft.category === 'Rest'} onChange={e => setDraft({ ...draft, intensity: Number(e.target.value) })} /></label></div><label>Комментарий<textarea maxLength={2000} value={draft.notes ?? ''} onChange={e => setDraft({ ...draft, notes: e.target.value || null })} /></label><p className="readonly-note">Это слот плана. Для посещаемости и упражнений создайте занятие в журнале.</p><div className="training-dialog-actions"><button type="button" className="button ghost" onClick={remove}>Сбросить слот</button><button className="button">Добавить в план</button></div>{draft.category !== 'Rest' && <Link to={`/coach/trainings/journal?team=${teamId}&date=${entry.date}&title=${encodeURIComponent(draft.session)}`}>Открыть конструктор занятия →</Link>}</form></TrainingDialog>
}

export function TrainingDialog({ title, close, children, wide = false }: { title: string; close: () => void; children: ReactNode; wide?: boolean }) {
  const onClose = useEffectEvent(close)
  useEffect(() => {
    const previous = document.activeElement as HTMLElement | null
    const handleKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose()
      if (event.key === 'Tab') {
        const dialog = document.querySelector('.training-dialog'), items = dialog?.querySelectorAll<HTMLElement>('button:not(:disabled), input:not(:disabled), select:not(:disabled), textarea:not(:disabled), a[href]')
        if (!items?.length) return
        const first = items[0], last = items[items.length - 1]
        if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus() }
        else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus() }
      }
    }
    document.querySelector<HTMLElement>('.training-dialog button')?.focus()
    document.addEventListener('keydown', handleKey)
    return () => { document.removeEventListener('keydown', handleKey); previous?.focus() }
  }, [])
  return <div className="training-dialog-overlay"><section role="dialog" aria-modal="true" aria-labelledby="training-dialog-title" className={`training-dialog ${wide ? 'wide' : ''}`} style={{ '--dialog-width': wide ? '1500px' : '660px' } as CSSProperties}><div className="card-heading"><h2 id="training-dialog-title">{title}</h2><button type="button" className="button ghost" aria-label="Закрыть окно" onClick={close}>×</button></div>{children}</section></div>
}
