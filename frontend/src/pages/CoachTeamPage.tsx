import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { put } from '../api'
import { EmptyState, ErrorState, PageHeader, PageLoader, StatCard } from '../components'
import { formatDate, useApiData } from '../hooks'

type CoachTeam = { teamId: number; name: string; ageGroup?: string; season?: string; school: string; isHeadCoach: boolean; players: number }
type TeamWorkspace = { teamId: number; name: string; season?: string; trainingCycleStage: string; tacticFormation?: string; tacticNotes?: string; school: string; players: { playerId: number; firstName: string; lastName: string; preferredPosition: string; shirtNumber?: number }[]; matches: { id: number; opponent: string; competition?: string; scheduledAt: string; venue: string }[] }
type Notice = { text: string; ok: boolean } | null

export function CoachTeamsPage() {
  const teams = useApiData<CoachTeam[]>('/api/coach/teams')
  const [params, setParams] = useSearchParams()
  const [notice, setNotice] = useState<Notice>(null)
  const activeTeams = useMemo(() => teams.data ?? [], [teams.data])
  const selectedId = Number(params.get('team')) || activeTeams[0]?.teamId || 0
  const workspace = useApiData<TeamWorkspace>(selectedId ? `/api/coach/teams/${selectedId}/workspace` : null)
  useEffect(() => { if (!params.get('team') && activeTeams[0]) setParams({ team: String(activeTeams[0].teamId) }, { replace: true }) }, [activeTeams, params, setParams])
  if (teams.loading) return <PageLoader />
  if (teams.error || !teams.data) return <ErrorState message={teams.error} retry={teams.reload} />
  if (!activeTeams.length) return <><PageHeader eyebrow="Кабинет тренера" title="Мои команды" /><section className="card"><EmptyState title="Составы не назначены">Владелец школы должен назначить вас тренером состава.</EmptyState></section></>
  return <>
    <PageHeader eyebrow="Кабинет тренера" title="Мои команды" actions={<Link className="button" to="/coach/trainings">Открыть журнал →</Link>} />
    <section className="team-context-bar coach-team-context"><label><small>КОМАНДА И СОСТАВ</small><select value={selectedId} onChange={e => setParams({ team: e.target.value })}>{activeTeams.map(x => <option value={x.teamId} key={x.teamId}>{x.name}</option>)}</select></label>{workspace.data && <div className="team-context-meta"><span><small>Школа</small><strong>{workspace.data.school}</strong></span><span><small>Игроков</small><strong>{workspace.data.players.length}</strong></span><span><small>Цикл</small><strong>{workspace.data.trainingCycleStage}</strong></span><span><small>Сезон</small><strong>{workspace.data.season || '—'}</strong></span></div>}</section>
    {notice && <p className={notice.ok ? 'success-message' : 'error-message'}>{notice.text}</p>}
    {workspace.loading ? <PageLoader /> : workspace.error || !workspace.data ? <ErrorState message={workspace.error} retry={workspace.reload} /> : <CoachTeamWorkspace data={workspace.data} reload={workspace.reload} setNotice={setNotice} />}
  </>
}

function CoachTeamWorkspace({ data, reload, setNotice }: { data: TeamWorkspace; reload: () => Promise<void>; setNotice: (value: Notice) => void }) {
  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault(); const f = new FormData(event.currentTarget); setNotice(null)
    try { await put(`/api/coach/teams/${data.teamId}/tactics`, { formation: f.get('formation'), notes: f.get('notes') }); setNotice({ text: 'План состава сохранён и доступен владельцу школы.', ok: true }); await reload() }
    catch (e) { setNotice({ text: e instanceof Error ? e.message : 'Не удалось сохранить план.', ok: false }) }
  }
  const nextMatch = data.matches[0]
  return <>
    <section className="stat-grid"><StatCard label="Игроков" value={data.players.length} tone="accent" /><StatCard label="Схема" value={data.tacticFormation || 'Не выбрана'} /><StatCard label="Ближайший матч" value={nextMatch ? formatDate(nextMatch.scheduledAt) : 'Не назначен'} /></section>
    <section className="tactics-grid coach-tactics-workspace">
      <article className="card tactics-board">{['11', '7', '9', '10', '8', '6', '3', '4', '5', '2', '1'].map(x => <span key={x}>{x}</span>)}</article>
      <article className="card"><div className="card-heading"><div><span className="eyebrow">Перед игрой</span><h2>Схема и предварительный состав</h2></div></div>{nextMatch && <div className="coach-next-match"><small>{formatDate(nextMatch.scheduledAt)} · {nextMatch.competition || 'Матч'}</small><strong>{nextMatch.venue === 'Дома' ? `${data.name} — ${nextMatch.opponent}` : `${nextMatch.opponent} — ${data.name}`}</strong></div>}<form className="profile-form" onSubmit={submit}><label className="full">Схема<select name="formation" defaultValue={data.tacticFormation || '4-3-3'}><option>4-3-3</option><option>4-2-3-1</option><option>4-4-2</option><option>3-4-3</option><option>3-5-2</option></select></label><label className="full">Предварительный состав и установка<textarea name="notes" defaultValue={data.tacticNotes} placeholder="Например: высокий прессинг; стартовый состав, замены и стандарты" /></label><div className="form-actions"><button className="button">Сохранить для школы</button></div></form></article>
    </section>
    <section className="card data-table coach-roster-table"><div className="table-head"><span>Игрок</span><span>Позиция</span><span>Номер</span></div>{data.players.map(x => <div key={x.playerId}><span><strong>{x.firstName} {x.lastName}</strong></span><span>{x.preferredPosition}</span><span>{x.shirtNumber ? `№ ${x.shirtNumber}` : '—'}</span></div>)}</section>
  </>
}
